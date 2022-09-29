using System.Diagnostics;
using System.Diagnostics.Contracts;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace SurrealDB.Models;

/// <summary>
///     Indicates a table or a specific record.
/// </summary>
/// <remarks>
///     `table_name:record_id`
/// </remarks>
[JsonConverter(typeof(Converter))]
[DebuggerDisplay("{Inner,nq}")]
public readonly record struct Thing {
    public const char CHAR_SEP = ':';
    public const char CHAR_PRE = '⟨';
    public const char CHAR_SUF = '⟩';

    private readonly int _split;

    public Thing(
            int split,
            string inner) {
        _split = split;
        Inner = inner;
    }

    /// <summary>
    /// Returns the underlying string.
    /// </summary>
    public string Inner { get; }

    /// <summary>
    /// Returns the Table part of the Thing
    /// </summary>
    public ReadOnlySpan<char> Table => Inner.AsSpan(0, _split);

    /// <summary>
    /// Returns the Key part of the Thing.
    /// </summary>
    public ReadOnlySpan<char> Key => GetKeyOffset(out int rec) ? Inner.AsSpan(rec) : default;

    /// <summary>
    /// If the <see cref="Key"/> is present returns the <see cref="Table"/> part including the separator; otherwise returns the <see cref="Table"/>.
    /// </summary>
    public ReadOnlySpan<char> TableAndSeparator => GetKeyOffset(out int rec) ? Inner.AsSpan(0, rec) : Inner;

    public bool HasKey => _split < Length;

    public int Length => Inner.Length;

    /// <summary>
    /// Indicates whether the <see cref="Key"/> is escaped. true if no <see cref="Key"/> is present.
    /// </summary>
    public bool IsKeyEscaped => GetKeyOffset(out int rec) ? Inner[rec] == CHAR_PRE && Inner[Inner.Length - 1] == CHAR_SUF : true;

    /// <summary>
    /// Returns the unescaped key, if tne key is escaped
    /// </summary>
    public bool TryUnescapeKey(out ReadOnlySpan<char> key) {
        if (!GetKeyOffset(out int off) || !IsKeyEscaped) {
            key = default;
            return false;
        }

        int escOff = off + 1;
        key = Inner.AsSpan(escOff, Inner.Length - escOff - 1);
        return true;
    }

    /// <summary>
    /// Escapes the <see cref="Thing"/> if not already <see cref="IsKeyEscaped"/>.
    /// </summary>
    /// <returns></returns>
    [Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Thing Escape() {
        return IsKeyEscaped ? this : new(_split, $"{TableAndSeparator.ToString()}{CHAR_PRE}{Key.ToString()}{CHAR_SUF}");
    }

    /// <summary>
    /// Uneescapes the <see cref="Thing"/> if not already <see cref="IsKeyEscaped"/>.
    /// </summary>
    /// <returns></returns>
    [Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Thing Unescape() {
        return TryUnescapeKey(out ReadOnlySpan<char> key) ? new(_split, $"{TableAndSeparator.ToString()}{key.ToString()}") : this;
    }

    [Pure]
    private bool GetKeyOffset(out int off) {
        off = _split + 1;
        return HasKey;
    }

    public static Thing From(string? thing) {
        if (string.IsNullOrEmpty(thing)) {
            return default;
        }

        int split = thing.IndexOf(CHAR_SEP);
        return new(split <= 0 ? thing.Length : split, thing);
    }

    public static Thing From(
        in ReadOnlySpan<char> table,
        in ReadOnlySpan<char> key) {
        return From($"{table.ToString()}:{key.ToString()}");
    }

    public Thing WithTable(in ReadOnlySpan<char> table) {
        int keyOffset = table.Length + 1;
        int chars = keyOffset + Key.Length;
        Span<char> builder = stackalloc char[chars];
        table.CopyTo(builder);
        builder[table.Length] = CHAR_SEP;
        Key.CopyTo(builder.Slice(keyOffset));
        return new(table.Length, builder.ToString());
    }

    public Thing WithKey(in ReadOnlySpan<char> key) {
        int keyOffset = Table.Length + 1;
        int chars = keyOffset + key.Length;
        Span<char> builder = stackalloc char[chars];
        Table.CopyTo(builder);
        builder[Table.Length] = ':';
        key.CopyTo(builder.Slice(keyOffset));
        return new(Table.Length, builder.ToString());
    }

    public static implicit operator Thing(in string? thing) {
        return From(thing);
    }

    // Double implicit operators can result in syntax problems, so we use the explicit operator instead.
    public static explicit operator string(in Thing thing) {
        return thing.Inner;
    }

    public string ToUri() {
        if (Length <= 0) {
            return "";
        }

        var len = Length;
        using ValueStringBuilder result = len > 512 ? new(len) : new(stackalloc char[len]);
        if (!Table.IsEmpty) {
            result.Append(Uri.EscapeDataString(Table.ToString()));
        }

        if (Key.IsEmpty) {
            return result.ToString();
        }

        if (!Table.IsEmpty) {
            result.Append('/');
        }

        if (!TryUnescapeKey(out ReadOnlySpan<char> key)) {
            key = Key;
        }
        result.Append(Uri.EscapeDataString(key.ToString()));

        return result.ToString();
    }

    public override string ToString() {
        return (string)Converter.EscapeComplexCharactersIfRequired(in this);
    }

    public sealed class Converter : JsonConverter<Thing> {
        public override Thing Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) {
            return reader.GetString();
        }

        public override Thing ReadAsPropertyName(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) {
            return reader.GetString();
        }

        public override void Write(Utf8JsonWriter writer, Thing value, JsonSerializerOptions options) {
            writer.WriteStringValue((string)EscapeComplexCharactersIfRequired(in value));
        }

        public override void WriteAsPropertyName(Utf8JsonWriter writer, Thing value, JsonSerializerOptions options) {
            writer.WritePropertyName((string)EscapeComplexCharactersIfRequired(in value));
        }

        internal static Thing EscapeComplexCharactersIfRequired(in Thing thing) {
            if (!ContainsComplexCharacters(in thing)) {
                return thing;
            }

            return thing.Escape();
        }

        internal static bool ContainsComplexCharacters(in Thing thing) {
            if (thing.IsKeyEscaped || !thing.GetKeyOffset(out int rec)) {
                // This Thing is not split
                return false;
            }
            ReadOnlySpan<char> text = (string)thing;
            int len = text.Length;

            for (int i = rec; i < len; i++) {
                char ch = text[i];
                if (!char.IsLetterOrDigit(ch) && ch != '_') {
                    return true;
                }
            }

            return false;
        }
    }
}
