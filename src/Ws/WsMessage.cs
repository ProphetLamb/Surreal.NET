using System.Net.WebSockets;
using System.Threading.Channels;

using SurrealDB.Common;

namespace SurrealDB.Ws;

public sealed class WsMessage : IDisposable, IAsyncDisposable {
    private readonly Channel<WebSocketReceiveResult> _channel = Channel.CreateUnbounded<WebSocketReceiveResult>();
    private readonly MemoryStream _stream;
    private readonly SemaphoreSlim _lock = new(1, 1);
    private readonly TaskCompletionSource<object?> _endOfMessageEvent = new();
    private int _endOfMessage;

    internal WsMessage(MemoryStream stream) {
        _stream = stream;
        _endOfMessage = 0;
    }

    public bool IsEndOfMessage => Interlocked.Add(ref _endOfMessage, 0) == 1;

    /// <summary>The underlying stream.</summary>
    /// <remarks>Use a <c>lock</c> (as in <see cref="Monitor.Enter(object?)"/>) before accessing!</remarks>
    public MemoryStream Stream => _stream;

    public void Dispose() {
        _endOfMessageEvent.TrySetCanceled();
        _lock.Dispose();
        _stream.Dispose();
    }

    public ValueTask DisposeAsync() {
        _endOfMessageEvent.TrySetCanceled();
        _lock.Dispose();
        return _stream.DisposeAsync();
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
}
