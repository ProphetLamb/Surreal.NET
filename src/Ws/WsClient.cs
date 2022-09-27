using System.Buffers;
using System.Net.WebSockets;
using System.Text.Json;
using System.Text.Json.Serialization;

using Microsoft.IO;

using SurrealDB.Common;
using SurrealDB.Json;

namespace SurrealDB.Ws;

/// <summary>
///     The client used to connect to the Surreal server via JSON RPC.
/// </summary>
public sealed class WsClient : IDisposable, IAsyncDisposable {
    const int FRAME_SIZE = 4096;
    private static readonly Lazy<RecyclableMemoryStreamManager> s_manager = new(static () => new());

    // Do not get any funny ideas and fill this fucker up.
    public static readonly List<object?> EmptyList = new();

    private ClientWebSocket? _ws;

    /// <summary>
    ///     Indicates whether the client is connected or not.
    /// </summary>
    public bool Connected => _ws is not null && _ws.State == WebSocketState.Open;

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
    public async Task Open(
        Uri url,
        CancellationToken ct = default) {
        ThrowIfConnected();
        try {
            _ws = new ClientWebSocket();
            await _ws.ConnectAsync(url, ct);
        } catch {
            // Clean state
            _ws?.Dispose();
            _ws = null;
            throw;
        }
    }

    /// <summary>
    ///     Closes the connection to the Surreal server.
    /// </summary>
    public async Task Close(CancellationToken ct = default) {
        if (_ws is null) {
            return;
        }

        await _ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "Client closed", ct);
        _ws.Dispose();
        _ws = null;
    }

    /// <inheritdoc cref="IDisposable" />
    public void Dispose() {
        if (_ws is not null) {
            Close().Wait();
        }
    }

    /// <inheritdoc cref="IAsyncDisposable" />
    public ValueTask DisposeAsync() {
        return _ws is null ? default : new(Close());
    }

    /// <summary>
    ///     Sends the specified request to the Surreal server, and returns the response.
    /// </summary>
    /// <param name="req"> The request to send </param>
    public async Task<Response> Send(
        Request req,
        CancellationToken ct = default) {
        ThrowIfDisconnected();
        req.id ??= GetRandomId(6);
        req.parameters ??= EmptyList;

        await using RecyclableMemoryStream stream = new(s_manager.Value);

        await JsonSerializer.SerializeAsync(stream, req, SerializerOptions.Shared, ct);
        // Now Position = Length = EndOfMessage
        // Write the buffer to the websocket
        stream.Position = 0;
        await SendStream(_ws!, stream, ct);
        // Reset the Position and Length
        // Read the response from the websocket
        stream.Position = 0;
        stream.SetLength(0);
        await ReceiveStream(_ws!, stream, ct);
        // Read the buffer to json DOM
        stream.Position = 0;
        Response rsp = await JsonSerializer.DeserializeAsync<Response>(stream, SerializerOptions.Shared, ct);
        return rsp;
    }

    private static async Task SendStream(WebSocket ws, Stream stream, CancellationToken ct) {
        using IMemoryOwner<byte> frame = MemoryPool<byte>.Shared.Rent(FRAME_SIZE);
        int read;
        while ((read = stream.Read(frame.Memory.Span)) > 0) {
            await ws.SendAsync(frame.Memory.Slice(0, read), WebSocketMessageType.Text, IsLastFrame(stream, FRAME_SIZE), ct);
        }
    }

    private static async Task ReceiveStream(WebSocket ws, Stream stream, CancellationToken ct) {
        using IMemoryOwner<byte> frame = MemoryPool<byte>.Shared.Rent(FRAME_SIZE);
        ValueWebSocketReceiveResult res;
        do {
            res = await ws.ReceiveAsync(frame.Memory, ct);
            await stream.WriteAsync(frame.Memory.Slice(0, res.Count), ct);
        } while (!res.EndOfMessage);
    }

    private static bool IsLastFrame(Stream stream, long frameSize) {
        return stream.Position + frameSize >= stream.Length;
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
