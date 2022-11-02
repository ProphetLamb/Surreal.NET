using System.Buffers;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Net.WebSockets;
using System.Threading.Channels;

using Microsoft.IO;

using SurrealDB.Common;

namespace SurrealDB.Ws;

/// <summary>Receives messages from a websocket server and passes them to a channel</summary>
public sealed class WsReceiverInflater : IDisposable {
    private readonly ClientWebSocket _socket;
    private readonly ChannelWriter<WsReceiverMessageReader> _channel;
    private readonly RecyclableMemoryStreamManager _memoryManager;
    private CancellationTokenSource? _cts;
    private Task? _execute;

    private readonly int _blockSize;
    private readonly int _messageSize;

    public WsReceiverInflater(ClientWebSocket socket, ChannelWriter<WsReceiverMessageReader> channel, RecyclableMemoryStreamManager memoryManager, int blockSize, int messageSize) {
        _socket = socket;
        _channel = channel;
        _memoryManager = memoryManager;
        _blockSize = blockSize;
        _messageSize = messageSize;
    }

    private async Task Execute(CancellationToken ct) {
        Debug.Assert(ct.CanBeCanceled);
        while (!ct.IsCancellationRequested) {
            var buffer = ArrayPool<byte>.Shared.Rent(_blockSize);
            await Produce(buffer, ct).Inv();
            ArrayPool<byte>.Shared.Return(buffer);
            ct.ThrowIfCancellationRequested();
        }
    }

    private async Task Produce(byte[] buffer, CancellationToken ct) {
        var log = WsReceiverInflaterEventSource.Log;

        // log that we are waiting for the socket
        log.SocketWaiting();
        // receive the first part
        var result = await _socket.ReceiveAsync(buffer, ct).Inv();
        // log that we have received a message from the socket
        log.SockedReceived(result);

        ct.ThrowIfCancellationRequested();
        // create a new message with a RecyclableMemoryStream
        // use buffer instead of the build the builtin IBufferWriter, bc of thread safely issues related to locking
        WsReceiverMessageReader msg = new(_memoryManager, _messageSize);
        // begin adding the message to the output
        var push = _channel.WriteAsync(msg, ct);

        await msg.AppendResultAsync(buffer, result, ct).Inv();

        while (!result.EndOfMessage && !ct.IsCancellationRequested) {
            // receive more parts
            result = await _socket.ReceiveAsync(buffer, ct).Inv();
            // log that we have received a message from the socket
            log.SockedReceived(result);
            await msg.AppendResultAsync(buffer, result, ct).Inv();
        }

        // log that we have completely received the message
        log.MessageReceiveFinished();
        // finish adding the message to the output
        await push.Inv();
        // log that the message has been pushed to the channel
        log.MessagePushed();
    }


    [MemberNotNullWhen(true, nameof(_cts)), MemberNotNullWhen(true, nameof(_execute))]
    public bool Connected => _cts is not null & _execute is not null;

    public void Open() {
        var log = WsReceiverInflaterEventSource.Log;

        ThrowIfConnected();
        _cts = new();
        _execute = Execute(_cts.Token);

        log.Opened();
    }

    public async Task CloseAsync() {
        var log = WsReceiverInflaterEventSource.Log;

        ThrowIfDisconnected();
        var task = _execute;
        _cts.Cancel();
        _cts.Dispose(); // not relly needed here
        _cts = null;
        _execute = null;

        log.CloseBegin();

        try {
            await task.Inv();
        } catch (OperationCanceledException) {
            // expected on close using cts
        } catch (WebSocketException) {
            // expected on abort
        }

        log.CloseFinish();
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
        var log = WsReceiverInflaterEventSource.Log;

        _cts?.Cancel();
        _cts?.Dispose();
        _channel.TryComplete();

        log.Disposed();
    }
}
