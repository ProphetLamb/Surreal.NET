using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Text.Json;

using SurrealDB.Json;

namespace SurrealDB.Models;

/// <summary>
///     The result of a successful query to the Surreal database.
/// </summary>
[DebuggerDisplay("{Inner,nq}")]
public readonly struct Result : IEquatable<Result>, IComparable<Result> {
    private readonly JsonElement _json;
    private readonly object? _sentinelOrValue;
    private readonly long _int64ValueField;

    public Result(
            JsonElement json,
            object? sentinelOrValue) {
        _json = json;
        _sentinelOrValue = sentinelOrValue;
        _int64ValueField = 0;
    }

    public Result(
            JsonElement json,
            object? sentinelOrValue,
            long int64ValueField) {
        _json = json;
        _sentinelOrValue = sentinelOrValue;
        _int64ValueField = int64ValueField;
    }

    public JsonElement Inner => _json;

    public T? GetObject<T>() {
        if (_json.ValueKind is JsonValueKind.Undefined or JsonValueKind.Null) {
            return default;
        }
        var obj = _json.Deserialize<T>(SerializerOptions.Shared);
        return obj;
    }

    public IEnumerable<T> GetArray<T>() {
        if (_json.ValueKind != JsonValueKind.Array || _json.GetArrayLength() <= 0) {
            yield break;
        }

        var en = _json.EnumerateArray();
        while (en.MoveNext()) {
            T? v = en.Current.Deserialize<T>(SerializerOptions.Shared);
            if (!EqualityComparer<T>.Default.Equals(default, v)) {
                yield return v!;
            }
        }
    }

    public bool TryGetValue([NotNullWhen(true)] out string? value) {
        bool isString = GetKind() == ResultKind.String;
        value = isString ? (string)_sentinelOrValue! : null;
        return isString;
    }

    public bool TryGetValue(out int value) {
        var isInt = TryGetValue(out long valueLong);
        value = (int)valueLong;
        return isInt;
    }

    public bool TryGetValue(out long value) {
        bool isInt = GetKind() == ResultKind.SignedInteger;
        value = isInt ? _int64ValueField : 0;
        return isInt;
    }

    public bool TryGetValue(out ulong value) {
        bool isInt = GetKind() == ResultKind.SignedInteger;
        long data = _int64ValueField;
        value = isInt ? Unsafe.As<long, ulong>(ref data) : 0;
        return isInt;
    }

    public bool TryGetValue(out float value) {
        var isFloat = TryGetValue(out double valueDouble);
        value = (float)valueDouble;
        return isFloat;
    }

    public bool TryGetValue(out double value) {
        bool isFloat = GetKind() == ResultKind.Float;
        long data = _int64ValueField;
        value = isFloat ? Unsafe.As<long, double>(ref data) : 0;
        return isFloat;
    }

    public bool TryGetValue(out bool value) {
        bool isBoolean = GetKind() == ResultKind.Boolean;
        value = isBoolean && _int64ValueField != FALSE_VALUE;
        return isBoolean;
    }

    // Below is the logic determining the type of the boxed value in the result.
    // The type is primarily determined by the presence of a sentinel.
    // Both strings and documents make use of the sentinel field as a value field,
    // In this case the valueField determines the type.
    private static readonly object s_arraySentinel = new();
    private static readonly object s_objectSentinel = new();
    private static readonly object s_noneSentinel = new();
    private static readonly object s_signedIntegerSentinel = new();
    private static readonly object s_unsignedIntegerSentinel = new();
    private static readonly object s_floatSentinel = new();
    private static readonly object s_booleanSentinel = new();

    private const long TRUE_VALUE = 1;
    private const long FALSE_VALUE = 0;

    public static Result From(in JsonElement json) {
        return json.ValueKind switch {
            JsonValueKind.Undefined => new(json, s_noneSentinel),
            JsonValueKind.Object =>  new(json, s_objectSentinel),
            JsonValueKind.Array => new(json, s_arraySentinel),
            JsonValueKind.String => new(json, json.GetString()),
            JsonValueKind.Number => FromNumber(json),
            JsonValueKind.True => new(json, s_booleanSentinel, TRUE_VALUE),
            JsonValueKind.False => new(json, s_booleanSentinel, FALSE_VALUE),
            JsonValueKind.Null => new(json, s_noneSentinel),
            _ => ThrowUnknownJsonValueKind(json),
        };
    }

    private static Result FromNumber(in JsonElement json) {
        if (json.TryGetInt64(out long signed)) {
            return new(json, s_signedIntegerSentinel, signed);
        }

        if (json.TryGetUInt64(out ulong unsigned)) {
            return new(json, s_unsignedIntegerSentinel, Unsafe.As<ulong, long>(ref unsigned));
        }

        if (json.TryGetDouble(out double dbl)) {
            return new(json, s_floatSentinel, Unsafe.As<double, long>(ref dbl));
        }

        return new(json, s_noneSentinel);
    }

    private ResultKind GetKind() {

        if (ReferenceEquals(s_objectSentinel, _sentinelOrValue)) {
            return ResultKind.Object;
        }

        if (ReferenceEquals(s_arraySentinel, _sentinelOrValue)) {
            return ResultKind.Array;
        }

        if (ReferenceEquals(s_noneSentinel, _sentinelOrValue)) {
            return ResultKind.None;
        }

        if (ReferenceEquals(s_signedIntegerSentinel, _sentinelOrValue)) {
            return ResultKind.SignedInteger;
        }

        if (ReferenceEquals(s_unsignedIntegerSentinel, _sentinelOrValue)) {
            return ResultKind.UnsignedInteger;
        }

        if (ReferenceEquals(s_floatSentinel, _sentinelOrValue)) {
            return ResultKind.Float;
        }

        if (ReferenceEquals(s_booleanSentinel, _sentinelOrValue)) {
            return ResultKind.Boolean;
        }

        if (_sentinelOrValue is string) {
            return ResultKind.String;
        }

        Debug.Assert(false); // Should not happen, but is not fatal; None covers all edge cases.
        return ResultKind.None;
    }

    [DoesNotReturn, DebuggerStepThrough,]
    private static Result ThrowUnknownJsonValueKind(JsonElement json) {
        throw new ArgumentOutOfRangeException(nameof(json), json.ValueKind, "Unknown value kind.");
    }

    // Below is the implementation for the comparison and equality logic,
    // as well as operator overloads and conversion logic for IConvertible.

    public bool Equals(in Result other) {
        // Fastest check for inequality, is via the value field.
        if (_int64ValueField != other._int64ValueField) {
            return false;
        }

        // More expensive check for the type of the boxed value.
        ResultKind kind = GetKind();

        // Most expensive check requires unboxing of the value.
        return kind == other.GetKind() && EqualsUnboxed(in other, in kind);
    }

    private bool EqualsUnboxed(
        in Result other,
        in ResultKind kind) {
        return kind switch {
            ResultKind.Object or ResultKind.None => EqualityComparer<JsonElement>.Default.Equals(
                _json,
                other._json
            ),
            ResultKind.String => string.Equals((string)_sentinelOrValue!, (string)other._sentinelOrValue!),
            // Due to the unsafe case we are still able to use the operator and do not need to cast to compare structs.
            _ => _int64ValueField == other._int64ValueField,
        };
    }

    // The struct is big, do not copy if not necessary!
    bool IEquatable<Result>.Equals(Result other) {
        return Equals(in other);
    }

    public override bool Equals(object? obj) {
        return obj is Result other && Equals(other);
    }

    public override int GetHashCode() {
        return HashCode.Combine(_json.ValueKind, _sentinelOrValue, _int64ValueField);
    }

    public static bool operator ==(
        in Result left,
        in Result right) {
        return left.Equals(in right);
    }

    public static bool operator !=(
        in Result left,
        in Result right) {
        return !left.Equals(in right);
    }


    public int CompareTo(in Result other) {
        ResultKind thisKind = GetKind();
        ResultKind otherKind = other.GetKind();

        long thisValue = _int64ValueField;
        long otherValue = other._int64ValueField;

        return (thisKind, otherKind) switch {
            (ResultKind.SignedInteger, ResultKind.SignedInteger) => thisValue.CompareTo(otherValue),
            (ResultKind.SignedInteger, ResultKind.UnsignedInteger) =>
                ((double)thisValue).CompareTo(Unsafe.As<long, ulong>(ref otherValue)),
            (ResultKind.SignedInteger, ResultKind.Float) =>
                ((double)thisValue).CompareTo(Unsafe.As<long, double>(ref otherValue)),

            (ResultKind.UnsignedInteger, ResultKind.SignedInteger) =>
                ((double)Unsafe.As<long, ulong>(ref thisValue)).CompareTo(otherValue),
            (ResultKind.UnsignedInteger, ResultKind.UnsignedInteger) =>
                Unsafe.As<long, ulong>(ref thisValue).CompareTo(Unsafe.As<long, ulong>(ref otherValue)),
            (ResultKind.UnsignedInteger, ResultKind.Float) =>
                ((double)Unsafe.As<long, ulong>(ref thisValue)).CompareTo(Unsafe.As<long, double>(ref otherValue)),

            (ResultKind.Float, ResultKind.SignedInteger) =>
                Unsafe.As<long, double>(ref thisValue).CompareTo(otherValue),
            (ResultKind.Float, ResultKind.UnsignedInteger) =>
                Unsafe.As<long, double>(ref thisValue).CompareTo(Unsafe.As<long, ulong>(ref otherValue)),
            (ResultKind.Float, ResultKind.Float) =>
                Unsafe.As<long, double>(ref thisValue).CompareTo(Unsafe.As<long, double>(ref otherValue)),

            _ => ThrowInvalidCompareTypes(),
        };
    }

    // The struct is big, do not copy if not necessary!
    int IComparable<Result>.CompareTo(Result other) {
        return CompareTo(in other);
    }

    public static bool operator <(
        in Result left,
        in Result right) {
        return left.CompareTo(in right) < 0;
    }

    public static bool operator <=(
        in Result left,
        in Result right) {
        return left.CompareTo(in right) <= 0;
    }

    public static bool operator >(
        in Result left,
        in Result right) {
        return left.CompareTo(in right) > 0;
    }

    public static bool operator >=(
        in Result left,
        in Result right) {
        return left.CompareTo(in right) >= 0;
    }


    public override string ToString() {
        return Inner.ToString();
    }

    [DoesNotReturn, DebuggerStepThrough,]
    private static int ThrowInvalidCompareTypes() {
        throw new InvalidOperationException("Cannot compare SurrealResult of different types, if one or more is not numeric..");
    }
}
