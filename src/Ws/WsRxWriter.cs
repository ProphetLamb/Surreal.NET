using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Net.WebSockets;
using System.Threading.Channels;

using SurrealDB.Common;

namespace SurrealDB.Ws;

/// <summary>Sends messages from a channel to a websocket server.</summary>
public sealed class WsRxWriter : IDisposable {
    private readonly ClientWebSocket _ws;
    private readonly ChannelReader<BufferStreamReader> _in;
    private readonly object _lock = new();
    private CancellationTokenSource? _cts;
    private Task? _execute;

    public WsRxWriter(ClientWebSocket ws, ChannelReader<BufferStreamReader> @in) {
        _ws = ws;
        _in = @in;
    }

    private static async Task Execute(ClientWebSocket output, ChannelReader<BufferStreamReader> input, CancellationToken ct) {
        Debug.Assert(ct.CanBeCanceled);
        while (!ct.IsCancellationRequested) {
            var reader = await input.ReadAsync(ct).Inv();

            bool isFinalBlock = false;
            while (!isFinalBlock && !ct.IsCancellationRequested) {
                var rom = await reader.ReadAsync(BufferStreamReader.BUFFER_SIZE, ct).Inv();
                isFinalBlock = rom.Length != BufferStreamReader.BUFFER_SIZE;
                await output.SendAsync(rom, WebSocketMessageType.Text, isFinalBlock, ct).Inv();
            }

            if (!isFinalBlock) {
                // ensure that the message is always terminated
                // no not pass a CancellationToken
                await output.SendAsync(default, WebSocketMessageType.Text, true, default).Inv();
            }

            await reader.DisposeAsync().Inv();
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

    public void Dispose() {
        _cts?.Dispose();
        _execute?.Dispose();
    }
}
