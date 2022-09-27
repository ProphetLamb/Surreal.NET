using System.Net;

using SurrealDB.Common;

namespace SurrealDB.Configuration;

/// <summary>
///     Component that configures a <see cref="Config" />.
/// </summary>
/// <remarks>
///     A <see cref="IConfigBuilder" /> may not be directly instantiated in user code!
/// </remarks>
public interface IConfigBuilder {
    /// <summary>
    ///     Returns the <see cref="IConfigBuilder" /> that seeded this instance
    /// </summary>
    public IConfigBuilder? Parent { get; }

    /// <summary>
    ///     Configures the <see cref="Config" /> with the current settings
    /// </summary>
    /// <exception cref="InvalidConfigException"> If the configuration of this instance if faulty </exception>
    public void Configure(ref Config config);
}

/// <summary>
///     Contains logic to build a <see cref="Config" /> in the form of <see cref="IConfigBuilder" /> components and extensions methods.
/// </summary>
public static class ConfigBuilder {
    /// <summary>
    ///     Begins configuration of a <see cref="Config" /> with fluent api.
    /// </summary>
    public static Basic Create() {
        return new(null);
    }

    /// <summary>
    ///     Returns the configured <see cref="Config" /> by applying the entire <see cref="IConfigBuilder" /> chain.
    /// </summary>
    /// <exception cref="InvalidConfigException"> If the configuration is faulty. </exception>
    public static Config Build(this IConfigBuilder? builder) {
        InvalidConfigException.ThrowIf(builder is null, "", "Empty configuration is not allowed.");

        Config config = default;

        Stack<IConfigBuilder> backlog = new();
        backlog.Push(builder);
        while (builder.Parent is not null) {
            backlog.Push(builder.Parent);
            builder = builder.Parent;
        }

        while (backlog.TryPop(out builder)) {
            builder.Configure(ref config);
        }

        config.MarkAsValidated();
        return config;
    }


    /// <inheritdoc cref="BasicAuth" />
    public static BasicAuth WithBasicAuth(this IConfigBuilder config, string? username = null, string? password = null) {
        return new(config) { Username = username, Password = password, };
    }

    /// <inheritdoc cref="JwtAuth" />
    public static JwtAuth WithJwtAuth(this IConfigBuilder config, string? token = null) {
        return new(config) { Token = token, };
    }

    /// <inheritdoc cref="UseRpc" />
    public static UseRpc WithRpc(this IConfigBuilder config, bool insecure = false) {
        return new(config) { Insecure = insecure, };
    }

    /// <inheritdoc cref="UseRest" />
    public static UseRest WithRest(this IConfigBuilder config, bool insecure = false) {
        return new(config) { Insecure = insecure, };
    }

    /// <summary>
    ///     Basic options, such as the remote, database and namespace of the <see cref="Config" />
    /// </summary>
    public sealed class Basic : IConfigBuilder {
        internal Basic(IConfigBuilder? parent) {
            Parent = parent;
        }

        /// <inheritdoc cref="Config.Endpoint" />
        public IPEndPoint? Endpoint { get; set; }

        /// <inheritdoc cref="Config.Database" />
        public string? Database { get; set; }

        /// <inheritdoc cref="Config.Namespace" />
        public string? Namespace { get; set; }

        /// <inheritdoc />
        public IConfigBuilder? Parent { get; }

        /// <inheritdoc />
        public void Configure(ref Config config) {
            InvalidConfigException.ThrowIfNull(Endpoint, "Remote cannot be null");
            config.Endpoint = Endpoint;
            config.Database = Database;
            config.Namespace = Namespace;
        }

        /// <inheritdoc cref="Config.Endpoint" />
        public Basic WithEndpoint(IPEndPoint endpoint) {
            Endpoint = endpoint;
            return this;
        }

        /// <inheritdoc cref="Config.Endpoint" />
        public Basic WithEndpoint(
            in ReadOnlySpan<char> endpoint,
            Func<IPEndPoint>? fallback = default) {
            if (fallback is null) {
                Endpoint = NetHelper.ParseEndpoint(endpoint);
                return this;
            }

            if (!NetHelper.TryParseEndpoint(endpoint, out IPEndPoint? ip)) {
                ip = fallback();
            }

            return WithEndpoint(ip);
        }


        /// <inheritdoc cref="Config.Endpoint" />
        public Basic WithAddress(IPAddress address) {
            if (Endpoint is null) {
                Endpoint = new(address, 0);
            } else {
                Endpoint.Address = address;
            }

            return this;
        }


        /// <inheritdoc cref="Config.Endpoint" />
        public Basic WithAddress(
            in ReadOnlySpan<char> address,
            Func<IPAddress>? fallback = default) {
            IPAddress? ip;
            if (fallback is null) {
                ip = IPAddress.Parse(address);
            } else if (!IPAddress.TryParse(address, out ip)) {
                ip = fallback();
            }

            return WithAddress(ip);
        }


        /// <inheritdoc cref="Config.Endpoint" />
        public Basic WithPort(in int port) {
            if (Endpoint is null) {
                Endpoint = new(IPAddress.Loopback, port);
            } else {
                Endpoint.Port = port;
            }

            return this;
        }

        /// <inheritdoc cref="Config.Database" />
        public Basic WithDatabase(string database) {
            Database = database;
            return this;
        }

        /// <inheritdoc cref="Config.Database" />
        public Basic WithDatabase(in ReadOnlySpan<char> database) {
            return WithDatabase(database.ToString());
        }

        /// <inheritdoc cref="Config.Namespace" />
        public Basic WithNamespace(string ns) {
            Namespace = ns;
            return this;
        }

        /// <inheritdoc cref="Config.Namespace" />
        public Basic WithNamespace(in ReadOnlySpan<char> ns) {
            return WithDatabase(ns.ToString());
        }
    }

    /// <summary>
    ///     Configures the <see cref="Config" /> with User and Password authentication
    /// </summary>
    /// <remarks>
    ///     Important:
    ///     Do not ever use this on a client sided application!
    ///     The password is stored in plaintext in the heap, and can be obtained by a 3rd party!
    /// </remarks>
    public sealed class BasicAuth : IConfigBuilder {
        internal BasicAuth(IConfigBuilder? parent) {
            Parent = parent;
        }

        /// <inheritdoc cref="Config.Username" />
        public string? Username { get; set; }

        /// <inheritdoc cref="Config.Password" />
        public string? Password { get; set; }

        /// <inheritdoc />
        public IConfigBuilder? Parent { get; }

        /// <inheritdoc />
        public void Configure(ref Config config) {
            InvalidConfigException.ThrowIfNullOrWhitespace(Username, "Username cannot be null or whitespace");
            config.Authentication = Auth.Basic;
            config.Username = Username;
            config.Password = Password;
        }


        public BasicAuth WithUser(string user) {
            Username = user;
            return this;
        }

        /// <inheritdoc cref="Config.Username" />
        public BasicAuth WithUser(ReadOnlySpan<char> user) {
            return WithUser(user.ToString());
        }

        /// <inheritdoc cref="Config.Password" />
        public BasicAuth WithPassword(string password) {
            Password = password;
            return this;
        }

        /// <inheritdoc cref="Config.Password" />
        public BasicAuth WithPassword(ReadOnlySpan<char> password) {
            return WithPassword(password.ToString());
        }
    }

    /// <summary>
    ///     Configures the <see cref="Config" /> with Json Web Token Authentication
    /// </summary>
    public sealed class JwtAuth : IConfigBuilder {
        internal JwtAuth(IConfigBuilder? parent) {
            Parent = parent;
        }

        /// <inheritdoc cref="Config.JsonWebToken" />
        public string? Token { get; set; }

        /// <inheritdoc />
        public IConfigBuilder? Parent { get; }

        /// <inheritdoc />
        public void Configure(ref Config config) {
            InvalidConfigException.ThrowIfNullOrWhitespace(Token, "Invalid Json Web Token");
            config.Authentication = Auth.JsonWebToken;
            config.JsonWebToken = Token;
        }

        /// <inheritdoc cref="Config.JsonWebToken" />
        public JwtAuth WithToken(string? jwt) {
            Token = jwt;
            return this;
        }
    }

    /// <summary>
    ///     Configures the <see cref="Config" /> to use the rpc endpoint
    /// </summary>
    public sealed class UseRpc : IConfigBuilder {
        internal UseRpc(IConfigBuilder? parent) {
            Parent = parent;
        }

        /// <summary>
        ///     Optional: Determines whether to disable TLS for the RPC connection.
        ///     `false` uses the `wss` protocol, `true` uses `ws`.
        /// </summary>
        /// <remarks>
        ///     This is not recommended, and should only be used for testing purposes
        /// </remarks>
        public bool Insecure { get; set; }

        /// <inheritdoc cref="Config.RpcEndpoint" />
        public Uri? RpcEndpoint { get; set; }

        public IConfigBuilder? Parent { get; }


        public void Configure(ref Config config) {
            config.RpcEndpoint = RpcEndpoint ?? GetUri(config.Endpoint!, Insecure);
        }

        /// <inheritdoc cref="Config.RpcEndpoint" />
        public UseRpc WithRpcEndpoint(Uri url) {
            RpcEndpoint = url;
            return this;
        }

        /// <inheritdoc cref="Insecure" />
        public UseRpc WithRpcInsecure(bool insecure) {
            Insecure = insecure;
            return this;
        }

        /// <summary>
        ///     Creates the <see cref="Uri" /> used for the rpc websocket based on the specified <see cref="EndPoint" />.
        /// </summary>
        public static Uri GetUri(
            EndPoint endPoint,
            bool insecure = false) {
            return insecure
                ? new Uri($"ws://{endPoint}/rpc/")
                : new Uri($"wss://{endPoint}/rpc/");
        }
    }

    /// <summary>
    ///     Configures the <see cref="Config" /> to use the REST endpoint
    /// </summary>
    public sealed class UseRest : IConfigBuilder {
        internal UseRest(IConfigBuilder? parent) {
            Parent = parent;
        }

        /// <summary>
        ///     Optional: Determines whether to disable TLS for the RPC connection.
        ///     `false` uses the `wss` protocol, `true` uses `ws`.
        /// </summary>
        /// <remarks>
        ///     This is not recommended, and should only be used for testing purposes
        /// </remarks>
        public bool Insecure { get; set; }

        /// <inheritdoc cref="Config.RestEndpoint" />
        public Uri? RestEndpoint { get; set; }

        public IConfigBuilder? Parent { get; }


        public void Configure(ref Config config) {
            config.RestEndpoint = RestEndpoint ?? GetUri(config.Endpoint!, Insecure);
        }

        /// <inheritdoc cref="Config.RestEndpoint" />
        public UseRest WithRestEndpoint(Uri url) {
            RestEndpoint = url;
            return this;
        }

        /// <inheritdoc cref="Insecure" />
        public UseRest WithRestInsecure(bool insecure) {
            Insecure = insecure;
            return this;
        }

        /// <summary>
        ///     Creates the <see cref="Uri" /> used for the rpc websocket based on the specified <see cref="EndPoint" />.
        /// </summary>
        public static Uri GetUri(
            EndPoint endPoint,
            bool insecure = false) {
            return insecure
                ? new Uri($"http://{endPoint}/")
                : new Uri($"https://{endPoint}/");
        }
    }
}
