using SurrealDB.Abstractions;
using SurrealDB.Configuration;
using SurrealDB.Models;
using SurrealDB.Ws;

namespace SurrealDB.Driver.Rpc;

public sealed partial class DatabaseRpc : IDatabase<RpcResponse>, IDisposable {
    private readonly WsClient _client = new();
    private Config _config;
    private bool _configured;

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
        await SetAuth(_config.Username, _config.Password, ct);

        // Use database
        await SetUse(_config.Database, _config.Namespace, ct);
    }

    public async Task Close(CancellationToken ct = default) {
        await _client.Close(ct);
    }

    /// <param name="ct"> </param>
    /// <inheritdoc />
    public async Task<RpcResponse> Info(CancellationToken ct) {
        return await _client.Send(new() { method = "info", }).ToSurreal();
    }

    /// <inheritdoc />
    public async Task<RpcResponse> Use(
        string? db,
        string? ns,
        CancellationToken ct = default) {
        WsClient.Response rsp = await _client.Send(new() { method = "use", parameters = new(){ db, ns } }, ct);

        if (rsp.error == default) {
            _config.Database = db;
            _config.Namespace = ns;
        }

        return rsp.ToSurreal();
    }

    /// <inheritdoc />
    public async Task<RpcResponse> Signup(
        object auth,
        CancellationToken ct = default) {
        return await _client.Send(new() { method = "signup", parameters = new() { auth, }, }, ct).ToSurreal();
    }

    /// <inheritdoc />
    public async Task<RpcResponse> Signin(
        object auth,
        CancellationToken ct = default) {
        WsClient.Response rsp = await _client.Send(new() { method = "signin", parameters = new() { auth, }, }, ct);

        // TODO: Update auth
        return rsp.ToSurreal();
    }

    /// <inheritdoc />
    public async Task<RpcResponse> Invalidate(CancellationToken ct = default) {
        return await _client.Send(new() { method = "invalidate", }, ct).ToSurreal();
    }

    /// <inheritdoc />
    public async Task<RpcResponse> Authenticate(
        string token,
        CancellationToken ct = default) {
        return await _client.Send(new() { method = "authenticate", parameters = new() { token, }, }, ct).ToSurreal();
    }

    /// <inheritdoc />
    public async Task<RpcResponse> Let(
        string key,
        object? value,
        CancellationToken ct = default) {
        return await _client.Send(new() { method = "let", parameters = new() { key, value, }, }, ct).ToSurreal();
    }

    /// <inheritdoc />
    public async Task<RpcResponse> Query(
        string sql,
        IReadOnlyDictionary<string, object?>? vars,
        CancellationToken ct = default) {
        WsClient.Request req = new() { method = "query", parameters = new() { sql, vars, }, };
        WsClient.Response rsp = await _client.Send(req, ct);
        return rsp.ToSurreal();
    }

    /// <inheritdoc />
    public async Task<RpcResponse> Select(
        Thing thing,
        CancellationToken ct = default) {
        WsClient.Request req = new() { method = "select", parameters = new() { thing, }, };
        WsClient.Response rsp = await _client.Send(req, ct);
        return rsp.ToSurreal();
    }

    /// <inheritdoc />
    public async Task<RpcResponse> Create(
        Thing thing,
        object data,
        CancellationToken ct = default) {
        WsClient.Request req = new() { method = "create", async = true, parameters = new() { thing, data, }, };
        WsClient.Response rsp = await _client.Send(req, ct);
        return rsp.ToSurreal();
    }

    /// <inheritdoc />
    public async Task<RpcResponse> Update(
        Thing thing,
        object data,
        CancellationToken ct = default) {
        return await _client.Send(new() { method = "update", parameters = new() { thing, data, }, }, ct).ToSurreal();
    }

    /// <inheritdoc />
    public async Task<RpcResponse> Change(
        Thing thing,
        object data,
        CancellationToken ct = default) {
        return await _client.Send(new() { method = "change", parameters = new() { thing, data, }, }, ct).ToSurreal();
    }

    /// <inheritdoc />
    public async Task<RpcResponse> Modify(
        Thing thing,
        object data,
        CancellationToken ct = default) {
        return await _client.Send(new() { method = "modify", parameters = new() { thing, data, }, }, ct).ToSurreal();
    }

    /// <inheritdoc />
    public async Task<RpcResponse> Delete(
        Thing thing,
        CancellationToken ct = default) {
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

    private async Task SetAuth(
        string? user,
        string? pass,
        CancellationToken ct) {
        // TODO: Support jwt auth
        _config.Username = user;
        _config.Password = pass;
        await Signin(new{ user = user, pass = pass, }, ct);
    }

    public void Dispose() {
        _client.Dispose();
    }
}
