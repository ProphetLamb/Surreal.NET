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
        var log = WsReceiverDeflaterEventSource.Log;

        ct.ThrowIfCancellationRequested();

        // log that we are waiting for a message from the channel
        log.MessageAwaiting();

        var message = await ReadAsync(ct).Inv();
        // log that a message has been retrieved from the channel
        log.MessageReceived(message.Header.Id);

        // find the handler
        string? id = message.Header.Id;
        if (id is null || !_handlers.TryGetValue(id, out var handler)) {
            // invalid format, or no registered -> discard message
            message.Dispose();
            // log that the message has been discarded
            log.MessageDiscarded(id);
            return;
        }
        Debug.Assert(id == handler.Id);

        // dispatch the message to the handler
        try {
            handler.Dispatch(message);
        } catch (Exception ex) {
            // handler is canceled -> unregister
            Unregister(id);
            // log that the dispatch has resulted in a exception
            log.HandlerUnregisteredAfterException(id, ex);
        }

        if (!handler.Persistent) {
            Unregister(id);
            // log that the handler has been unregistered
            log.HandlerUnregisterdFleeting(id);
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
        var log = WsReceiverDeflaterEventSource.Log;

        ThrowIfConnected();
        _cts = new();
        _execute = Execute(_cts.Token);

        log.Opened();
    }

    public async Task CloseAsync() {
        var log = WsReceiverDeflaterEventSource.Log;

        ThrowIfDisconnected();
        Task task;
        _cts.Cancel();
        _cts.Dispose(); // not relly needed here
        _cts = null;
        task = _execute;
        _execute = null;

        log.CloseBegin();

        try {
            await task.Inv();
        } catch (OperationCanceledException) {
            // expected on close using cts
        }

        log.CloseFinish();
    }

    public void Dispose() {
        var log = WsReceiverDeflaterEventSource.Log;

        _cts?.Cancel();
        _cts?.Dispose();

        log.Disposed();
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
}
