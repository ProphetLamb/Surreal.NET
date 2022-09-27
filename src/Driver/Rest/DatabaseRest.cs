using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

using SurrealDB.Abstractions;
using SurrealDB.Json;
using SurrealDB.Models;

namespace SurrealDB.Driver.Rest;

public sealed partial class DatabaseRest : IDatabase<RestResponse>, IDisposable {
    private readonly HttpClient _client = new();
    private Configuration.Config _config;
    private bool _configured;

    public DatabaseRest() {}

    public DatabaseRest(in Configuration.Config config) {
        _config = config;
    }

    private static Task<RestResponse> CompletedOk => Task.FromResult(RestResponse.EmptyOk);

    private readonly Dictionary<string, object?> _vars = new();

    public void Dispose() {
        _client.Dispose();
    }

    public Configuration.Config GetConfig() {
        return _config;
    }

    public Task Open(Configuration.Config config, CancellationToken ct = default) {
        _config = config;
        _configured = false;
        return Open(ct);
    }

    public Task Open(CancellationToken ct = default) {
        if (_configured) {
            return Task.CompletedTask;
        }
        _config.ThrowIfInvalid();
        _configured = true;
        ConfigureClients();

        // Authentication
        _client.BaseAddress = _config.RestEndpoint;
        SetAuth(_config.Username, _config.Password);

        // Use database
        SetUse(_config.Database, _config.Namespace);

        // The warp package Surreal uses for HTTP will return a 405
        // on OSX if the `Accept` header is not set.
        _client.DefaultRequestHeaders.Add("Accept", new[] { "application/json" });

        return Task.CompletedTask;
    }

    public Task Close(CancellationToken ct = default) {
        return Task.CompletedTask;
    }

    /// <summary>
    ///     UNSUPPORTED FOR REST IMPLEMENTATION
    /// </summary>
    public Task<RestResponse> Info(CancellationToken ct = default) {
        return CompletedOk;
    }

    public Task<RestResponse> Use(
        string db,
        string ns,
        CancellationToken ct = default) {
        SetUse(db, ns);

        return CompletedOk;
    }

    public async Task<RestResponse> Signup(
        object auth,
        CancellationToken ct = default) {
        return await Signup(ToJsonContent(auth), ct);
    }

    public async Task<RestResponse> Signin(
        object auth,
        CancellationToken ct = default) {
        // SetAuth(auth.Username, auth.Password);
        HttpResponseMessage rsp = await _client.PostAsync("signin", ToJsonContent(auth), ct);
        return await rsp.ToSurreal();
    }

    public Task<RestResponse> Invalidate(CancellationToken ct = default) {
        SetUse(null, null);
        RemoveAuth();

        return CompletedOk;
    }

    public Task<RestResponse> Authenticate(
        string token,
        CancellationToken ct = default) {
        throw new NotSupportedException(); // TODO: Is it tho???
    }

    public Task<RestResponse> Let(
        string key,
        object? value,
        CancellationToken ct = default) {
        if (value is null) {
            _vars.Remove(key);
        } else {
            _vars[key] = ToJson(value);
        }

        return CompletedOk;
    }

    public async Task<RestResponse> Query(
        string sql,
        IReadOnlyDictionary<string, object?>? vars,
        CancellationToken ct = default) {
        string query = FormatVars(sql, vars);
        HttpContent content = ToContent(query);
        return await Query(content, ct);
    }

    public async Task<RestResponse> Select(
        Thing thing,
        CancellationToken ct = default) {
        HttpRequestMessage requestMessage = ToRequestMessage(HttpMethod.Get, BuildRequestUri(thing));
        HttpResponseMessage rsp = await _client.SendAsync(requestMessage, ct);
        return await rsp.ToSurreal();
    }

    public async Task<RestResponse> Create(
        Thing thing,
        object data,
        CancellationToken ct = default) {
        return await Create(thing, ToJsonContent(data), ct);
    }


    public async Task<RestResponse> Update(
        Thing thing,
        object data,
        CancellationToken ct = default) {
        return await Update(thing, ToJsonContent(data), ct);
    }

    public async Task<RestResponse> Change(
        Thing thing,
        object data,
        CancellationToken ct = default) {
        // Is this the most optimal way?
        string sql = "UPDATE $what MERGE $data RETURN AFTER";
        Dictionary<string, object?> vars = new() { ["what"] = thing, ["data"] = data, };
        return await Query(sql, vars, ct);
    }

    public async Task<RestResponse> Modify(
        Thing thing,
        object data,
        CancellationToken ct = default) {
        HttpRequestMessage req = ToRequestMessage(HttpMethod.Patch, BuildRequestUri(thing), ToJson(data));
        HttpResponseMessage rsp = await _client.SendAsync(req, ct);
        return await rsp.ToSurreal();
    }

    public async Task<RestResponse> Delete(
        Thing thing,
        CancellationToken ct = default) {
        HttpRequestMessage requestMessage = ToRequestMessage(HttpMethod.Delete, BuildRequestUri(thing));
        HttpResponseMessage rsp = await _client.SendAsync(requestMessage, ct);
        return await rsp.ToSurreal();
    }

    private void ConfigureClients() {
        _client.DefaultRequestHeaders.ConnectionClose = false;
    }

    private void SetAuth(
        string? user,
        string? pass) {
        // TODO: Support jwt auth
        _config.Username = user;
        _config.Password = pass;
        AuthenticationHeaderValue header = new(
            "Basic",
            Convert.ToBase64String(Encoding.UTF8.GetBytes($"{user}:{pass}"))
        );

        _client.DefaultRequestHeaders.Authorization = header;
    }

    private void RemoveAuth() {
        _config.Username = null;
        _config.Password = null;
        _client.DefaultRequestHeaders.Authorization = null;
    }

    private void SetUse(
        string? db,
        string? ns) {
        _config.Database = db;
        _config.Namespace = ns;

        _client.DefaultRequestHeaders.Remove("DB");
        _client.DefaultRequestHeaders.Add("DB", db);
        _client.DefaultRequestHeaders.Remove("NS");
        _client.DefaultRequestHeaders.Add("NS", ns);
    }

    /// <inheritdoc cref="Signup(Authentication, CancellationToken)" />
    public async Task<RestResponse> Signup(
        HttpContent auth,
        CancellationToken ct = default) {
        HttpResponseMessage rsp = await _client.PostAsync("signup", auth, ct);
        return await rsp.ToSurreal();
    }

    /// <inheritdoc cref="Query(string, IReadOnlyDictionary{string, object?}?, CancellationToken)" />
    public async Task<RestResponse> Query(
        HttpContent sql,
        CancellationToken ct = default) {
        HttpResponseMessage rsp = await _client.PostAsync("sql", sql, ct);
        return await rsp.ToSurreal();
    }

    public async Task<RestResponse> Create(
        Thing thing,
        HttpContent data,
        CancellationToken ct = default) {
        HttpResponseMessage rsp = await _client.PostAsync(BuildRequestUri(thing), data, ct);
        return await rsp.ToSurreal();
    }

    public async Task<RestResponse> Update(
        Thing thing,
        HttpContent data,
        CancellationToken ct = default) {
        HttpResponseMessage rsp = await _client.PutAsync(BuildRequestUri(thing), data, ct);
        return await rsp.ToSurreal();
    }

    private string FormatUrl(
        Thing src,
        IReadOnlyDictionary<string, object?>? addVars = null) {

        return FormatVars(src.ToUri(), addVars);
    }

    private string FormatVars(
        string src,
        IReadOnlyDictionary<string, object?>? vars = null) {
        if (!src.Contains('$')) {
            return src;
        }

        int varsCount = _vars.Count + (vars?.Count ?? 0);
        if (varsCount <= 0) {
            return src;
        }

        return FormatVarsSlow(src, vars);
    }

    private string FormatVarsSlow(
        string template,
        IReadOnlyDictionary<string, object?>? vars) {
        using ValueStringBuilder result = template.Length > 512 ? new(template.Length) : new(stackalloc char[template.Length]);
        int i = 0;
        while (i < template.Length) {
            if (template[i] != '$') {
                result.Append(template[i]);
                i++;
                continue;
            }

            int start = ++i;
            while (i < template.Length && char.IsLetterOrDigit(template[i])) {
                i++;
            }

            string varName = template[start..i];

            if (_vars.TryGetValue(varName, out object? varValue)) {
                result.Append(ToJson(varValue));
            } else if (vars?.TryGetValue(varName, out varValue) == true) {
                result.Append(ToJson(varValue));
            } else {
                result.Append(template.AsSpan(start, i - start));
            }
        }

        return result.ToString();
    }

    private string BuildRequestUri(Thing thing) {
        return $"key/{FormatUrl(thing)}";
    }

    private string ToJson<T>(T? v) {
        return JsonSerializer.Serialize(v, SerializerOptions.Shared);
    }

    private HttpContent ToJsonContent<T>(T? v) {
        return ToContent(ToJson(v));
    }

    private static HttpContent ToContent(string s = "") {
        StringContent content = new(s, Encoding.UTF8, "application/json");

        if (content.Headers.ContentType != null) {
            // The server can only handle 'Content-Type' with 'application/json', remove any further information from this header
            content.Headers.ContentType.CharSet = null;
        }

        return content;
    }

    private HttpRequestMessage ToRequestMessage(
        HttpMethod method,
        string requestUri,
        string content = "") {
        // SurrealDb must have a 'Content-Type' header defined,
        // but HttpClient does not allow default request headers to be set.
        // So we need to make PUT and DELETE requests with an empty request body, but with request headers
        return new HttpRequestMessage { Method = method, RequestUri = new Uri(requestUri, UriKind.Relative), Content = ToContent(content), };
    }
}
