using System.Diagnostics;

namespace SurrealDB.Models.Result;

/// <summary>
///     The result of a failed query to the Surreal database.
/// </summary>
[DebuggerDisplay("{Code,nq}: {Message,nq}")]
public readonly record struct ErrorResult(TimeSpan Time, string Status, string? Message);
