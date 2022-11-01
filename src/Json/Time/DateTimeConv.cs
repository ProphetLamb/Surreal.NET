using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace SurrealDB.Json.Time;

public sealed class DateTimeConv : JsonConverter<DateTime> {
    public override DateTime Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) {
        DateTime dt = reader.TokenType switch {
            JsonTokenType.Null or JsonTokenType.None => default,
            JsonTokenType.String or JsonTokenType.PropertyName => Parse(reader.GetString()),
            JsonTokenType.Number => DateTimeOffset.FromUnixTimeSeconds(reader.GetInt64()).UtcDateTime,
            _ => ThrowJsonTokenTypeInvalid()
        };

        return dt;
    }

    public override DateTime ReadAsPropertyName(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) {
        return Read(ref reader, typeToConvert, options);
    }

    public override void Write(Utf8JsonWriter writer, DateTime value, JsonSerializerOptions options) {
        writer.WriteStringValue(ToString(value));
    }

    public override void WriteAsPropertyName(Utf8JsonWriter writer, DateTime value, JsonSerializerOptions options) {
        writer.WritePropertyName(ToString(value));
    }

    public static DateTime Parse(string? s) {
        return DateTimeOffsetConv.Parse(s).DateTime;
    }

    public static bool TryParse(string? s, out DateTime value) {
        if (DateTimeOffsetConv.TryParse(s, out var v)) {
            value = v.DateTime;
            return true;
        }

        value = default;
        return false;
    }

    public static string ToString(in DateTime value) {
        return value.ToString("O");
    }


    [DoesNotReturn, DebuggerStepThrough, MethodImpl(MethodImplOptions.NoInlining)]
    private static DateTime ThrowJsonTokenTypeInvalid() {
        throw new JsonException("Cannot deserialize a non numeric non string token as a DateTime.");
    }
}
