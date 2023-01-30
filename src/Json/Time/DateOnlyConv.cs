using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Serialization;

using Superpower;
using Superpower.Model;

namespace SurrealDB.Json.Time;

public sealed class DateOnlyConv : JsonConverter<DateOnly> {
    public override DateOnly Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) {
        return reader.TokenType switch {
            JsonTokenType.None or JsonTokenType.Null => default,
            JsonTokenType.String or JsonTokenType.PropertyName => Parse(reader.GetString()),
            _ => ThrowJsonTokenTypeInvalid()
        };
    }

    public override DateOnly ReadAsPropertyName(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) {
        return Read(ref reader, typeToConvert, options);
    }

    public override void Write(Utf8JsonWriter writer, DateOnly value, JsonSerializerOptions options) {
        writer.WriteStringValue(ToString(in value));
    }

    public override void WriteAsPropertyName(Utf8JsonWriter writer, DateOnly value, JsonSerializerOptions options) {
        writer.WritePropertyName(ToString(in value));
    }

    public static DateOnly Parse(string? s) {
        return TryParse(s, out DateOnly value) ? value : ThrowParseInvalid(s);
    }
    public static bool TryParse(string? s, out DateOnly value) {
        if (String.IsNullOrEmpty(s)) {
            value = default;
            return false;
        }
        Result<DateOnly> res = TimeParsers.IsoDate(new TextSpan(s));
        value = res.HasValue ? res.Value : default;
        return res.HasValue;
    }

    public static string ToString(in DateOnly value) {
        return $"{value.Year.ToString("D4")}-{value.Month.ToString("D2")}-{value.Day.ToString("D2")}";
    }

    [DoesNotReturn, DebuggerStepThrough, MethodImpl(MethodImplOptions.NoInlining)]
    private static DateOnly ThrowParseInvalid(string? s) {
        throw new ParseException($"Unable to parse DateOnly from `{s}`");
    }

    [DoesNotReturn, DebuggerStepThrough, MethodImpl(MethodImplOptions.NoInlining)]
    private static DateOnly ThrowJsonTokenTypeInvalid() {
        throw new JsonException("Cannot deserialize a non string token as a DateOnly.");
    }
}
