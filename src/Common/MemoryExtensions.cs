namespace SurrealDB.Common;

public static class MemoryExtensions {
    public static ReadOnlySpan<T> SliceToMin<T>(in this ReadOnlySpan<T> span, int length) {
        return span.Length <= length ? span : span.Slice(0, length);
    }
}
