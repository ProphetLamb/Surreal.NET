using System.Collections;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Text.Json;

using SurrealDB.Common;
using SurrealDB.Json;

namespace SurrealDB.Models.Result;

/// <summary>
///     The value of a successful query to the Surreal database.
/// </summary>
[DebuggerDisplay("{Inner,nq}")]
public readonly struct ResultValue : IEquatable<ResultValue>, IComparable<ResultValue> {
    private readonly JsonElement _json;
    private readonly object? _sentinelOrValue;
    private readonly long _int64ValueField;

    public ResultValue(
            JsonElement json,
            object? sentinelOrValue) {
        _json = json;
        _sentinelOrValue = sentinelOrValue;
        _int64ValueField = 0;
    }

    public ResultValue(
            JsonElement json,
            object? sentinelOrValue,
            long int64ValueField) {
        _json = json;
        _sentinelOrValue = sentinelOrValue;
        _int64ValueField = int64ValueField;
    }

    public JsonElement Inner => _json;

    public bool IsNullOrUndefined => _json.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined ||
        (_json.ValueKind is JsonValueKind.Array && _json.GetArrayLength() > 0 && _json.EnumerateArray().All(e => e.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined));
    public bool IsEmpty => _json.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined || (_json.ValueKind is JsonValueKind.Array && _json.GetArrayLength() == 0);

    public T? AsObject<T>() {
        if (IsNullOrUndefined) {
            return default;
        }
        var obj = _json.Deserialize<T>(SerializerOptions.Shared);
        return obj;
    }

    public ArrayIterator<T> AsEnumerable<T>() => new(_json);

    public struct ArrayIterator<T> : IEnumerator<T?>, IReadOnlyCollection<T> {
        private readonly JsonElement _json;
        private JsonElement.ArrayEnumerator _en;
        private nint _state;

        internal ArrayIterator(JsonElement json) {
            _json = json;
            if (json.ValueKind == JsonValueKind.Array) {
                _en = json.EnumerateArray();
            } else {
                _en = default;
            }
            _state = -1;
        }

        public readonly bool IsArray => _json.ValueKind == JsonValueKind.Array;

        public readonly bool IsElementNull => _json.ValueKind is JsonValueKind.Undefined or JsonValueKind.Null;

        public readonly nint Position => IsArray ? _state : 2 - _state;

        public readonly int Count => IsElementNull ? 0 : IsArray ? _json.GetArrayLength() : 1;

        public readonly T? Current => _state switch {
            >= 0 => _en.Current.Deserialize<T>(SerializerOptions.Shared),
            -2 => _json.Deserialize<T>(SerializerOptions.Shared),
            _ => default
        };

        readonly object? IEnumerator.Current => Current;

        public bool MoveNext() {
            nint state = _state;
            if (IsArray) {
                bool next = _en.MoveNext();
                _state = state + (next ? 1 : 0);
                return next;
            } else {
                bool next = state == -1 && !IsElementNull;
                _state = state - (next ? 1 : 0);
                return next;
            }
        }

        public readonly ArrayIterator<T> GetEnumerator() => new(_json);

        IEnumerator<T> IEnumerable<T>.GetEnumerator() {
            return GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator() {
            return GetEnumerator();
        }

        public void Reset() {
            _en.Reset();
            _state = -1;
        }

        public void Dispose() {
            if (_state >= 0) {
                _en.Dispose();
                _en = default;
            }
            _state = -2;
        }
    }

    public bool TryGetValue([NotNullWhen(true)] out string? value) {
        bool isString = GetKind() == Kind.String;
        value = isString ? (string)_sentinelOrValue! : null;
        return isString;
    }

    public bool TryGetValue(out int value) {
        var isInt = TryGetValue(out long valueLong);
        value = (int)valueLong;
        return isInt;
    }

    public bool TryGetValue(out long value) {
        bool isInt = GetKind() == Kind.SignedInteger;
        value = isInt ? _int64ValueField : 0;
        return isInt;
    }

    public bool TryGetValue(out ulong value) {
        bool isInt = GetKind() == Kind.SignedInteger;
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
        bool isFloat = GetKind() == Kind.Float;
        long data = _int64ValueField;
        value = isFloat ? Unsafe.As<long, double>(ref data) : 0;
        return isFloat;
    }

    public bool TryGetValue(out bool value) {
        bool isBoolean = GetKind() == Kind.Boolean;
        value = isBoolean && _int64ValueField != FalseValue;
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

    private const long TrueValue = 1;
    private const long FalseValue = 0;

    public static ResultValue From(in JsonElement root) {
        // reduce array of one element to the single element [ $value ] -> $value,
        // alternatively carry whatever $root is
        JsonElement json = IntoSingleOrOriginal(root);
        return json.ValueKind switch {
            JsonValueKind.Undefined => new(json, s_noneSentinel),
            JsonValueKind.Object => new(json, s_objectSentinel),
            JsonValueKind.Array => new(json, s_arraySentinel),
            JsonValueKind.String => new(json, json.GetString()),
            JsonValueKind.Number => FromNumber(json),
            JsonValueKind.True => new(json, s_booleanSentinel, TrueValue),
            JsonValueKind.False => new(json, s_booleanSentinel, FalseValue),
            JsonValueKind.Null => new(json, s_noneSentinel),
            _ => ThrowUnknownJsonValueKind(json),
        };
    }

    private static unsafe JsonElement IntoSingleOrOriginal(JsonElement root) {
        if (root.ValueKind != JsonValueKind.Array || root.GetArrayLength() > 1) {
            return root;
        }

        static bool Selector(in JsonElement e) => e.ValueKind is not JsonValueKind.Null or JsonValueKind.Undefined;
        var filter = SequenceHelper.Filter(root.EnumerateArray(), (delegate*<in JsonElement, bool>)&Selector);
        return SequenceHelper.TrySingle(ref filter, out JsonElement json) ? json : root;
    }

    private static ResultValue FromNumber(in JsonElement json) {
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

    public Kind GetKind() {

        if (ReferenceEquals(s_objectSentinel, _sentinelOrValue)) {
            return Kind.Object;
        }

        if (ReferenceEquals(s_arraySentinel, _sentinelOrValue)) {
            return Kind.Array;
        }

        if (ReferenceEquals(s_noneSentinel, _sentinelOrValue)) {
            return Kind.None;
        }

        if (ReferenceEquals(s_signedIntegerSentinel, _sentinelOrValue)) {
            return Kind.SignedInteger;
        }

        if (ReferenceEquals(s_unsignedIntegerSentinel, _sentinelOrValue)) {
            return Kind.UnsignedInteger;
        }

        if (ReferenceEquals(s_floatSentinel, _sentinelOrValue)) {
            return Kind.Float;
        }

        if (ReferenceEquals(s_booleanSentinel, _sentinelOrValue)) {
            return Kind.Boolean;
        }

        if (_sentinelOrValue is string) {
            return Kind.String;
        }

        Debug.Assert(false); // Should not happen, but is not fatal; None covers all edge cases.
        return Kind.None;
    }

    [DoesNotReturn, DebuggerStepThrough, MethodImpl(MethodImplOptions.NoInlining)]
    private static ResultValue ThrowUnknownJsonValueKind(JsonElement json) {
        throw new ArgumentOutOfRangeException(nameof(json), json.ValueKind, "Unknown value kind.");
    }

    // Below is the implementation for the comparison and equality logic,
    // as well as operator overloads and conversion logic for IConvertible.

    public bool Equals(in ResultValue other) {
        // Fastest check for inequality, is via the value field.
        if (_int64ValueField != other._int64ValueField) {
            return false;
        }

        // More expensive check for the type of the boxed value.
        Kind kind = GetKind();

        // Most expensive check requires unboxing of the value.
        return kind == other.GetKind() && EqualsUnboxed(in other, in kind);
    }

    private bool EqualsUnboxed(
        in ResultValue other,
        in Kind kind) {
        return kind switch {
            Kind.Object or Kind.None => EqualityComparer<JsonElement>.Default.Equals(
                _json,
                other._json
            ),
            Kind.String => string.Equals((string)_sentinelOrValue!, (string)other._sentinelOrValue!),
            // Due to the unsafe case we are still able to use the operator and do not need to cast to compare structs.
            _ => _int64ValueField == other._int64ValueField,
        };
    }

    // The struct is big, do not copy if not necessary!
    bool IEquatable<ResultValue>.Equals(ResultValue other) {
        return Equals(in other);
    }

    public override bool Equals(object? obj) {
        return obj is ResultValue other && Equals(other);
    }

    public override int GetHashCode() {
        return HashCode.Combine(_json.ValueKind, _sentinelOrValue, _int64ValueField);
    }

    public static bool operator ==(
        in ResultValue left,
        in ResultValue right) {
        return left.Equals(in right);
    }

    public static bool operator !=(
        in ResultValue left,
        in ResultValue right) {
        return !left.Equals(in right);
    }


    public int CompareTo(in ResultValue other) {
        Kind thisKind = GetKind();
        Kind otherKind = other.GetKind();

        long thisValue = _int64ValueField;
        long otherValue = other._int64ValueField;

        return (thisKind, otherKind) switch {
            (Kind.SignedInteger, Kind.SignedInteger) => thisValue.CompareTo(otherValue),
            (Kind.SignedInteger, Kind.UnsignedInteger) =>
                ((double)thisValue).CompareTo(Unsafe.As<long, ulong>(ref otherValue)),
            (Kind.SignedInteger, Kind.Float) =>
                ((double)thisValue).CompareTo(Unsafe.As<long, double>(ref otherValue)),

            (Kind.UnsignedInteger, Kind.SignedInteger) =>
                ((double)Unsafe.As<long, ulong>(ref thisValue)).CompareTo(otherValue),
            (Kind.UnsignedInteger, Kind.UnsignedInteger) =>
                Unsafe.As<long, ulong>(ref thisValue).CompareTo(Unsafe.As<long, ulong>(ref otherValue)),
            (Kind.UnsignedInteger, Kind.Float) =>
                ((double)Unsafe.As<long, ulong>(ref thisValue)).CompareTo(Unsafe.As<long, double>(ref otherValue)),

            (Kind.Float, Kind.SignedInteger) =>
                Unsafe.As<long, double>(ref thisValue).CompareTo(otherValue),
            (Kind.Float, Kind.UnsignedInteger) =>
                Unsafe.As<long, double>(ref thisValue).CompareTo(Unsafe.As<long, ulong>(ref otherValue)),
            (Kind.Float, Kind.Float) =>
                Unsafe.As<long, double>(ref thisValue).CompareTo(Unsafe.As<long, double>(ref otherValue)),

            _ => ThrowInvalidCompareTypes(),
        };
    }

    // The struct is big, do not copy if not necessary!
    int IComparable<ResultValue>.CompareTo(ResultValue other) {
        return CompareTo(in other);
    }

    public static bool operator <(
        in ResultValue left,
        in ResultValue right) {
        return left.CompareTo(in right) < 0;
    }

    public static bool operator <=(
        in ResultValue left,
        in ResultValue right) {
        return left.CompareTo(in right) <= 0;
    }

    public static bool operator >(
        in ResultValue left,
        in ResultValue right) {
        return left.CompareTo(in right) > 0;
    }

    public static bool operator >=(
        in ResultValue left,
        in ResultValue right) {
        return left.CompareTo(in right) >= 0;
    }


    public override string ToString() {
        return Inner.ToString();
    }

    [DoesNotReturn, DebuggerStepThrough, MethodImpl(MethodImplOptions.NoInlining)]
    private static int ThrowInvalidCompareTypes() {
        throw new InvalidOperationException("Cannot compare SurrealResult of different types, if one or more is not numeric..");
    }

    public enum Kind : byte {
        Object,
        Array,
        None,
        String,
        SignedInteger,
        UnsignedInteger,
        Float,
        Boolean
    }
}
