using System.Diagnostics;

namespace SurrealDB.Models;

/// <summary>
///     The result of a failed query to the Surreal database.
/// </summary>
[DebuggerDisplay("{Code,nq}: {Message,nq}")]
public readonly record struct Error(int Code, string? Message);
