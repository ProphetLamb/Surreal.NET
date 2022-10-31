using System.Diagnostics;

namespace SurrealDB.Common;

internal static class StreamExtensions {
    public static bool TryReadToBuffer(this MemoryStream memoryStream, int expectedSize, out ReadOnlyMemory<byte> read) {
        if (!memoryStream.TryGetBuffer(out var buffer)) {
            read = default;
            return false;
        }
        // negative size -> read to end
        expectedSize = expectedSize < 0 ? Int32.MaxValue : expectedSize;
        // fake a read call
        var pos = (int)memoryStream.Position;
        var cap = (int)memoryStream.Length;
        var len = Math.Min(expectedSize, cap - pos);
        memoryStream.Position += len;
        read = buffer.AsMemory(pos, len);
        return true;
    }

    public static bool TryReadToBuffer(this MemoryStream memoryStream, int expectedSize, out ReadOnlySpan<byte> read) {
        if (!memoryStream.TryGetBuffer(out var buffer)) {
            read = default;
            return false;
        }
        // negative size -> read to end
        expectedSize = expectedSize < 0 ? Int32.MaxValue : expectedSize;
        // fake a read call
        var pos = (int)memoryStream.Position;
        var cap = (int)memoryStream.Length;
        var len = Math.Min(expectedSize, cap - pos);
        memoryStream.Position += len;
        read = buffer.AsSpan(pos, len);
        return true;
    }

    public static async Task<ReadOnlyMemory<byte>> ReadToBufferAsync(this Stream stream, Memory<byte> buffer, CancellationToken ct) {
        int read = await stream.ReadAsync(buffer, ct);
        return buffer.Slice(0, read);
    }

    public static ReadOnlySpan<byte> ReadToBuffer(this Stream stream, Span<byte> buffer) {
        int read = stream.Read(buffer);
        return buffer.Slice(0, read);
    }
}
