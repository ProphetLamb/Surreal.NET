using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Net.WebSockets;
using System.Runtime.CompilerServices;
using System.Threading.Channels;

using SurrealDB.Common;

namespace SurrealDB.Ws;

public sealed class WsMessageReader : Stream {
    private readonly Channel<WebSocketReceiveResult> _channel = Channel.CreateUnbounded<WebSocketReceiveResult>();
    private readonly MemoryStream _stream;
    private int _endOfMessage;

    internal WsMessageReader(MemoryStream stream) {
        _stream = stream;
        _endOfMessage = 0;
    }

    public bool HasReceivedEndOfMessage => Interlocked.Add(ref _endOfMessage, 0) == 1;

    protected override void Dispose(bool disposing) {
        if (!disposing) {
            return;
        }

        Interlocked.MemoryBarrierProcessWide();
        _stream.Dispose();
    }

    private async ValueTask SetReceivedAsync(WebSocketReceiveResult result, CancellationToken ct) {
        await _channel.Writer.WriteAsync(result, ct).Inv();
        if (result.EndOfMessage) {
            Interlocked.Exchange(ref _endOfMessage, 1);
        }
    }

    public ValueTask<WebSocketReceiveResult> ReceiveAsync(CancellationToken ct) {
        return _channel.Reader.ReadAsync(ct);
    }

    public WebSocketReceiveResult Receive(CancellationToken ct) {
        return ReceiveAsync(ct).Result;
    }

    internal ValueTask AppendResultAsync(ReadOnlyMemory<byte> buffer, WebSocketReceiveResult result, CancellationToken ct) {
        ReadOnlySpan<byte> span = buffer.Span.Slice(0, result.Count);
        lock (_stream) {
            var pos = _stream.Position;
            _stream.Write(span);
            _stream.Position = pos;
        }

        return SetReceivedAsync(result, ct);
    }

#region Stream members

    public override bool CanRead => true;
    public override bool CanSeek => true;
    public override bool CanWrite => false;
    public override long Length {
        get {
            Interlocked.MemoryBarrierProcessWide();
            return _stream.Length;
        }
    }

    public override long Position {
        get {
            Interlocked.MemoryBarrierProcessWide();
            return _stream.Position;
        }
        set {
            lock (_stream) {
                _stream.Position = value;
            }
        }
    }
    public override void Flush() {
        ThrowCantWrite();
    }

    public override int Read(byte[] buffer, int offset, int count) {
        return Read(buffer.AsSpan(offset, count));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override int Read(Span<byte> buffer) {
        return Read(buffer, default);
    }

    private int Read(Span<byte> buffer, CancellationToken ct) {
        int read;
        lock (_stream) {
            // attempt to read from present buffer
            read = _stream.Read(buffer);
        }

        if (read == buffer.Length || HasReceivedEndOfMessage) {
            return read;
        }

        while (true) {
            var result = Receive(ct);
            int inc;
            lock (_stream) {
                inc = _stream.Read(buffer.Slice(read));
            }

            Debug.Assert(inc == result.Count);
            read += inc;

            if (result.EndOfMessage) {
                break;
            }
        }

        return read;
    }

    public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken ct) {
        return ReadAsync(buffer.AsMemory(offset, count), ct).AsTask();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken ct = default) {
        return ct.IsCancellationRequested ? new(0) : ReadInternalAsync(buffer, ct);
    }

    private ValueTask<int> ReadInternalAsync(Memory<byte> buffer, CancellationToken ct) {
        int read;
        lock (_stream) {
            // attempt to read from present buffer
            read = _stream.Read(buffer.Span);
        }

        if (read == buffer.Length || HasReceivedEndOfMessage) {
            return new(read);
        }

        return new(ReadFromChannelAsync(buffer, read, ct));
    }

    private async Task<int> ReadFromChannelAsync(Memory<byte> buffer, int read, CancellationToken ct) {
        while (true) {
            var result = await ReceiveAsync(ct).Inv();
            int inc;
            lock (_stream) {
                inc = _stream.Read(buffer.Span.Slice(read));
            }
            Debug.Assert(inc == result.Count);
            read += inc;

            if (result.EndOfMessage) {
                break;
            }
        }

        return read;
    }

    public override int ReadByte() {
        Span<byte> buffer = stackalloc byte[1];
        int read = Read(buffer);
        return read == 0 ? -1 : buffer[0];
    }

    public override long Seek(long offset, SeekOrigin origin) {
        lock (_stream) {
            return _stream.Seek(offset, origin);
        }
    }

    public override void SetLength(long value) {
        lock (_stream) {
            _stream.SetLength(value);
        }
    }

    public override void Write(byte[] buffer, int offset, int count) {
        ThrowCantWrite();
    }


#endregion

    [DoesNotReturn]
    private static void ThrowCantWrite() {
        throw new NotSupportedException("The stream does not support writing");
    }
}
