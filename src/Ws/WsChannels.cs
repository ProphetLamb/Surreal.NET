using System.Buffers;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Net.WebSockets;
using System.Threading.Channels;

using Microsoft.IO;

namespace SurrealDB.Ws;

/// <summary>Sends messages from a channel to a websocket server.</summary>
public sealed class WsChannelRx {
    private readonly ClientWebSocket _ws;
    private readonly ChannelReader<BufferedStreamReader> _in;
    private readonly object _lock = new();
    private CancellationTokenSource? _cts;
    private Task? _execute;

    public WsChannelRx(ClientWebSocket ws, ChannelReader<BufferedStreamReader> @in) {
        _ws = ws;
        _in = @in;
    }

    private static async Task Execute(ClientWebSocket output, ChannelReader<BufferedStreamReader> input, CancellationToken ct) {
        Debug.Assert(ct.CanBeCanceled);
        while (!ct.IsCancellationRequested) {
            var reader = await input.ReadAsync(ct).ConfigureAwait(false);

            bool isFinalBlock = false;
            while (!isFinalBlock && !ct.IsCancellationRequested) {
                var rom = await reader.ReadAsync(BufferedStreamReader.BUFFER_SIZE, ct).ConfigureAwait(false);
                isFinalBlock = rom.Length != BufferedStreamReader.BUFFER_SIZE;
                await output.SendAsync(rom, WebSocketMessageType.Text, isFinalBlock, ct).ConfigureAwait(false);
            }

            if (!isFinalBlock) {
                // ensure that the message is always terminated
                // no not pass a CancellationToken
                await output.SendAsync(default, WebSocketMessageType.Text, true, default).ConfigureAwait(false);
            }

            await reader.DisposeAsync().ConfigureAwait(false);
            ct.ThrowIfCancellationRequested();
        }
    }

    [MemberNotNullWhen(true, nameof(_cts)), MemberNotNullWhen(true, nameof(_execute))]
    public bool Connected => _cts is not null & _execute is not null;

    public void Open() {
        lock (_lock) {
            ThrowIfConnected();
            _cts = new();
            _execute = Execute(_ws, _in, _cts.Token);
        }
    }

    public Task Close() {
        Task task;
        lock (_lock) {
            ThrowIfDisconnected();
            _cts.Cancel();
            task = _execute;
            _execute = null;
        }
        return task;
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

/// <summary>Receives messages from a websocket server and passes them to a channel</summary>
public sealed class WsChannelTx {
    private readonly ClientWebSocket _ws;
    private readonly ChannelWriter<WsMessage> _out;
    private readonly RecyclableMemoryStreamManager _memoryManager;
    private readonly object _lock = new();
    private CancellationTokenSource? _cts;
    private Task? _execute;

    public WsChannelTx(ClientWebSocket ws, ChannelWriter<WsMessage> @out, RecyclableMemoryStreamManager memoryManager) {
        _ws = ws;
        _out = @out;
        _memoryManager = memoryManager;
    }

    private static async Task Execute(
        RecyclableMemoryStreamManager memoryManager,
        ClientWebSocket input,
        ChannelWriter<WsMessage> output,
        CancellationToken ct) {
        Debug.Assert(ct.CanBeCanceled);
        while (!ct.IsCancellationRequested) {
            var buffer = ArrayPool<byte>.Shared.Rent(BufferedStreamReader.BUFFER_SIZE);
            // receive the first part
            var result = await input.ReceiveAsync(buffer, ct).ConfigureAwait(false);
            // create a new message with a RecyclableMemoryStream
            // use buffer instead of the build the builtin IBufferWriter, bc of thread safely issues related to locking
            WsMessage msg = new(new RecyclableMemoryStream(memoryManager));
            // begin adding the message to the output
            var writeOutput = output.WriteAsync(msg, ct);
            using (var h = await msg.LockAsync(ct).ConfigureAwait(false)) {
                // write the first part to the message
                await h.Stream.WriteAsync(buffer.AsMemory(0, result.Count), ct).ConfigureAwait(false);
                // indicate, that a message has been received
                msg.SetReceived(result);
            }

            while (!result.EndOfMessage && !ct.IsCancellationRequested) {
                // receive more parts
                result = await input.ReceiveAsync(buffer, ct).ConfigureAwait(false);
                using var h = await msg.LockAsync(ct).ConfigureAwait(false);
                await h.Stream.WriteAsync(buffer.AsMemory(0, result.Count), ct).ConfigureAwait(false);
                msg.SetReceived(result);
                if (result.EndOfMessage) {
                    msg.SetEndOfMessage();
                }
                ct.ThrowIfCancellationRequested();
            }

            // finish adding the message to the output
            await writeOutput.ConfigureAwait(false);

            ArrayPool<byte>.Shared.Return(buffer);
            ct.ThrowIfCancellationRequested();
        }
    }

    [MemberNotNullWhen(true, nameof(_cts)), MemberNotNullWhen(true, nameof(_execute))]
    public bool Connected => _cts is not null & _execute is not null;

    public void Open() {
        lock (_lock) {
            ThrowIfConnected();
            _cts = new();
            _execute = Execute(_memoryManager, _ws, _out, _cts.Token);
        }
    }

    public Task Close() {
        Task task;
        lock (_lock) {
            ThrowIfDisconnected();
            _cts.Cancel();
            task = _execute;
            _execute = null;
        }
        return task;
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

public sealed class WsMessage : IDisposable, IAsyncDisposable {
    private readonly MemoryStream _buffer;
    private readonly SemaphoreSlim _lock = new(1, 1);
    private readonly TaskCompletionSource<object?> _endOfMessageEvent = new();
    private TaskCompletionSource<WebSocketReceiveResult> _receivedEvent = new();
    private int _endOfMessage;

    internal WsMessage(MemoryStream buffer) {
        _buffer = buffer;
        _endOfMessage = 0;
    }

    public async Task<Handle> LockAsync(CancellationToken ct) {
        await _lock.WaitAsync(ct).ConfigureAwait(false);
        return new(this);
    }

    public Handle Lock(CancellationToken ct) {
        _lock.Wait(ct);
        return new(this);
    }

    public void Dispose() {
        _endOfMessageEvent.TrySetCanceled();
        _lock.Dispose();
        _buffer.Dispose();
    }

    public ValueTask DisposeAsync() {
        _endOfMessageEvent.TrySetCanceled();
        _lock.Dispose();
        return _buffer.DisposeAsync();
    }

    internal void SetEndOfMessage() {
        var endOfMessage = Interlocked.Exchange(ref _endOfMessage, 1);
        if (endOfMessage == 0) {
            // finish the AwaitEndOfMessage task
            _endOfMessageEvent.SetResult(null);
        }
    }

    internal void SetReceived(WebSocketReceiveResult count) {
        var receivedEvent = Interlocked.Exchange(ref _receivedEvent, new());
        receivedEvent.SetResult(count);
    }

    public Task EndOfMessageAsync() {
        return _endOfMessageEvent.Task;
    }

    public Task<WebSocketReceiveResult> ReceivedAsync() {
        return _receivedEvent.Task;
    }

    public readonly struct Handle : IDisposable {
        private readonly WsMessage _msg;

        internal Handle(WsMessage msg) {
            _msg = msg;
        }

        public MemoryStream Stream => _msg._buffer;

        public bool EndOfMessage => _msg._endOfMessage == 0;

        public void Dispose() {
            _msg._lock.Release();
        }
    }
}
