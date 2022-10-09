namespace SurrealDB.Configuration;

/// <summary>
///     Available authentication methods
/// </summary>
public enum AuthMethod : byte {
    None = 0,
    Basic,
    JsonWebToken,
}
