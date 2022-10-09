namespace SurrealDB.Models.Result;

public readonly record struct OkResult(TimeSpan Time, string Status, ResultValue Value);
