using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Serialization;

using Microsoft.IO;

using SurrealDB.Common;
using SurrealDB.Json;

namespace SurrealDB.Ws;

/// <summary>
///     The client used to connect to the Surreal server via JSON RPC.
/// </summary>
public sealed class WsClientSync : IDisposable, IAsyncDisposable {
    private static readonly Lazy<RecyclableMemoryStreamManager> s_manager = new(static () => new());
    // Do not get any funny ideas and fill this fucker up.
    private static readonly List<object?> s_emptyList = new();

    private readonly Ws _ws = new();

    /// <summary>
    ///     Indicates whether the client is connected or not.
    /// </summary>
    public bool Connected => _ws.Connected;

    /// <summary>
    ///     Generates a random base64 string of the length specified.
    /// </summary>
    public static string GetRandomId(int length) {
        Span<byte> buf = stackalloc byte[length];
        ThreadRng.Shared.NextBytes(buf);
        return Convert.ToBase64String(buf);
    }

    /// <summary>
    ///     Opens the connection to the Surreal server.
    /// </summary>
    public async Task Open(Uri url, CancellationToken ct = default) {
        ThrowIfConnected();
        await _ws.Open(url, ct);
    }

    /// <summary>
    ///     Closes the connection to the Surreal server.
    /// </summary>
    public async Task Close(CancellationToken ct = default) {
        await _ws.Close(ct);
    }

    /// <inheritdoc cref="IDisposable" />
    public void Dispose() {
        _ws.Dispose();
    }

    /// <inheritdoc cref="IAsyncDisposable" />
    public ValueTask DisposeAsync() {
        return _ws.DisposeAsync();
    }

    /// <summary>
    ///     Sends the specified request to the Surreal server, and returns the response.
    /// </summary>
    public async Task<Response> Send(Request req, CancellationToken ct = default) {
        ThrowIfDisconnected();
        req.id ??= GetRandomId(6);
        req.parameters ??= s_emptyList;

        await using RecyclableMemoryStream stream = new(s_manager.Value);

        await JsonSerializer.SerializeAsync(stream, req, SerializerOptions.Shared, ct);
        // Now Position = Length = EndOfMessage
        // Write the buffer to the websocket
        stream.Position = 0;
        var (rsp, nty, stm) = await _ws.RequestOnce(req.id, stream, ct);
        if (!nty.IsDefault) {
            ThrowExpectRspGotNty();
        }

        if (rsp.IsDefault) {
            ThrowRspDefault();
        }

        var bdy = await JsonSerializer.DeserializeAsync<JsonDocument>(stm, SerializerOptions.Shared, ct);
        if (bdy is null) {
            ThrowRspDefault();
        }
        return new(rsp.id, rsp.err, ExtractResult(bdy));
    }

    private static JsonElement ExtractResult(JsonDocument root) {
        return root.RootElement.TryGetProperty("result", out JsonElement res) ? res : default;
    }

    private void ThrowIfDisconnected() {
        if (!Connected) {
            throw new InvalidOperationException("The connection is not open.");
        }
    }

    private void ThrowIfConnected() {
        if (Connected) {
            throw new InvalidOperationException("The connection is already open");
        }
    }

    [DoesNotReturn]
    private static void ThrowExpectRspGotNty() {
        throw new InvalidOperationException("Expected a response, got a notification");
    }

    [DoesNotReturn]
    private static void ThrowRspDefault() {
        throw new InvalidOperationException("Invalid response");
    }

    public record struct Request(
        string? id,
        [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault),]
        bool async,
        string? method,
        [property: JsonPropertyName("params"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault),]
        List<object?>? parameters);

    public readonly record struct Response(
        string? id,
        [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault),]
        Error error,
        [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault),]
        JsonElement result);

    public readonly record struct Error(
        int code,
        [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault),]
        string? message);


    public record struct Notify(
        string? id,
        string? method,
        [property: JsonPropertyName("params"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault),]
        List<object?>? parameters);


}
