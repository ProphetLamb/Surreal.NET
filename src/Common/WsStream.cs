using System.Buffers;
using System.Diagnostics.CodeAnalysis;
using System.Net.WebSockets;

namespace SurrealDB.Common;

public sealed class WsStream : Stream {
    private readonly IDisposable _prefixOwner;
    /// <summary>
    /// The prefix is the memory already obtained to be consumed before queries the socket
    /// </summary>
    private readonly ReadOnlyMemory<byte> _prefix;
    private int _prefixConsumed;

    private readonly WebSocket _ws;
    public bool EndOfMessage { get; private set; } = false;

    public override bool CanRead => true;
    public override bool CanSeek => false;
    public override bool CanWrite => false;
    public override long Length => ThrowSeekDisallowed();
    public override long Position { get => ThrowSeekDisallowed(); set => ThrowSeekDisallowed(); }

    public WsStream(IDisposable prefixOwner, ReadOnlyMemory<byte> prefix, WebSocket ws) {
        _prefixOwner = prefixOwner;
        _prefix = prefix;
        _ws = ws;
    }

    public override void Flush() {
        // Readonly
    }

    public override int Read(byte[] buffer, int offset, int count) {
        return Read(buffer.AsSpan(offset, count));
    }

    /// <summary>
    /// Use <see cref="Read(Memory{byte})"/>, or <see cref="ReadAsync(Memory{byte},CancellationToken)"/> if possible.
    /// </summary>
    public override int Read(Span<byte> buffer) {
        int read = 0;
        // consume the prefix
        ReadOnlySpan<byte> pref = ConsumePrefixAsSpan(buffer.Length);
        if (!pref.IsEmpty) {
            pref.CopyTo(buffer);
            buffer = buffer.Slice(pref.Length);
            read += pref.Length;
        }

        if (buffer.IsEmpty) {
            return read;
        }

        using IMemoryOwner<byte> o = MemoryPool<byte>.Shared.Rent(buffer.Length);
        Memory<byte> m = o.Memory.Slice(0, buffer.Length);
        buffer.CopyTo(m.Span);
        return read + ReadSync(m);
    }

    /// <inheritdoc cref="Read(Span{byte})" />
    public int Read(Memory<byte> buffer) {
        int read = 0;
        // consume the prefix
        ReadOnlySpan<byte> pref = ConsumePrefixAsSpan(buffer.Length);
        if (!pref.IsEmpty) {
            pref.CopyTo(buffer.Span);
            buffer = buffer.Slice(pref.Length);
            read += pref.Length;
        }

        return read + ReadSync(buffer);
    }

    private int ReadSync(Memory<byte> buffer) {
        // This causes issues if the scheduler is exclusive.
        return ReadAsync(buffer).Result;
    }

    public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) {
        return ReadAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();
    }

    public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) {
        var pref = ConsumePrefix(buffer.Length);
        if (!pref.IsEmpty) {
            pref.CopyTo(buffer);
            return pref.Length;
        }

        int read = 0;
        if (!EndOfMessage) {
            ValueWebSocketReceiveResult rsp = await _ws.ReceiveAsync(buffer, cancellationToken);
            read = rsp.Count;

            EndOfMessage = rsp.EndOfMessage;
        }

        return read;
    }

    private ReadOnlySpan<byte> ConsumePrefixAsSpan(int length) {
        var prefix = ConsumePrefix(length);
        return prefix.Span;
    }

    private ReadOnlyMemory<byte> ConsumePrefix(int length) {
        int len = _prefix.Length;
        int con = _prefixConsumed;
        if (con == len) {
            return default;
        }
        int rem = len - con;
        int inc = Math.Min(rem, length);
        _prefixConsumed = con + inc;
        return _prefix.Slice(con, inc);
    }

    private void DisposePrefix() {
        _prefixConsumed = _prefix.Length;
        _prefixOwner.Dispose();
    }

    public override long Seek(long offset, SeekOrigin origin) {
        return ThrowSeekDisallowed();
    }

    public override void SetLength(long value) {
        ThrowWriteDisallowed();
    }

    public override void Write(byte[] buffer, int offset, int count) {
        ThrowWriteDisallowed();
    }

    protected override void Dispose(bool disposing) {
        base.Dispose(disposing);
        DisposePrefix();
    }

    public override async ValueTask DisposeAsync() {
        await base.DisposeAsync();
        DisposePrefix();
    }

    [DoesNotReturn]
    private static void ThrowWriteDisallowed() {
        throw new InvalidOperationException("Cannot write a readonly stream");
    }

    [DoesNotReturn]
    private static long ThrowSeekDisallowed() {
        throw new InvalidOperationException("Cannot seek in the stream");
    }
}
