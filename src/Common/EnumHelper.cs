namespace SurrealDB.Common;

internal static class EnumHelper {
    public static T[] GetValues<T>()
        where T: struct, Enum {
#if NET6_0_OR_GREATER
        return Enum.GetValues<T>();
#else
        return (T[])Enum.GetValues(typeof(T));
#endif
    }
}
