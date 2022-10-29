using SurrealDB.Json;

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
[JsonConverter(typeof(ThingConverter))]
[DebuggerDisplay("{ToString(),nq}")]
public readonly record struct Thing {
    public const char CHAR_SEP = ':';
    public const char CHAR_PRE = '⟨';
    public const char CHAR_SUF = '⟩';

    private readonly int _split;
    private readonly string? _inner;

    public Thing(string? thing) {
        // thing of null string should equal `default` thing!
        _split = thing.AsSpan().IndexOf(CHAR_SEP) + 1;
        _inner = thing;
    }

    public Thing(
        string table,
        string key) {
        _split = table.Length;

        _inner = $"{table}{CHAR_SEP}{EscapeComplexCharactersIfRequired(key)}";
    }

    public Thing(
        string table,
        object? key) {
        _split = table.Length;

        if (key == null) {
            _inner = table;
            return;
        }

        string keyStr = JsonSerializer.Serialize(key, SerializerOptions.Shared);
        if (keyStr[0] == '"' && keyStr[keyStr.Length - 1] == '"') {
            // This key is being represented in JSON as a string (rather than a number, object or array)
            // We need to strip off the double quotes and check if it needs to be escaped
            keyStr = keyStr.Substring(1, keyStr.Length - 2);
            keyStr = EscapeComplexCharactersIfRequired(keyStr);
        }

        _inner = $"{table}{CHAR_SEP}{keyStr}";
    }

    /// <summary>
    /// Returns the Table part of the Thing
    /// </summary>
    public ReadOnlySpan<char> Table => GetKeyOffset(out int rec) ? _inner.AsSpan(0, rec - 1) : _inner.AsSpan();

    /// <summary>
    /// Returns the Key part of the Thing.
    /// </summary>
    public ReadOnlySpan<char> Key => GetKeyOffset(out int rec) ? _inner.AsSpan(rec) : default;

    /// <summary>
    /// If the <see cref="Key"/> is present returns the <see cref="Table"/> part including the separator; otherwise returns the <see cref="Table"/>.
    /// </summary>
    public ReadOnlySpan<char> TableAndSeparator => GetKeyOffset(out int rec) ? _inner.AsSpan(0, rec) : _inner;

    public bool HasKey => GetKeyOffset(out _);

    public int Length => _inner?.Length ?? 0;

    /// <summary>
    /// Indicates whether the <see cref="Key"/> is escaped. false if no <see cref="Key"/> is present.
    /// </summary>
    public bool IsKeyEscaped => GetKeyOffset(out _) ? IsStringEscaped(Key) : false;

    /// <summary>
    /// Returns the unescaped key, if the key is escaped
    /// </summary>
    private bool TryUnescapeKey(out ReadOnlySpan<char> key) {
        if (!GetKeyOffset(out int off) || !IsKeyEscaped) {
            key = default;
            return false;
        }

        int escOff = off + 1;
        key = _inner.AsSpan(escOff, _inner.Length - escOff - 1);
        return true;
    }

    private static bool IsStringEscaped(in ReadOnlySpan<char> key) {
        if (key.Length == 0) {
            return false;
        }

        return key[0] == CHAR_PRE && key[key.Length - 1] == CHAR_SUF;
    }

    private static string EscapeKey(in ReadOnlySpan<char> key) {
        return $"{CHAR_PRE}{key.ToString()}{CHAR_SUF}";
    }

    private static string EscapeComplexCharactersIfRequired(in ReadOnlySpan<char> key) {
        if (!ContainsComplexCharacters(in key) || IsStringEscaped(key)) {
            return key.ToString();
        }

        return EscapeKey(key);
    }

    private static bool ContainsComplexCharacters(in ReadOnlySpan<char> key) {
        for (int i = 0; i < key.Length; i++) {
            char ch = key[i];
            if (IsComplexCharacter(ch)) {
                return true;
            }
        }

        return false;
    }

    private static bool IsComplexCharacter(char c) {
        // A complex character is one that is not 0..9, a..Z or _
        switch (c) {
        case >= '0' and <= '9':
        case >= 'a' and <= 'z':
        case >= 'A' and <= 'Z':
        case '_':
            return false;
        default:
            return true;
        }
    }

    [Pure]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private bool GetKeyOffset(out int offset) {
        offset = _split;
        return offset > 0;
    }

    public static implicit operator Thing(in string? thing) {
        return new(thing);
    }

    // Double implicit operators can result in syntax problems, so we use the explicit operator instead.
    public static explicit operator string(in Thing thing) {
        return thing.ToString();
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

        if (!HasKey) {
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
        return _inner ?? "";
    }

    public sealed class ThingConverter : JsonConverter<Thing> {
        public override Thing Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) {
            return new(reader.GetString());
        }

        public override Thing ReadAsPropertyName(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) {
            return new(reader.GetString());
        }

        public override void Write(Utf8JsonWriter writer, Thing value, JsonSerializerOptions options) {
            writer.WriteStringValue(value.ToString());
        }

        public override void WriteAsPropertyName(Utf8JsonWriter writer, Thing value, JsonSerializerOptions options) {
            writer.WritePropertyName(value.ToString());
        }

    }
}
