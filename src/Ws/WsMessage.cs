using System.Collections.ObjectModel;
using System.Net.WebSockets;
using System.Threading.Channels;

namespace SurrealDB.Ws;

public sealed class WsMessage : IDisposable, IAsyncDisposable {
    private readonly Channel<WebSocketReceiveResult> _channel = Channel.CreateUnbounded<WebSocketReceiveResult>();
    private readonly MemoryStream _buffer;
    private readonly SemaphoreSlim _lock = new(1, 1);
    private readonly TaskCompletionSource<object?> _endOfMessageEvent = new();
    private int _endOfMessage;

    internal WsMessage(MemoryStream buffer) {
        _buffer = buffer;
        _endOfMessage = 0;
    }

    public bool IsEndOfMessage => Interlocked.Add(ref _endOfMessage, 0) == 1;

    public async Task<Handle> LockStreamAsync(CancellationToken ct) {
        await _lock.WaitAsync(ct).ConfigureAwait(false);
        return new(this);
    }

    public Handle LockStream(CancellationToken ct) {
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

    public Task EndOfMessageAsync() {
        return _endOfMessageEvent.Task;
    }

    internal ValueTask SetReceivedAsync(WebSocketReceiveResult result, CancellationToken ct) {
        return _channel.Writer.WriteAsync(result, ct);
    }

    public ValueTask<WebSocketReceiveResult> ReceiveAsync(CancellationToken ct) {
        return _channel.Reader.ReadAsync(ct);
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
