using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Net.WebSockets;
using System.Threading.Channels;

using SurrealDB.Common;

namespace SurrealDB.Ws;

/// <summary>Sends messages from a channel to a websocket server.</summary>
public sealed class WsChannelRx : IDisposable {
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

    public void Dispose() {
        _ws.Dispose();
        _cts?.Dispose();
        _execute?.Dispose();
    }
}
