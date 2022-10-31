using SurrealDB.Abstractions;
using SurrealDB.Configuration;
using SurrealDB.Models;
using SurrealDB.Ws;

using DriverResponse = SurrealDB.Models.Result.DriverResponse;

namespace SurrealDB.Driver.Rpc;

public sealed class DatabaseRpc : IDatabase {
    private readonly WsClientSync _client = new();
    private Config _config;
    private bool _configured;

    /// <summary>
    ///     Indicates whether the client has valid connection details.
    /// </summary>
    public bool InvalidConnectionDetails =>
        _config.Namespace == null ||
        _config.Database == null ||
        ((_config.Username == null || _config.Password == null) &&
            _config.JsonWebToken == null);

    private void ThrowIfInvalidConnection() {
        if (!_configured || InvalidConnectionDetails) {
            throw new InvalidOperationException("The connection details is invalid.");
        }
    }

    public DatabaseRpc() {}

    public DatabaseRpc(in Config config) {
        _config = config;
    }

    /// <inheritdoc />
    public Config GetConfig() {
        return _config;
    }

    /// <inheritdoc />
    public async Task Open(Config config, CancellationToken ct = default) {
        _config = config;
        _configured = false;
        await Open(ct);
    }

    public async Task Open(CancellationToken ct = default) {
        if (_configured) {
            return;
        }
        _config.ThrowIfInvalid();

        _configured = true;

        // Open connection
        InvalidConfigException.ThrowIfNull(_config.RpcEndpoint);
        await _client.Open(_config.RpcEndpoint!, ct);

        // Authenticate
        if (_config.Username != null && _config.Password != null) {
            await Signin(new RootAuth(_config.Username, _config.Password), ct);
        } else if (_config.JsonWebToken != null)  {
            await Authenticate(_config.JsonWebToken, ct);
        }

        // Use database
        await SetUse(_config.Database, _config.Namespace, ct);
    }

    public async Task Close(CancellationToken ct = default) {
        _configured = false;
        await _client.Close(ct);
    }

    /// <param name="ct"> </param>
    /// <inheritdoc />
    public async Task<DriverResponse> Info(CancellationToken ct) {
        ThrowIfInvalidConnection();
        return await _client.Send(new() { method = "info", }, ct).ToSurreal();
    }

    /// <inheritdoc />
    public async Task<DriverResponse> Use(
        string? db,
        string? ns,
        CancellationToken ct = default) {
        ThrowIfInvalidConnection();
        WsClientSync.Response rsp = await _client.Send(new() { method = "use", parameters = new(){ db, ns } }, ct);

        if (rsp.error == default) {
            _config.Database = db;
            _config.Namespace = ns;
        }

        return rsp.ToSurreal();
    }

    /// <inheritdoc />
    public async Task<DriverResponse> Signup<TRequest>(
        TRequest auth,
        CancellationToken ct = default) where TRequest : IAuth {
        ThrowIfInvalidConnection();
        return await _client.Send(new() { method = "signup", parameters = new() { auth } }, ct).ToSurreal();
    }

    /// <inheritdoc />
    public async Task<DriverResponse> Signin<TRequest>(
        TRequest auth,
        CancellationToken ct = default) where TRequest : IAuth {
        ThrowIfInvalidConnection();
        WsClientSync.Response rsp = await _client.Send(new() { method = "signin", parameters = new() { auth } }, ct);

        return rsp.ToSurreal();
    }

    /// <inheritdoc />
    public async Task<DriverResponse> Invalidate(CancellationToken ct = default) {
        ThrowIfInvalidConnection();
        var response = await _client.Send(new() { method = "invalidate", }, ct).ToSurreal();

        if (!response.HasErrors) {
            RemoveAuth();
        }

        return response;
    }

    /// <inheritdoc />
    public async Task<DriverResponse> Authenticate(
        string token,
        CancellationToken ct = default) {
        ThrowIfInvalidConnection();
        var response = await _client.Send(new() { method = "authenticate", parameters = new() { token, }, }, ct).ToSurreal();

        if (!response.HasErrors) {
            SetAuth(token);
        }

        return response;
    }

    /// <inheritdoc />
    public async Task<DriverResponse> Let(
        string key,
        object? value,
        CancellationToken ct = default) {
        ThrowIfInvalidConnection();
        return await _client.Send(new() { method = "let", parameters = new() { key, value, }, }, ct).ToSurreal();
    }

    /// <inheritdoc />
    public async Task<DriverResponse> Query(
        string sql,
        IReadOnlyDictionary<string, object?>? vars,
        CancellationToken ct = default) {
        ThrowIfInvalidConnection();
        WsClientSync.Request req = new() { method = "query", parameters = new() { sql, vars, }, };
        WsClientSync.Response rsp = await _client.Send(req, ct);
        return rsp.ToSurreal();
    }

    /// <inheritdoc />
    public async Task<DriverResponse> Select(
        Thing thing,
        CancellationToken ct = default) {
        ThrowIfInvalidConnection();
        WsClientSync.Request req = new() { method = "select", parameters = new() { thing, }, };
        WsClientSync.Response rsp = await _client.Send(req, ct);
        return rsp.ToSurreal();
    }

    /// <inheritdoc />
    public async Task<DriverResponse> Create(
        Thing thing,
        object data,
        CancellationToken ct = default) {
        ThrowIfInvalidConnection();
        WsClientSync.Request req = new() { method = "create", async = true, parameters = new() { thing, data, }, };
        WsClientSync.Response rsp = await _client.Send(req, ct);
        return rsp.ToSurreal();
    }

    /// <inheritdoc />
    public async Task<DriverResponse> Update(
        Thing thing,
        object data,
        CancellationToken ct = default) {
        ThrowIfInvalidConnection();
        return await _client.Send(new() { method = "update", parameters = new() { thing, data, }, }, ct).ToSurreal();
    }

    /// <inheritdoc />
    public async Task<DriverResponse> Change(
        Thing thing,
        object data,
        CancellationToken ct = default) {
        ThrowIfInvalidConnection();
        return await _client.Send(new() { method = "change", parameters = new() { thing, data, }, }, ct).ToSurreal();
    }

    /// <inheritdoc />
    public async Task<DriverResponse> Modify(Thing thing, Patch[] patches, CancellationToken ct = default) {
        ThrowIfInvalidConnection();
        return await _client.Send(new() { method = "modify", parameters = new() { thing, patches, }, }, ct).ToSurreal();
    }

    /// <inheritdoc />
    public async Task<DriverResponse> Delete(Thing thing, CancellationToken ct = default) {
        ThrowIfInvalidConnection();
        return await _client.Send(new() { method = "delete", parameters = new() { thing, }, }, ct).ToSurreal();
    }

    private async Task SetUse(
        string? db,
        string? ns,
        CancellationToken ct) {
        _config.Database = db;
        _config.Namespace = ns;
        await Use(db, ns, ct);
    }

    private void SetAuth(
        string? user,
        string? pass) {
        RemoveAuth();

        _config.Username = user;
        _config.Password = pass;
    }

    private void SetAuth(
        string? jwt) {
        RemoveAuth();

        _config.JsonWebToken = jwt;
    }

    private void RemoveAuth() {
        _config.JsonWebToken = null;
        _config.Username = null;
        _config.Password = null;
    }

    public void Dispose() {
        _client.Dispose();
    }
}
