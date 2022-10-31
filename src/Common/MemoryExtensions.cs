namespace SurrealDB.Common;

internal static class MemoryExtensions {
    public static Span<T> ClipLength<T>(in this Span<T> span, int length) {
        return span.Length <= length ? span : span.Slice(0, length);
    }
    public static ReadOnlySpan<T> ClipLength<T>(in this ReadOnlySpan<T> span, int length) {
        return span.Length <= length ? span : span.Slice(0, length);
    }
}
