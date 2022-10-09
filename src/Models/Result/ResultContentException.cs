using System.Diagnostics.CodeAnalysis;
using System.Runtime.Serialization;

namespace SurrealDB.Models.Result;

public sealed class ResultContentException : Exception {
    public ResultContentException() {
    }

    public ResultContentException(SerializationInfo info, StreamingContext context) : base(info, context) {
    }

    public ResultContentException(string? message) : base(message) {
    }

    public ResultContentException(string? message, Exception? innerException) : base(message, innerException) {
    }

    [DoesNotReturn]
    public static ErrorResult ExpectedAnyError() => throw new ResultContentException($"The {nameof(Result.DriverResponse)} does not contain any {nameof(ErrorResult)}");

    [DoesNotReturn]
    public static OkResult ExpectedAnyOk() => throw new ResultContentException($"The {nameof(Result.DriverResponse)} does not contain any {nameof(OkResult)}");

    [DoesNotReturn]
    public static ErrorResult ExpectedSingleError() => throw new ResultContentException($"The {nameof(Result.DriverResponse)} does not contain exactly one {nameof(ErrorResult)}");

    [DoesNotReturn]
    public static OkResult ExpectedSingleOk() => throw new ResultContentException($"The {nameof(Result.DriverResponse)} does not contain exactly one {nameof(OkResult)}");
}
