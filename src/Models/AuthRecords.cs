
// ReSharper disable InconsistentNaming

namespace SurrealDB.Models;

public interface IAuth {}

public readonly record struct RootAuth(
    string user,
    string pass) : IAuth;

public readonly record struct NamespaceAuth(
    string user,
    string pass,
    string NS) : IAuth;

public readonly record struct DatabaseAuth(
    string user,
    string pass,
    string NS,
    string DB) : IAuth;

public readonly record struct ScopeAuth(
    string user,
    string pass,
    string NS,
    string DB,
    string SC) : IAuth;

public readonly record struct Token(
    string ID,
    string NS,
    string DB,
    string SC,
    DateTime exp,
    DateTime iat,
    DateTime nbf,
    string iss) : IAuth;
