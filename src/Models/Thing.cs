using SurrealDB.Json;

using System.Diagnostics;
using System.Diagnostics.Contracts;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

using SurrealDB.Common;

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
    private readonly int _split;
    private readonly string? _inner;

    private Thing(int split, string? inner) {
        _split = split;
        _inner = inner;
    }

    /// <summary>
    /// Creates a <see cref="Thing"/> from a table `foo`, or a table key tuple `foo:bar`.
    /// </summary>
    /// <param name="thing">The string representation of the thing</param>
    public Thing(string? thing) {
        // thing of null string equals `default` thing!
        this = new(thing.AsSpan().IndexOf(ThingHelper.SEPARATOR) + 1, thing);
    }

    /// <summary>Creates a <see cref="Thing"/> from a table and key.</summary>
    /// <param name="table">The table.</param>
    /// <param name="key">The key.</param>
    /// <remarks>The key is omitted if null or empty json.</remarks>
    public Thing(ReadOnlySpan<char> table, ReadOnlySpan<char> key) {
        int split = table.Length + 1;
        int cap = split + key.Length;
        ValueStringBuilder sb = cap <= 512 ? new(stackalloc char[cap]) : new(cap);
        sb.Append(table);
        sb.Append(ThingHelper.SEPARATOR);
        sb.Append(key);
        _split = split;
        _inner = sb.ToString();
    }

    /// <summary>Creates a <see cref="Thing"/> from a table and key.</summary>
    /// <param name="table">The table.</param>
    /// <param name="key">The key struct.</param>
    /// <remarks>The key is omitted if null or empty json.</remarks>
    public static Thing From<T>(ReadOnlySpan<char> table, T key) {
        return new(table, ThingHelper.SerializeKey(key));
    }

    /// <summary>Returns the string representing the <see cref="Thing"/>.</summary>
    public string? TableAndKey => _inner;

    /// <summary>Returns the Table part of the Thing</summary>
    public ReadOnlySpan<char> Table => GetKeyOffset(out int rec) ? _inner.AsSpan(0, rec - 1) : _inner.AsSpan();

    /// <summary>Returns the Key part of the Thing.</summary>
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
    public bool IsKeyEscaped => GetKeyOffset(out int rec) && ThingHelper.IsEscaped(_inner.AsSpan(rec));

    [Pure]
    public void Deconstruct(out ReadOnlySpan<char> table, out ReadOnlySpan<char> key) {
        table = Table;
        key = Key;
    }

    public ReadOnlySpan<char>.Enumerator GetEnumerator() {
        return _inner.AsSpan().GetEnumerator();
    }

    [Pure]
    public override string ToString() {
        return _inner ?? "";
    }

    [Pure]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal bool GetKeyOffset(out int offset) {
        offset = _split;
        return _split > 0;
    }

    public static implicit operator Thing(in string? thing) {
        return new(thing);
    }

    public static implicit operator Thing((string Table, string Key) thing) {
        return From(thing.Table, thing.Key);
    }

    // Double implicit operators can result in syntax problems, so we use the explicit operator instead.
    public static explicit operator string(in Thing thing) {
        return thing.ToString();
    }

    public static explicit operator (string Table, string Key)(in Thing thing) {
        return (thing.Key.ToString(), thing.Table.ToString());
    }

    public sealed class ThingConverter : JsonConverter<Thing> {
        public override Thing Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) {
            return reader.GetString();
        }

        public override Thing ReadAsPropertyName(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) {
            return reader.GetString();
        }

        public override void Write(Utf8JsonWriter writer, Thing value, JsonSerializerOptions options) {
            writer.WriteStringValue(value.ToString());
        }

        public override void WriteAsPropertyName(Utf8JsonWriter writer, Thing value, JsonSerializerOptions options) {
            writer.WritePropertyName(value.ToString());
        }

    }
}
