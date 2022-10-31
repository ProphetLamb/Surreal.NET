using Microsoft.IO;

namespace SurrealDB.Ws;

public sealed record WsClientOptions {
    public int ChannelRxMessagesMax { get; init; } = 256;
    public int ChannelTxMessagesMax { get; init; } = 256;
    public int HeaderBytesMax { get; init; } = 512;
    /// <summary>The id is base64 encoded. 6 bytes = 4 characters. Use values in steps of 6.</summary>
    public int IdBytes { get; init; } = 6;
    public RecyclableMemoryStreamManager? MemoryManager { get; init; }

    internal static WsClientOptions Default => new();
}
