using System.Buffers;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Threading.Channels;

using SurrealDB.Common;

namespace SurrealDB.Ws;

/// <summary>Listens for <see cref="WsMessageReader"/>s and dispatches them by their headers to different <see cref="IHandler"/>s.</summary>
internal sealed class WsTxConsumer : IDisposable {
    private readonly ChannelReader<WsMessageReader> _in;
    private readonly DisposingCache<string, IHandler> _handlers;
    private readonly object _lock = new();
    private CancellationTokenSource? _cts;
    private Task? _execute;

    public WsTxConsumer(ChannelReader<WsMessageReader> channel, int maxHeaderBytes, TimeSpan cacheSlidingExpiration, TimeSpan cacheEvictionInterval) {
        _in = channel;
        MaxHeaderBytes = maxHeaderBytes;
        _handlers = new(cacheSlidingExpiration, cacheEvictionInterval);
    }

    public int MaxHeaderBytes { get; }

    [MemberNotNullWhen(true, nameof(_cts)), MemberNotNullWhen(true, nameof(_execute))]
    public bool Connected => _cts is not null & _execute is not null;

    private async Task Execute(CancellationToken ct) {
        Debug.Assert(ct.CanBeCanceled);

        while (!ct.IsCancellationRequested) {
            await Consume(ct).Inv();
        }
    }

    private async Task Consume(CancellationToken ct) {
        var message = await _in.ReadAsync(ct).Inv();

        // receive the first part of the message
        var bytes = ArrayPool<byte>.Shared.Rent(MaxHeaderBytes);
        int read = await message.ReadAsync(bytes, ct).Inv();
        // peek instead of reading
        message.Position = 0;
        // parse the header portion of the stream, without reading the `result` property.
        // the header is a sum-type of all possible headers.
        var header = HeaderHelper.Parse(bytes.AsSpan(0, read));
        ArrayPool<byte>.Shared.Return(bytes);

        // find the handler
        string? id = header.Id;
        if (id is null || !_handlers.TryGetValue(id, out var handler)) {
            // invalid format, or no registered -> discard message
            await message.DisposeAsync().Inv();
            return;
        }

        // dispatch the message to the handler
        bool persist = handler.Persistent;
        try {
            handler.Dispatch(new(header, message));
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

    public void Open() {
        ThrowIfConnected();
        lock (_lock) {
            if (Connected) {
                return;
            }
            _cts = new();
            _execute = Execute(_cts.Token);
        }
    }

    public async Task Close() {
        ThrowIfDisconnected();
        Task task;
        lock (_lock) {
            if (!Connected) {
                return;
            }
            _cts.Cancel();
            _cts.Dispose(); // not relly needed here
            _cts = null;
            task = _execute;
            _execute = null;
        }

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
