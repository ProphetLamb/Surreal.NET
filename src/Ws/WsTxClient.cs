using System.Buffers;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Net.WebSockets;
using System.Threading.Channels;

using Microsoft.IO;

using SurrealDB.Common;

namespace SurrealDB.Ws;

/// <summary>Listens for <see cref="WsMessage"/>s and dispatches them by their headers to different <see cref="IHandler"/>s.</summary>
internal sealed class WsTxClient : IDisposable {
    private readonly ChannelReader<WsMessage> _in;
    private readonly WsTxReader _tx;
    private readonly ConcurrentDictionary<string, IHandler> _handlers = new();
    private readonly object _lock = new();
    private CancellationTokenSource? _cts;
    private Task? _execute;

    public WsTxClient(ClientWebSocket ws, Channel<WsMessage> channel, RecyclableMemoryStreamManager memoryManager, int maxHeaderBytes) {
        _in = channel.Reader;
        _tx = new(ws, channel.Writer, memoryManager);
        MaxHeaderBytes = maxHeaderBytes;
    }

    public int MaxHeaderBytes { get; }

    [MemberNotNullWhen(true, nameof(_cts)), MemberNotNullWhen(true, nameof(_execute))]
    public bool Connected => _cts is not null & _execute is not null;

    private async Task Execute(CancellationToken ct) {
        Debug.Assert(ct.CanBeCanceled);

        while (!ct.IsCancellationRequested) {
            ThrowIfDisconnected();
            var message = await _in.ReadAsync(ct).Inv();

            // receive the first part of the message
            var result = await message.ReceiveAsync(ct).Inv();
            WsHeader header;
            lock (message.Stream) {
                // parse the header from the message
                header = PeekHeader(message.Stream, result.Count);
            }

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
            ct.ThrowIfCancellationRequested();
        }
    }

    private WsHeader PeekHeader(MemoryStream stream, int seekLength) {
        Span<byte> bytes = stackalloc byte[MaxHeaderBytes].ClipLength(seekLength);
        int read = stream.Read(bytes);
        // peek instead of reading
        stream.Position -= read;
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
        _tx.Open();
    }

    public async Task Close() {
        await _tx.Close().Inv();
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
        Debug.Assert(_tx.Connected == Connected);
        if (!Connected) {
            throw new InvalidOperationException("The connection is not open.");
        }
    }

    private void ThrowIfConnected() {
        Debug.Assert(_tx.Connected == Connected);
        if (Connected) {
            throw new InvalidOperationException("The connection is already open");
        }
    }

    public void Dispose() {
        _tx.Dispose();
        _cts?.Cancel();
        _cts?.Dispose();
        _execute?.Dispose();
    }
}
