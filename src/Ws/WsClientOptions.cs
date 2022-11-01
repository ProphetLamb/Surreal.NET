using System.Text;

using Microsoft.IO;

using SurrealDB.Common;

namespace SurrealDB.Ws;

public sealed record WsClientOptions : ValidateReadonly {
    public const int MaxArraySize = 0X7FFFFFC7;

    /// <summary>The maximum number of messages in the client outbound channel.</summary>
    /// <remarks>A message may consist of multiple blocks. Only the message counts towards this number.</remarks>
    public int ChannelRxMessagesMax {
        get => _channelRxMessagesMax;
        set => Set(out _channelRxMessagesMax, in value);
    }
    /// <summary>The maximum number of messages in the client inbound channel.</summary>
    /// <remarks>A message may consist of multiple blocks. Only the message counts towards this number.</remarks>
    public int ChannelTxMessagesMax {
        get => _channelTxMessagesMax;
        set => Set(out _channelTxMessagesMax, in value);
    }
    /// <summary>The maximum number of bytes a received header can consist of.</summary>
    /// <remarks>The client receives a message with a <see cref="WsHeader"/> and the message.
    /// This is the length the socket "peeks" at the beginning of the network stream, in oder to fully deserialize the <see cref="RspHeader"/> or <see cref="NtyHeader"/>.
    /// The entire header must be contained within the peeked memory.
    /// The length is bound to <see cref="MemoryManager"/> <see cref="RecyclableMemoryStreamManager.BlockSize"/>.
    /// Longer lengths introduce additional overhead.</remarks>
    public int ReceiveHeaderBytesMax {
        get => _receiveHeaderBytesMax;
        set => Set(out _receiveHeaderBytesMax, in value);
    }
    /// <summary>The number of bytes the id consists of.</summary>
    /// <remarks>The id is base64 encoded, therefore 6 bytes = 4 characters. Use values in steps of 6.</remarks>
    public int IdBytes {
        get => _idBytes;
        set => Set(out _idBytes, in value);
    }
    /// <summary>Defines the resize behaviour of the streams used for handling messages.</summary>
    public RecyclableMemoryStreamManager MemoryManager {
        get => _memoryManager!; // Validated not null
        set => Set(out _memoryManager, in value);
    }

    private RecyclableMemoryStreamManager? _memoryManager;
    private int _idBytes = 6;
    private int _receiveHeaderBytesMax = 512;
    private int _channelTxMessagesMax = 256;
    private int _channelRxMessagesMax = 256;

    public void ValidateAndMakeReadonly() {
        if (!IsReadonly()) {
            ValidateOrThrow();
            MakeReadonly();
        }
    }

    protected override IEnumerable<(string PropertyName, string Message)> Validations() {
        if (ChannelRxMessagesMax <= 0) {
            yield return (nameof(ChannelRxMessagesMax), "cannot be less then or equal to zero");
        }
        if (ChannelRxMessagesMax > MaxArraySize) {
            yield return (nameof(ChannelRxMessagesMax), "cannot be greater then MaxArraySize");
        }

        if (ChannelTxMessagesMax <= 0) {
            yield return (nameof(ChannelTxMessagesMax), "cannot be less then or equal to zero");
        }
        if (ChannelTxMessagesMax > MaxArraySize) {
            yield return (nameof(ChannelTxMessagesMax), "cannot be greater then MaxArraySize");
        }

        if (ReceiveHeaderBytesMax <= 0) {
            yield return (nameof(ReceiveHeaderBytesMax), "cannot be less then or equal to zero");
        }

        if (ReceiveHeaderBytesMax > (_memoryManager?.BlockSize ?? 0)) {
            yield return (nameof(ReceiveHeaderBytesMax), "cannot be greater then MemoryManager.BlockSize");
        }

        if (_memoryManager is null) {
            yield return (nameof(MemoryManager), "cannot be null");
        }
    }

    internal static WsClientOptions Default => new();
}
