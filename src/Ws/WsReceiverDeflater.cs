using System.Buffers;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Threading.Channels;

using SurrealDB.Common;

namespace SurrealDB.Ws;

/// <summary>Listens for <see cref="WsReceiverMessageReader"/>s and dispatches them by their headers to different <see cref="IHandler"/>s.</summary>
internal sealed class WsReceiverDeflater : IDisposable {
    private readonly ChannelReader<WsReceiverMessageReader> _channel;
    private readonly DisposingCache<string, IHandler> _handlers;
    private CancellationTokenSource? _cts;
    private Task? _execute;
    private readonly int _maxHeaderBytes;

    public WsReceiverDeflater(ChannelReader<WsReceiverMessageReader> channel, int maxHeaderBytes, TimeSpan cacheSlidingExpiration, TimeSpan cacheEvictionInterval) {
        _channel = channel;
        _maxHeaderBytes = maxHeaderBytes;
        _handlers = new(cacheSlidingExpiration, cacheEvictionInterval);
    }

    [MemberNotNullWhen(true, nameof(_cts)), MemberNotNullWhen(true, nameof(_execute))]
    public bool Connected => _cts is not null & _execute is not null;

    private async Task Execute(CancellationToken ct) {
        Debug.Assert(ct.CanBeCanceled);

        while (!ct.IsCancellationRequested) {
            await Consume(ct).Inv();
            ct.ThrowIfCancellationRequested();
        }
    }

    private async Task Consume(CancellationToken ct) {
        var message = await ReadAsync(ct).Inv();
        ct.ThrowIfCancellationRequested();

        // find the handler
        string? id = message.Header.Id;
        if (id is null || !_handlers.TryGetValue(id, out var handler)) {
            // invalid format, or no registered -> discard message
            await message.Reader.DisposeAsync().Inv();
            return;
        }

        // dispatch the message to the handler
        bool persist = handler.Persistent;
        try {
            handler.Dispatch(message);
        } catch (OperationCanceledException) {
            // handler is canceled -> unregister
            persist = false;
        }

        if (!persist) {
            Unregister(handler.Id);
        }
    }

    public void Unregister(string id) {
        if (_handlers.TryRemove(id, out var handler))
        {
            handler.Dispose();
        }
    }

    public bool RegisterOrGet(IHandler handler) {
        return _handlers.TryAdd(handler.Id, handler);
    }

    private async Task<WsHeaderWithMessage> ReadAsync(CancellationToken ct) {
        var message = await _channel.ReadAsync(ct).Inv();

        // receive the first part of the message
        var bytes = ArrayPool<byte>.Shared.Rent(_maxHeaderBytes);
        int read = await message.ReadAsync(bytes, ct).Inv();
        // peek instead of reading
        message.Position = 0;
        // parse the header portion of the stream, without reading the `result` property.
        // the header is a sum-type of all possible headers.
        var header = HeaderHelper.Parse(bytes.AsSpan(0, read));
        ArrayPool<byte>.Shared.Return(bytes);
        return new(header, message);
    }

    public void Open() {
        ThrowIfConnected();
        _cts = new();
        _execute = Execute(_cts.Token);
    }

    public async Task CloseAsync() {
        ThrowIfDisconnected();
        Task task;
        _cts.Cancel();
        _cts.Dispose(); // not relly needed here
        _cts = null;
        task = _execute;
        _execute = null;

        try {
            await task.Inv();
        } catch (OperationCanceledException) {
            // expected on close using cts
        }
    }

    [MemberNotNull(nameof(_cts)), MemberNotNull(nameof(_execute))]
    private void ThrowIfDisconnected() {
        if (!Connected) {
            throw new InvalidOperationException("The connection is not open.");
        }
    }

    private void ThrowIfConnected() {
        if (Connected) {
            throw new InvalidOperationException("The connection is already open");
        }
    }

    public void Dispose() {
        _cts?.Cancel();
        _cts?.Dispose();
    }
}
