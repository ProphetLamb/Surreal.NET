namespace SurrealDB.Models.Result;

public readonly record struct TransportErrorResult(int Code, string Status, string Detail);
