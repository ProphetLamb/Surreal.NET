using System.Buffers;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace SurrealDB.Common;

/// <summary>Allows reading a stream efficiently</summary>
public struct BufferStreamReader : IDisposable {
    private Stream? _arbitraryStream;
    private MemoryStream? _memoryStream;
    private readonly int _bufferSize;
    private byte[]? _poolArray;

    private BufferStreamReader(Stream? arbitraryStream, MemoryStream? memoryStream, int bufferSize) {
        _arbitraryStream = arbitraryStream;
        _memoryStream = memoryStream;
        _bufferSize = bufferSize;
        _poolArray = null;
    }

    public Stream Stream => _memoryStream ?? _arbitraryStream!;

    public BufferStreamReader(Stream stream, int bufferSize) {
        ThrowArgIfStreamCantRead(stream);
        this = stream switch {
            // inhering from ms doesnt guarantee a good GetBuffer impl, such as RecyclableMemoryStream,
            // therefore we have to check the exact runtime type.
            MemoryStream ms when ms.GetType() == typeof(MemoryStream) => new(null, ms, bufferSize),
            _ => new(stream, null, bufferSize)
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
            _poolArray = buffer = ArrayPool<byte>.Shared.Rent(_bufferSize);
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
            _poolArray = buffer = ArrayPool<byte>.Shared.Rent(_bufferSize);
        }

        return stream.ReadToBuffer(buffer.AsSpan(0, Math.Min(buffer.Length, expectedSize)));
    }


    public void Dispose() {
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
