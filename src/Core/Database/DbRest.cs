﻿using System.Net.Http.Headers;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using Rustic;

namespace Surreal.Net.Database;

public sealed class DbRest : ISurrealDatabase<SurrealRestResponse>, IDisposable
{
    private readonly HttpClient _client = new();
    private SurrealConfig _config;

    private static Task<SurrealRestResponse> CompletedOk => Task.FromResult(SurrealRestResponse.EmptyOk);

    public Dictionary<string, string> UseVariables { get; } = new();

    private static IReadOnlyDictionary<string, object?> EmptyVars { get; } = new Dictionary<string, object?>(0);

    public JsonSerializerOptions SerializerOptions { get; } = new()
    {
        PropertyNamingPolicy = JsonLowerSnakeCaseNamingPolicy.Instance,
        PropertyNameCaseInsensitive = true,
        WriteIndented = false,
        AllowTrailingCommas = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        DictionaryKeyPolicy = JsonLowerSnakeCaseNamingPolicy.Instance,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    public SurrealConfig GetConfig() => _config;

    public Task Open(SurrealConfig config, CancellationToken ct = default)
    {
        config.ThrowIfInvalid();
        _config = config;

        ConfigureClients();

        // Authentication
        _client.BaseAddress = config.RestEndpoint;
        SetAuth(config.Username, config.Password);

        // Use database
        SetUse(config.Database, config.Namespace);

        return Task.CompletedTask;
    }

    private void ConfigureClients()
    {
        _client.DefaultRequestHeaders.ConnectionClose = false;
    }

    private void SetAuth(string? user, string? pass)
    {
        // TODO: Support jwt auth
        _config.Username = user;
        _config.Password = pass;
        AuthenticationHeaderValue header = new("Basic",
            Convert.ToBase64String(Encoding.UTF8.GetBytes($"{user}:{pass}")));
        _client.DefaultRequestHeaders.Authorization = header;
    }

    private void RemoveAuth()
    {
        _config.Username = null;
        _config.Password = null;
        _client.DefaultRequestHeaders.Authorization = null;
    }
    private void SetUse(string? db, string? ns)
    {
        _config.Database = db;
        _config.Namespace = ns;

        _client.DefaultRequestHeaders.Remove("DB");
        _client.DefaultRequestHeaders.Add("DB", db);
        _client.DefaultRequestHeaders.Remove("NS");
        _client.DefaultRequestHeaders.Add("NS", ns);
    }

    public Task Close(CancellationToken ct = default)
    {
        return Task.CompletedTask;
    }

    /// <summary>
    /// UNSUPPORTED FOR REST IMPLEMENTATION
    /// </summary>
    public Task<SurrealRestResponse> Info(CancellationToken ct = default)
    {
        return CompletedOk;
    }

    public Task<SurrealRestResponse> Use(string db, string ns, CancellationToken ct = default)
    {
        SetUse(db, ns);

        return CompletedOk;
    }

    public async Task<SurrealRestResponse> Signup(SurrealAuthentication auth, CancellationToken ct = default)
    {
        return await Signup(ToJsonContent(auth), ct);
    }

    /// <inheritdoc cref="Signup(SurrealAuthentication, CancellationToken)"/>
    public async Task<SurrealRestResponse> Signup(HttpContent auth, CancellationToken ct = default)
    {
        var rsp = await _client.PostAsync("signup", auth, ct);
        return await rsp.ToSurreal();
    }

    public async Task<SurrealRestResponse> Signin(SurrealAuthentication auth, CancellationToken ct = default)
    {
        SetAuth(auth.Username, auth.Password);
        var rsp = await _client.PostAsync("signin", ToJsonContent(auth), ct);
        return await rsp.ToSurreal();
    }

    public Task<SurrealRestResponse> Invalidate(CancellationToken ct = default)
    {
        SetUse(null, null);
        RemoveAuth();

        return CompletedOk;
    }

    public Task<SurrealRestResponse> Authenticate(string token, CancellationToken ct = default)
    {
        throw new NotSupportedException(); // TODO: Is it tho???
    }

    public Task<SurrealRestResponse> Let(string key, object? value, CancellationToken ct = default)
    {
        if (value is null)
        {
            UseVariables.Remove(key);
        }
        else
        {
            UseVariables[key] = ToJson(value);
        }
        return CompletedOk;
    }

    public async Task<SurrealRestResponse> Query(string sql, IReadOnlyDictionary<string, object?>? vars, CancellationToken ct = default)
    {
        string query = FormatVars(sql, vars);
        var content = ToContent(query);
        return await Query(content, ct);
    }

    /// <inheritdoc cref="Query(string, IReadOnlyDictionary{string, object?}?, CancellationToken)"/>
    public async Task<SurrealRestResponse> Query(HttpContent sql, CancellationToken ct = default)
    {
        var rsp = await _client.PostAsync("sql", sql, ct);
        return await rsp.ToSurreal();
    }

    public async Task<SurrealRestResponse> Select(SurrealThing thing, CancellationToken ct = default)
    {
        var requestMessage = ToRequestMessage(HttpMethod.Get, BuildRequestUri(thing));
        var rsp = await _client.SendAsync(requestMessage, ct);
        return await rsp.ToSurreal();
    }

    public async Task<SurrealRestResponse> Create(SurrealThing thing, object data, CancellationToken ct = default)
    {
        return await Create(thing, ToJsonContent(data), ct);
    }

    public async Task<SurrealRestResponse> Create(SurrealThing thing, HttpContent data, CancellationToken ct = default)
    {
        var rsp = await _client.PostAsync(BuildRequestUri(thing), data, ct);

        return await rsp.ToSurreal();
    }


    public async Task<SurrealRestResponse> Update(SurrealThing thing, object data, CancellationToken ct = default)
    {
        return await Update(thing, ToJsonContent(data), ct);
    }

    public async Task<SurrealRestResponse> Update(SurrealThing thing, HttpContent data, CancellationToken ct = default)
    {
        var rsp = await _client.PutAsync(BuildRequestUri(thing), data, ct);
        return await rsp.ToSurreal();
    }

    public async Task<SurrealRestResponse> Change(SurrealThing thing, object data, CancellationToken ct = default)
    {
        // Is this the most optimal way?
        string sql = "UPDATE $what MERGE $data RETURN AFTER";
        var vars = new Dictionary<string, object?>
        {
            ["what"] = thing.ToString(),
            ["data"] = data
        };
        return await Query(sql, vars, ct);
    }

    public async Task<SurrealRestResponse> Modify(SurrealThing thing, object data, CancellationToken ct = default)
    {
        var req = ToRequestMessage(HttpMethod.Patch, BuildRequestUri(thing), ToJson(data));
        var rsp = await _client.SendAsync(req, ct);
        return await rsp.ToSurreal();
    }

    public async Task<SurrealRestResponse> Delete(SurrealThing thing, CancellationToken ct = default)
    {
        var requestMessage = ToRequestMessage(HttpMethod.Delete, BuildRequestUri(thing));
        var rsp = await _client.SendAsync(requestMessage, ct);
        return await rsp.ToSurreal();
    }

    public void Dispose()
    {
        _client.Dispose();
    }

    private string FormatUrl(SurrealThing src, IReadOnlyDictionary<string, object?>? addVars = null)
    {
        using StrBuilder result = src.Length > 512 ? new(src.Length) : new(stackalloc char[src.Length]);
        if (!src.Table.IsEmpty)
            result.Append(Uri.EscapeDataString(src.Table.ToString()));
        if (!src.Table.IsEmpty && !src.Key.IsEmpty)
            result.Append('/');
        if (!src.Key.IsEmpty)
            result.Append(Uri.EscapeDataString(src.Key.ToString()));

        return FormatVars(result.ToString(), addVars);
    }

    private string FormatVars(string src, IReadOnlyDictionary<string, object?>? addVars = null)
    {
        if (!src.Contains('$'))
        {
            return src;
        }

        IReadOnlyDictionary<string, string>? vars = CombineVars(UseVariables, addVars ?? EmptyVars);

        if (vars is null || vars.Count == 0)
            return src;

        return FormatVarsSlow(src, vars);
    }

    private string FormatVarsSlow(string template, IReadOnlyDictionary<string, string>? vars)
    {
        using StrBuilder result = template.Length > 512 ? new(template.Length) : new(stackalloc char[template.Length]);
        int i = 0;
        while (i < template.Length)
        {
            if (template[i] != '$')
            {
                result.Append(template[i]);
                i++;
                continue;
            }

            int start = i;
            i++;
            while (i < template.Length && char.IsLetterOrDigit(template[i]))
            {
                i++;
            }
            string varName = template[start..i];

            if (UseVariables.TryGetValue(varName, out string? varValue))
            {
                result.Append(varValue);
            }
            else if (vars?.TryGetValue(varName, out varValue) == true)
            {
                result.Append(varValue);
            }
            else
            {
                result.Append(template.AsSpan(start, i - start));
            }
        }

        return result.ToString();
    }


    private IReadOnlyDictionary<string, string> CombineVars(IReadOnlyDictionary<string, string> a, IReadOnlyDictionary<string, object?> b)
    {
        Dictionary<string, string> result = new(a.Count + b.Count, StringComparer.OrdinalIgnoreCase);
        foreach (var (k, v) in a)
        {
            result[k] = v;
        }

        foreach (var (k, v) in b)
        {
            result[k] = ToJson(v);
        }

        return result;
    }

    private string BuildRequestUri(SurrealThing thing)
    {
        return $"key/{FormatUrl(thing)}";
    }

    private string ToJson<T>(T? v)
    {
        return JsonSerializer.Serialize(v, SerializerOptions);
    }

    private HttpContent ToJsonContent<T>(T? v)
    {
        return ToContent(ToJson(v));
    }

    private static HttpContent ToContent(string s = "")
    {
        var content = new StringContent(s, Encoding.UTF8, "application/json");

        if (content.Headers.ContentType != null)
        {
            // The server can only handle 'Content-Type' with 'application/json', remove any further information from this header
            content.Headers.ContentType.CharSet = null;
        }

        return content;
    }

    private HttpRequestMessage ToRequestMessage(HttpMethod method, string requestUri, string? content = "")
    {
        // SurrealDb must have a 'Content-Type' header defined,
        // but HttpClient does not allow default request headers to be set.
        // So we need to make PUT and DELETE requests with an empty request body, but with request headers
        return new HttpRequestMessage
        {
            Method = method,
            RequestUri = new Uri(requestUri, UriKind.Relative),
            Content = ToContent(content)
        };
    }
}