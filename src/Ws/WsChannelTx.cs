using System.Buffers;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Net.WebSockets;
using System.Threading.Channels;

using Microsoft.IO;

using SurrealDB.Common;

namespace SurrealDB.Ws;

/// <summary>Receives messages from a websocket server and passes them to a channel</summary>
public sealed class WsChannelTx : IDisposable {
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
            using (var h = await msg.LockStreamAsync(ct).ConfigureAwait(false)) {
                // write the first part to the message and notify listeners
                await h.Stream.WriteAsync(buffer.AsMemory(0, result.Count), ct).ConfigureAwait(false);
                await msg.SetReceivedAsync(result, ct).Inv();
            }

            while (!result.EndOfMessage && !ct.IsCancellationRequested) {
                // receive more parts
                result = await input.ReceiveAsync(buffer, ct).ConfigureAwait(false);
                using var h = await msg.LockStreamAsync(ct).ConfigureAwait(false);
                await h.Stream.WriteAsync(buffer.AsMemory(0, result.Count), ct).ConfigureAwait(false);
                await msg.SetReceivedAsync(result, ct).Inv();

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

    public void Dispose() {
        _ws.Dispose();
        _cts?.Dispose();
        _execute?.Dispose();
        _out.Complete();
    }
}
