using System.Diagnostics.CodeAnalysis;
using System.Text;

namespace SurrealDB.Common;

internal static class StringHelper {
    public static char End(this string str, int offset = 1) {
        return str[str.Length - offset];
    }
    public static char End(this ReadOnlySpan<char> str, int offset = 1) {
        return str[str.Length - offset];
    }

    public static bool IsEmpty([NotNullWhen(false)] this string? str) {
        return String.IsNullOrEmpty(str);
    }

    public static string Concat(ReadOnlySpan<char> p0, ReadOnlySpan<char> p1, ReadOnlySpan<char> p2 = default, ReadOnlySpan<char> p3 = default) {
#if NET5_0_OR_GREATER || NETCOREAPP3_0_OR_GREATER
        return String.Concat(p0, p1, p2, p3);
#else
        int cap = p0.Length + p1.Length + p2.Length + p3.Length;
        ValueStringBuilder sb = cap <= 512 ? new(stackalloc char[cap]) : new(cap);
        sb.Append(p0);
        sb.Append(p1);
        sb.Append(p2);
        sb.Append(p3);
        return sb.ToString();
#endif
    }
}
