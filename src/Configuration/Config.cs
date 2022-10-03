using System.Net;

namespace SurrealDB.Configuration;

public struct Config {
    /// <summary>
    ///     Flag indicating a validated config.
    /// </summary>
    public bool IsValidated { get; private set; }

    /// <summary>
    ///     Remote database server endpoint (address and port) to connect to.
    /// </summary>
    public IPEndPoint? Endpoint { get; set; }

    /// <summary>
    ///     Optional: Sets the selected Database for queries.
    /// </summary>
    public string? Database { get; set; }

    /// <summary>
    ///     Optional: Sets the selected Namespace for queries.
    /// </summary>
    public string? Namespace { get; set; }

    /// <summary>
    ///     Optional: Sets the desired authentication scope for for a user.
    /// </summary>
    public string? Scope { get; set; }

    /// <summary>
    ///     The authentication method to use.
    /// </summary>
    public AuthMethod Authentication { get; set; }

    /// <summary>
    ///     Database authentication username to use when connecting.
    /// </summary>
    public string? Username { get; set; }

    /// <summary>
    ///     Database authentication password to use when connecting.
    /// </summary>
    public string? Password { get; set; }

    /// <summary>
    ///     Database authentication Json Web Token to use when connecting.
    /// </summary>
    public string? JsonWebToken { get; set; }

    /// <summary>
    ///     Optional: Defines the RPC endpoint to use
    /// </summary>
    /// <remarks>
    ///     If not specified computed using <see cref="Endpoint" />.
    ///     This option can be used to override the computed value.
    /// </remarks>
    public Uri? RpcEndpoint { get; set; }

    /// <summary>
    ///     The <see cref="Uri" /> of the rest endpoint of the REST client
    /// </summary>
    /// <remarks>
    ///     If not specified computed using <see cref="Endpoint" />.
    ///     This option can be used to override the computed value.
    /// </remarks>
    public Uri? RestEndpoint { get; set; }

    /// <summary>
    ///     Begins configuration of a <see cref="Config" /> with fluent api.
    /// </summary>
    /// <returns> </returns>
    public static ConfigBuilder.Basic Create() {
        return ConfigBuilder.Create();
    }

    /// <summary>
    ///     Marks the configuration as validated.
    /// </summary>
    public void MarkAsValidated() {
        IsValidated = true;
    }

    public void ThrowIfInvalid() {
        if (!IsValidated) {
            throw new InvalidConfigException("The configuration is not marked as valid.");
        }
    }
}
