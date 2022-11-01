using System.Collections.Concurrent;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Threading.Channels;

using SurrealDB.Common;

namespace SurrealDB.Ws;

/// <summary>Listens for <see cref="WsMessageReader"/>s and dispatches them by their headers to different <see cref="IHandler"/>s.</summary>
internal struct WsTxConsumer : IDisposable {
    private readonly ChannelReader<WsMessageReader> _in;
    private readonly ConcurrentDictionary<string, IHandler> _handlers = new();
    private readonly object _lock = new();
    private CancellationTokenSource? _cts;
    private Task? _execute;

    public WsTxConsumer(ChannelReader<WsMessageReader> channel, int maxHeaderBytes) {
        _in = channel;
        MaxHeaderBytes = maxHeaderBytes;
    }

    public int MaxHeaderBytes { get; }

    [MemberNotNullWhen(true, nameof(_cts)), MemberNotNullWhen(true, nameof(_execute))]
    public bool Connected => _cts is not null & _execute is not null;

    private async Task Execute(CancellationToken ct) {
        Debug.Assert(ct.CanBeCanceled);

        while (!ct.IsCancellationRequested) {
            ThrowIfDisconnected();
            await Consume(ct).Inv();

            ct.ThrowIfCancellationRequested();
        }
    }

    private async Task Consume(CancellationToken ct) {
        var message = await _in.ReadAsync(ct).Inv();

        // receive the first part of the message
        var result = await message.ReceiveAsync(ct).Inv();
        // parse the header from the message
        WsHeader header = PeekHeader(message, result.Count);

        // find the handler
        string? id = header.Id;
        if (id is null || !_handlers.TryGetValue(id, out var handler)) {
            // invalid format, or no registered -> discard message
            await message.DisposeAsync().Inv();
            return;
        }

        // dispatch the message to the handler
        try {
            handler.Dispatch(new(header, message));
        } catch (OperationCanceledException) {
            // handler is canceled -> unregister
            Unregister(handler.Id);
        }

        if (!handler.Persistent) {
            // handler is only used once -> unregister
            Unregister(handler.Id);
        }
    }

    private WsHeader PeekHeader(Stream stream, int seekLength) {
        Span<byte> bytes = stackalloc byte[Math.Min(MaxHeaderBytes, seekLength)];
        int read = stream.Read(bytes);
        // peek instead of reading
        stream.Position = 0;
        Debug.Assert(read == bytes.Length);
        return HeaderHelper.Parse(bytes);
    }

    public void Unregister(string id) {
        if (_handlers.TryRemove(id, out var handler))
        {
            handler.Dispose();
        }
    }

    public bool TryRegister(IHandler handler) {
        return _handlers.TryAdd(handler.Id, handler);
    }

    public void Open() {
        lock (_lock) {
            ThrowIfConnected();
            _cts = new();
            _execute = Execute(_cts.Token);
        }
    }

    public async Task Close() {
        Task task;
        lock (_lock) {
            ThrowIfDisconnected();
            _cts.Cancel();
            task = _execute;
            _execute = null;
        }
        await task.Inv();
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
        _execute?.Dispose();
    }
}
