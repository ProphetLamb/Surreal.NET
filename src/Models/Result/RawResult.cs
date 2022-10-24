using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Text.Json;

namespace SurrealDB.Models.Result;

public readonly record struct RawResult {
    private RawResult(Kind wrapped, int code, TimeSpan time, string? status, string? detail, JsonElement result) {
        Wrapped = wrapped;
        _code = code;
        _time = time;
        _status = status;
        _detail = detail;
        _result = result;
    }

    public Kind Wrapped { get; }

    private readonly int _code;
    private readonly TimeSpan _time;
    private readonly string? _status;
    private readonly string? _detail;
    private readonly JsonElement _result;

    public bool IsDefault => default == this;

    public static RawResult Unknown(JsonElement inner) => new(Kind.Unknown, default, default, default, default, inner);
    public static RawResult Ok(TimeSpan time, string status, JsonElement inner) => new(Kind.Ok, default, time, status, default, inner);
    public static RawResult Ok(TimeSpan time, JsonElement inner) => new(Kind.Ok, default, time, "OK", default, inner);
    public static RawResult Error(TimeSpan time, string status, string detail) => new(Kind.Error, default, time, status, detail, default);
    public static RawResult TransportError(int code, string status, string detail) => new(Kind.TransportError, code, default, status, detail, default);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryGetUnknown([NotNullWhen(true)] out JsonElement inner) {
        if (Wrapped == Kind.Unknown) {
            inner = _result;
            return true;
        }

        inner = default;
        return false;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryGetOk([NotNullWhen(true)] out OkResult ok) {
        if (Wrapped == Kind.Ok) {
            ok = new(_time, _status!, ResultValue.From(_result));
            return true;
        }

        ok = default;
        return false;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryGetError([NotNullWhen(true)] out ErrorResult err) {
        if (Wrapped == Kind.Error) {
            err = new(_time, _status!, _detail);
            return true;
        }

        err = default;
        return false;
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryGetAnyError([NotNullWhen(true)] out ErrorResult err) {
        if (Wrapped == Kind.Error || Wrapped == Kind.TransportError) {
            err = new(_time, _status!, _detail);
            return true;
        }

        err = default;
        return false;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryGetTransportError([NotNullWhen(true)] out TransportErrorResult err) {
        if (Wrapped == Kind.TransportError) {
            err = new(_code, _status!, _detail!);
            return true;
        }

        err = default;
        return false;
    }

    public enum Kind: byte {
        Unknown = 0,
        Ok,
        Error,
        TransportError,
    }
}
