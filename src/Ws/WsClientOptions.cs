using Microsoft.IO;

using SurrealDB.Common;

namespace SurrealDB.Ws;

public sealed record WsClientOptions : ValidateReadonly {
    public const int MaxArraySize = 0X7FFFFFC7;

    /// <summary>The maximum number of pending messages in the client inbound channel. Default 1024</summary>
    /// <remarks>This does not refer to the number of simultaneous queries, but the number of unread messages, the size of the "inbox".
    /// A message may consist of multiple blocks. Only the message counts towards this number.</remarks>
    public int TxChannelCapacity {
        get => _txChannelCapacity;
        set => Set(out _txChannelCapacity, in value);
    }

    /// <summary>The maximum number of pending blocks in a single message channel. Default 64</summary>
    /// <remarks>The number of blocks of a message that can be received by the client, before they are consumed,
    /// by reading from the <see cref="WsReceiverMessageReader"/>.
    /// A block have up to <see cref="RecyclableMemoryStreamManager.BlockSize"/> bytes.</remarks>
    public int MessageChannelCapacity {
        get => _messageChannelCapacity;
        set => Set(out _messageChannelCapacity, in value);
    }

    /// <summary>The maximum number of bytes a received header can consist of. Default 4 * 1024bytes</summary>
    /// <remarks>The client receives a message with a <see cref="WsHeader"/> and the message.
    /// This is the length the socket "peeks" at the beginning of the network stream, in oder to fully deserialize the <see cref="RspHeader"/> or <see cref="NtyHeader"/>.
    /// The entire header must be contained within the peeked memory.
    /// The length is bound to <see cref="RecyclableMemoryStreamManager.BlockSize"/>.
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

    /// <summary>The maximum time a request is awaited, before a <see cref="OperationCanceledException"/> is thrown.</summary>
    /// <remarks>Limited by the internal cache eviction timeout (1s) & pressure/traffic.</remarks>
    public TimeSpan RequestExpiration {
        get => _requestExpiration;
        set => Set(out _requestExpiration, value);
    }

    private RecyclableMemoryStreamManager? _memoryManager;
    private int _idBytes = 6;
    private int _receiveHeaderBytesMax = 4 * 1024;
    private int _txChannelCapacity = 1024;
    private int _messageChannelCapacity = 64;
    private TimeSpan _requestExpiration = TimeSpan.FromSeconds(10);

    public void ValidateAndMakeReadonly() {
        if (!IsReadonly()) {
            ValidateOrThrow();
            MakeReadonly();
        }
    }

    protected override IEnumerable<(string PropertyName, string Message)> Validations() {
        if (TxChannelCapacity <= 0) {
            yield return (nameof(TxChannelCapacity), "cannot be less then or equal to zero");
        }
        if (TxChannelCapacity > MaxArraySize) {
            yield return (nameof(TxChannelCapacity), "cannot be greater then MaxArraySize");
        }

        if (MessageChannelCapacity <= 0) {
            yield return (nameof(MessageChannelCapacity), "cannot be less then or equal to zero");
        }
        if (MessageChannelCapacity > MaxArraySize) {
            yield return (nameof(MessageChannelCapacity), "cannot be greater then MaxArraySize");
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

        if (RequestExpiration <= TimeSpan.Zero) {
            yield return (nameof(RequestExpiration), "expiration time cannot be less then or equal to zero");
        }
    }

    public static WsClientOptions Default { get; } = CreateDefault();

    private static WsClientOptions CreateDefault() {
        WsClientOptions o = new() { MemoryManager = new(), };
        o.ValidateAndMakeReadonly();
        return o;
    }
}
