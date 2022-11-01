using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;

using SurrealDB.Common;
using SurrealDB.Json;

namespace SurrealDB.Models;

public static class ThingExtensions {
    /// <summary>Creates a <see cref="Thing"/> from a table and key.</summary>
    public static Thing ToThing<T>(in this (string Table, T Key) thing) {
        return Thing.From(thing.Table, thing.Key);
    }

    /// <summary>Escapes the thing into one or two URI path components.</summary>
    /// <remarks>The first component is the <see cref="Thing.Table"/>. If <see cref="Thing.HasKey"/>,
    /// then the second component is the <see cref="Thing.Key"/>.</remarks>
    public static string ToUri(in this Thing thing) {
        var (table, key) = thing;
        return (!table.IsEmpty, thing.HasKey) switch {
            (true, true) => StringHelper.Concat(table, "/", UriEscapeKey(key)),
            (true, false) => table.ToString(),
            (false, true) => StringHelper.Concat("/", UriEscapeKey(key)),
            (false, false) => thing.TableAndKey ?? ""
        };
    }

    private static string UriEscapeKey(ReadOnlySpan<char> key) {
        return Uri.EscapeDataString(ThingHelper.IsEscaped(key) ? ThingHelper.UnescapeKey(key) : key.ToString());
    }
}

internal static class ThingHelper {
    public const char SEPARATOR = ':';
    public const char PREFIX = '⟨';
    public const char SUFFIX = '⟩';
    public const string ESC_SUFFIX = @"\⟩";

    public static string SerializeKey<T>(in T key) {
        if (IsStringableType(in key)) {
            // the ToString method is cheap
            var text = key.ToString()!;
            return RequiresEscape(text) ? EscapeKey(text) : text;
        }

        var json = JsonSerializer.Serialize(key, SerializerOptions.Shared);
        // string literals may require escaping
        if (TryGetJsonString(json, out ReadOnlySpan<char> literal)) {
            return RequiresEscape(literal) ? EscapeKey(literal) : literal.ToString();
        }
        // objects and arrays do not
        return json;
    }

    public static bool IsStringableType<T>([NotNullWhen(true)] in T value) {
        var t = typeof(T);
        if (value is not null && t == typeof(object)) {
            t = value.GetType();
        }
        // string and ROM are known to have a good ToString representation
        return t == typeof(string) || t == typeof(ReadOnlyMemory<char>);
    }

    public static bool IsEscaped(ReadOnlySpan<char> key) {
        return key.Length >= 2 && key[0] == PREFIX && key.End() == SUFFIX;
    }

    public static string EscapeKey(ReadOnlySpan<char> key) {
        Debug.Assert(key.Length > 0);
        int cap = key.Length + 2;
        ValueStringBuilder sb = cap <= 512 ? new(stackalloc char[cap]) : new(cap);
        sb.Append(PREFIX);
        for (int p = 0; p < key.Length; p++) {
            if (key[p] == SUFFIX) {
                return EscapeKeyInternal(key, p, ref sb);
            }
        }
        sb.Append(key);
        sb.Append(SUFFIX);
        return sb.ToString();
    }

    private static string EscapeKeyInternal(ReadOnlySpan<char> key, int pos, ref ValueStringBuilder sb) {
        sb.Append(key.Slice(0, pos));
        sb.Append(ESC_SUFFIX);
        pos += 1;
        while (pos < key.Length) {
            int cur = pos;
            while (cur < key.Length) {
                if (key[cur] != SUFFIX) {
                    cur += 1;
                    continue;
                }
                break;
            }
            sb.Append(key.Slice(pos, cur - pos));
            sb.Append(ESC_SUFFIX);
            pos = cur + 1;
        }
        sb.Append(SUFFIX);
        return sb.ToString();
    }

    public static bool RequiresEscape(ReadOnlySpan<char> key) {
        return !IsEscaped(key) && ContainsComplexCharacters(key);
    }

    private static bool ContainsComplexCharacters(ReadOnlySpan<char> key) {
        foreach (char c in key) {
            if (IsComplexCharacter(c)) {
                return true;
            }
        }

        return false;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool IsComplexCharacter(char c) {
        // A complex character is one that is not 0..9, a..Z or _
        switch (c) {
            // case >= '*' and <= '/':
            case >= '0' and <= '9':
            case >= 'a' and <= 'z':
            case >= 'A' and <= 'Z':
            case '_':
                return false;
            default:
                return true;
        }
    }

    public static string UnescapeKey(ReadOnlySpan<char> key) {
        if (key.IsEmpty) {
            return "";
        }

        if (key[0] != PREFIX | key.End() != SUFFIX) {
            return key.ToString();
        }
        Debug.Assert(key.Length >= 2);

        int last = key.Length - 1;
        for (int p = 0; p < last; p++) {
            if (key.Slice(p, ESC_SUFFIX.Length) == ESC_SUFFIX) {
                // found escape sequence, unescape
                return UnescapeKeyInternal(key, p);
            }
        }

        // only strip prefix and suffix
        return key.Slice(1, key.Length - 2).ToString();
    }

    private static string UnescapeKeyInternal(ReadOnlySpan<char> key, int pos) {
        Debug.Assert(key.Slice(pos, ESC_SUFFIX.Length) == ESC_SUFFIX);
        ValueStringBuilder sb = key.Length <= 512 ? new(stackalloc char[key.Length]) : new(key.Length);

        sb.Append(key.Slice(0, pos));
        sb.Append(SUFFIX);

        int last = key.Length - 1;
        while (pos < key.Length) {
            int cur = pos;
            while (cur < last) {
                if (key.Slice(cur, ESC_SUFFIX.Length) != ESC_SUFFIX) {
                    cur += 1;
                    continue;
                }
                break;
            }

            if (cur == last && key.Slice(cur, ESC_SUFFIX.Length) != ESC_SUFFIX) {
                // loop exited at end and the end is not \⟩
                // append whatever end inclusive
                cur += ESC_SUFFIX.Length;
                sb.Append(key.Slice(pos, cur - pos));
            } else {
                sb.Append(key.Slice(pos, cur - pos));
                sb.Append(SUFFIX);
            }

            pos = cur;
        }

        return sb.ToString();
    }

    public static bool TryGetJsonString(ReadOnlySpan<char> text, out ReadOnlySpan<char> literal) {
        if (2 >= (uint)text.Length) {
            literal = text;
            return false;
        }
        char start = text[0], end = text.End();
        if (start == '"' & end == '"') {
            literal = text.Slice(1, text.Length - 2);
            return true; // string strip quotes
        }

        literal = text;
        return false;
    }
}
