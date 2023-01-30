using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Serialization;

using Superpower;
using Superpower.Model;

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
        return TryParse(s, out DateTime value) ? value : ThrowParseInvalid(s);
    }

    public static bool TryParse(string? s, out DateTime value) {
        if (String.IsNullOrEmpty(s)) {
            value = default;
            return false;
        }
        Result<DateTime> res = TimeParsers.IsoDateTimeUtc(new TextSpan(s));
        value = res.HasValue ? res.Value : default;
        return res.HasValue;
    }

    public static string ToString(in DateTime value) {
        return value.ToString("O");
    }

    [DoesNotReturn, DebuggerStepThrough, MethodImpl(MethodImplOptions.NoInlining)]
    private static DateTime ThrowParseInvalid(string? s) {
        throw new ParseException($"Unable to parse DateTime from `{s}`");
    }


    [DoesNotReturn, DebuggerStepThrough, MethodImpl(MethodImplOptions.NoInlining)]
    private static DateTime ThrowJsonTokenTypeInvalid() {
        throw new JsonException("Cannot deserialize a non numeric non string token as a DateTime.");
    }
}
