using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Net.WebSockets;
using System.Threading.Channels;

using SurrealDB.Common;

namespace SurrealDB.Ws;

/// <summary>Sends messages from a channel to a websocket server.</summary>
public sealed class WsRx {
    private readonly ClientWebSocket _ws;
    private readonly int _blockSize;

    public WsRx(ClientWebSocket ws, int blockSize) {
        _ws = ws;
        _blockSize = blockSize;
    }

    public async Task SendAsync(Stream stream, CancellationToken ct) {
        // reader is disposed by the consumer
        using BufferStreamReader reader = new(stream, _blockSize);
        await Consume(reader, ct).Inv();
    }


    private async Task Consume(BufferStreamReader reader, CancellationToken ct) {
        bool isFinalBlock = false;
        while (!isFinalBlock && !ct.IsCancellationRequested) {
            var rom = await reader.ReadAsync(_blockSize, ct).Inv();
            isFinalBlock = rom.Length != _blockSize;
            await _ws.SendAsync(rom, WebSocketMessageType.Text, isFinalBlock, ct).Inv();
        }

        if (!isFinalBlock) {
            // ensure that the message is always terminated
            // no not pass a CancellationToken
            await _ws.SendAsync(default, WebSocketMessageType.Text, true, default).Inv();
        }
    }
}
