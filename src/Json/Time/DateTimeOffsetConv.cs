using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Serialization;

using Superpower;
using Superpower.Model;

namespace SurrealDB.Json.Time;

public sealed class DateTimeOffsetConv : JsonConverter<DateTimeOffset> {
    public override DateTimeOffset Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) {
        DateTimeOffset dt = reader.TokenType switch {
            JsonTokenType.Null or JsonTokenType.None => default,
            JsonTokenType.String or JsonTokenType.PropertyName => Parse(reader.GetString()),
            JsonTokenType.Number => DateTimeOffset.FromUnixTimeSeconds(reader.GetInt64()),
            _ => ThrowJsonTokenInvalid()
        };

        return dt;
    }

    public override DateTimeOffset ReadAsPropertyName(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) {
        return Read(ref reader, typeToConvert, options);
    }

    public override void Write(Utf8JsonWriter writer, DateTimeOffset value, JsonSerializerOptions options) {
        writer.WriteStringValue(ToString(in value));
    }

    public override void WriteAsPropertyName(Utf8JsonWriter writer, DateTimeOffset value, JsonSerializerOptions options) {
        writer.WritePropertyName(ToString(in value));
    }

    public static DateTimeOffset Parse(string? s) {
        return TryParse(s, out DateTimeOffset value) ? value : ThrowParseInvalid(s);
    }

    public static bool TryParse(string? s, out DateTimeOffset value) {
        if (String.IsNullOrEmpty(s)) {
            value = default;
            return false;
        }
        Result<DateTimeOffset> res = TimeParsers.IsoDateTimeOffset(new TextSpan(s));
        value = res.HasValue ? res.Value : default;
        return res.HasValue;
    }

    public static string ToString(in DateTimeOffset value) {
        return value.ToString("O");
    }

    [DoesNotReturn, DebuggerStepThrough, MethodImpl(MethodImplOptions.NoInlining)]
    private static DateTimeOffset ThrowParseInvalid(string? s) {
        throw new ParseException($"Unable to parse DateTimeOffset from `{s}`");
    }

    [DoesNotReturn, DebuggerStepThrough, MethodImpl(MethodImplOptions.NoInlining)]
    private static DateTimeOffset ThrowJsonTokenInvalid() {
        throw new JsonException("Cannot deserialize a non numeric non string token as a DateTime.");
    }
}
