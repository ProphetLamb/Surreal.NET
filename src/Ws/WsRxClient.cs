using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Net.WebSockets;
using System.Threading.Channels;

using SurrealDB.Common;

namespace SurrealDB.Ws;

public sealed class WsRxClient : IDisposable {
    private readonly ClientWebSocket _ws;
    private readonly Channel<BufferStreamReader> _channel;
    private readonly WsRxWriter _rx;

    public WsRxClient(ClientWebSocket ws, Channel<BufferStreamReader> channel) {
        _ws = ws;
        _channel = channel;
        _rx = new(ws, channel.Reader);
    }

    public bool Connected => _rx.Connected;


    public ValueTask SendAsync(Stream stream) {
        BufferStreamReader reader = new(stream);
        return _channel.Writer.WriteAsync(reader);
    }

    public void Open() {
        _rx.Open();
    }

    public async Task Close() {
        await _rx.Close().Inv();
    }


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
        _rx.Dispose();
        _channel.Writer.Complete();
    }
}
