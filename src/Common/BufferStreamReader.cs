using System.Buffers;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

using Microsoft.IO;

namespace SurrealDB.Common;

/// <summary>Allows reading a stream efficiently</summary>
public struct BufferStreamReader : IDisposable, IAsyncDisposable {
    public const int BUFFER_SIZE = 16 * 1024;
    private Stream? _arbitraryStream;
    private MemoryStream? _memoryStream;
    private byte[]? _poolArray;

    private BufferStreamReader(Stream? arbitraryStream, MemoryStream? memoryStream) {
        _arbitraryStream = arbitraryStream;
        _memoryStream = memoryStream;
        _poolArray = null;
    }

    public Stream Stream => _memoryStream ?? _arbitraryStream!;

    public BufferStreamReader(Stream stream) {
        ThrowArgIfStreamCantRead(stream);
        this = stream switch {
            RecyclableMemoryStream => new(stream, null), // TryGetBuffer is expensive!
            MemoryStream ms => new(null, ms),
            _ => new(stream, null)
        };
    }

    /// <summary>Reads up to <paramref name="expectedSize"/> bytes from the underlying <see cref="Stream"/>.</summary>
    /// <param name="expectedSize">The expected number of bytes to read</param>
    /// <param name="ct">The cancellation token</param>
    /// <returns>The context bound memory representing the bytes read.</returns>
    /// <remarks>The returned memory is invalid outside this instance. Do not reference the memory outside of the scope!</remarks>
    public ValueTask<ReadOnlyMemory<byte>> ReadAsync(int expectedSize, CancellationToken ct = default) {
        var memoryStream = _memoryStream;
        var stream = _arbitraryStream;
        ThrowIfNull(stream is null & memoryStream is null);

        if (memoryStream is not null) {
            if (memoryStream.TryReadToBuffer(expectedSize, out ReadOnlyMemory<byte> read)) {
                return new(read);
            }

            // unable to access the memory stream buffer
            // handle as regular stream
            stream = memoryStream;
        }

        Debug.Assert(stream is not null);
        // reserve the buffer
        var buffer = _poolArray;
        if (buffer is null) {
            _poolArray = buffer = ArrayPool<byte>.Shared.Rent(BUFFER_SIZE);
        }

        // negative buffer size -> read as much as possible
        expectedSize = expectedSize < 0 ? buffer.Length : expectedSize;

        return new(stream.ReadToBufferAsync(buffer.AsMemory(0, Math.Min(buffer.Length, expectedSize)), ct));
    }

    /// <inheritdoc cref="ReadAsync"/>
    public ReadOnlySpan<byte> Read(int expectedSize) {
        var memoryStream = _memoryStream;
        var stream = _arbitraryStream;
        ThrowIfNull(stream is null & memoryStream is null);

        if (memoryStream is not null) {
            if (memoryStream.TryReadToBuffer(expectedSize, out ReadOnlySpan<byte> read)) {
                return read;
            }

            // unable to access the memory stream buffer
            // handle as regular stream
            stream = memoryStream;
        }

        Debug.Assert(stream is not null);
        // reserve the buffer
        var buffer = _poolArray;
        if (buffer is null) {
            _poolArray = buffer = ArrayPool<byte>.Shared.Rent(BUFFER_SIZE);
        }

        return stream.ReadToBuffer(buffer.AsSpan(0, Math.Min(buffer.Length, expectedSize)));
    }


    public void Dispose() {
        _arbitraryStream?.Dispose();
        _arbitraryStream = null;
        _memoryStream?.Dispose();
        _memoryStream = null;

        var poolArray = _poolArray;
        _poolArray = null;
        if (poolArray is not null) {
            ArrayPool<byte>.Shared.Return(poolArray);
        }
    }

    public async ValueTask DisposeAsync() {
        var arbitraryStream = _arbitraryStream;
        _arbitraryStream = null;
        if (arbitraryStream is not null) {
            await arbitraryStream.DisposeAsync().Inv();
        }

        var memoryStream = _memoryStream;
        _memoryStream = null;
        if (memoryStream is not null) {
            await memoryStream.DisposeAsync().Inv();
        }

        var poolArray = _poolArray;
        _poolArray = null;
        if (poolArray is not null) {
            ArrayPool<byte>.Shared.Return(poolArray);
        }
    }

    private static void ThrowIfNull([DoesNotReturnIf(true)] bool isNull, [CallerArgumentExpression(nameof(isNull))] string expression = "") {
        if (isNull) {
            throw new InvalidOperationException($"The expression cannot be null. `{expression}`");
        }
    }

    private static void ThrowArgIfStreamCantRead(Stream stream, [CallerArgumentExpression(nameof(stream))] string argName = "") {
        if (!stream.CanRead) {
            throw new ArgumentException("The stream must be readable", argName);
        }
    }
}
