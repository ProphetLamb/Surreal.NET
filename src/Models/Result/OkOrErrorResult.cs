using System.Text.Json;

namespace SurrealDB.Models.Result;

public readonly record struct OkOrErrorResult(
    TimeSpan Time,
    string Status,
    string? Detail,
    JsonElement Result) {
    public bool IsDefault => default == this;

    public RawResult ToResult() => "OK".AsSpan().Equals(Status, StringComparison.OrdinalIgnoreCase)
        ? RawResult.Ok(Time, Status, Result)
        : RawResult.Error(Time, Status, Detail!);
}
