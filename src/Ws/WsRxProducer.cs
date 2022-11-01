using System.Threading.Channels;

using SurrealDB.Common;

namespace SurrealDB.Ws;

public readonly struct WsRxProducer : IDisposable {
    private readonly ChannelWriter<BufferStreamReader> _channel;
    private readonly int _bufferSize;

    public WsRxProducer(ChannelWriter<BufferStreamReader> channel, int bufferSize) {
        _channel = channel;
        _bufferSize = bufferSize;
    }

    public async Task SendAsync(Stream stream) {
        // reader is disposed by the consumber
        BufferStreamReader reader = new(stream, _bufferSize);
        await _channel.WriteAsync(reader);
    }

    public void Dispose() {
        _channel.Complete();
    }
}
