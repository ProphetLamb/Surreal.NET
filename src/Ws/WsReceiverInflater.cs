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
    private readonly ClientWebSocket _ws;
    private readonly ChannelWriter<WsReceiverMessageReader> _out;
    private readonly RecyclableMemoryStreamManager _memoryManager;
    private readonly object _lock = new();
    private CancellationTokenSource? _cts;
    private Task? _execute;

    private readonly int _blockSize;
    private readonly int _messageSize;

    public WsReceiverInflater(ClientWebSocket ws, ChannelWriter<WsReceiverMessageReader> @out, RecyclableMemoryStreamManager memoryManager, int blockSize, int messageSize) {
        _ws = ws;
        _out = @out;
        _memoryManager = memoryManager;
        _blockSize = blockSize;
        _messageSize = messageSize;
    }

    private async Task Execute(CancellationToken ct) {
        Debug.Assert(ct.CanBeCanceled);
        while (!ct.IsCancellationRequested) {
            var buffer = ArrayPool<byte>.Shared.Rent(_blockSize);
            await Produce(buffer, ct).Inv();
            ct.ThrowIfCancellationRequested();
        }
    }

    private async Task Produce(byte[] buffer, CancellationToken ct) {
        // receive the first part
        var result = await _ws.ReceiveAsync(buffer, ct).Inv();
        // create a new message with a RecyclableMemoryStream
        // use buffer instead of the build the builtin IBufferWriter, bc of thread safely issues related to locking
        WsReceiverMessageReader msg = new(_memoryManager, _messageSize);
        // begin adding the message to the output
        var writeOutput = _out.WriteAsync(msg, ct);

        await msg.AppendResultAsync(buffer, result, ct).Inv();

        while (!result.EndOfMessage && !ct.IsCancellationRequested) {
            // receive more parts
            result = await _ws.ReceiveAsync(buffer, ct).Inv();
            await msg.AppendResultAsync(buffer, result, ct).Inv();
        }

        // finish adding the message to the output
        await writeOutput.Inv();
    }


    [MemberNotNullWhen(true, nameof(_cts)), MemberNotNullWhen(true, nameof(_execute))]
    public bool Connected => _cts is not null & _execute is not null;

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
        Task task = _execute;
        lock (_lock) {
            if (!Connected) {
                return;
            }
            _cts.Cancel();
            _cts.Dispose(); // not relly needed here
            _cts = null;
            _execute = null;
        }

        try {
            await task.Inv();
        } catch (OperationCanceledException) {
            // expected on close using cts
        } catch (WebSocketException) {
            // expected on abort
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
        _out.TryComplete();
    }
}
