using System.Buffers;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Net.WebSockets;
using System.Text.Json;

using SurrealDB.Common;
using SurrealDB.Ws.Models;

namespace SurrealDB.Ws;

internal sealed class WsChannel : IDisposable {
    private readonly ClientWebSocket _ws = new();

    public static int DefaultBufferSize => 16 * 1024;

    /// <summary>
    ///     Indicates whether the client is connected or not.
    /// </summary>
    public bool Connected => _ws.State == WebSocketState.Open;

    public async Task Open(Uri remote, CancellationToken ct = default) {
        ThrowIfConnected();
        await _ws.ConnectAsync(remote, ct);
    }

    public async Task Close(CancellationToken ct = default) {
        if (_ws.State == WebSocketState.Closed) {
            return;
        }
        await _ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "client disconnect", ct);
    }

    public void Dispose() {
        _ws.Dispose();
    }

    /// <summary>
    /// Receives a response stream from the socket.
    /// Parses the header.
    /// The body contains the result array including the end object token `[...]}`.
    /// </summary>
    public async Task<(string id, ResponseHeader rsp, NotifyHeader nty, Stream body)> Recv(CancellationToken ct) {
        ThrowIfDisconnected();
        // this method assumes that the header size never exceeds DefaultBufferSize!
        IMemoryOwner<byte> owner = MemoryPool<byte>.Shared.Rent(DefaultBufferSize);
        var r = await _ws.ReceiveAsync(owner.Memory, ct);

        // parse the header
        var (rsp, nty, off) = ParseHeader(owner, r);
        string? id = rsp.IsDefault ? nty.id : rsp.id;
        if (String.IsNullOrEmpty(id)) {
            ThrowHeaderId();
        }
        // returns a stream over the remainder of the body
        Stream body = CreateBody(r, owner, owner.Memory.Slice(off));
        return (id,  rsp, nty, body);
    }

    private static (ResponseHeader rsp, NotifyHeader nty, int off) ParseHeader(IMemoryOwner<byte> owner, ValueWebSocketReceiveResult r) {
        ReadOnlySpan<byte> utf8 = owner.Memory.Span.Slice(0, r.Count);
        var (rsp, rspOff, rspErr) = ResponseHeader.Parse(utf8);
        if (rspErr is null) {
            return (rsp, default, (int)rspOff);
        }
        var (nty, ntyOff, ntyErr) = NotifyHeader.Parse(utf8);
        if (ntyErr is null) {
            return (default, nty, (int)ntyOff);
        }

        throw new JsonException($"Failed to parse RspHeader or NotifyHeader: {rspErr} \n--AND--\n {ntyErr}", null, 0, Math.Max(rspOff, ntyOff));
    }

    private Stream CreateBody(ValueWebSocketReceiveResult res, IDisposable owner, ReadOnlyMemory<byte> rem) {
        // check if rsp is already completely in the buffer
        if (res.EndOfMessage) {
            // create a rented stream from the remainder.
            return RentedMemoryStream.FromMemory(owner, rem, true, true);
        }

        // the rsp is not recv completely!
        // create a stream wrapping the websocket
        // with the recv portion as a prefix
        return new WsStream(owner, rem, _ws);
    }

    /// <summary>
    /// Sends the stream over the socket.
    /// </summary>
    /// <remarks>
    /// Fast if used with a <see cref="MemoryStream"/> with exposed buffer!
    /// </remarks>
    public async Task Reqt(Stream req, CancellationToken ct) {
        ThrowIfDisconnected();
        if (req is MemoryStream ms && ms.TryGetBuffer(out ArraySegment<byte> raw)) {
            // We can obtain the raw buffer from the request, send it
            await _ws.SendAsync(raw, WebSocketMessageType.Text, true, ct);
            return;
        }

        using IMemoryOwner<byte> owner = MemoryPool<byte>.Shared.Rent(DefaultBufferSize);
        bool end = false;
        while (!end && !ct.IsCancellationRequested) {
            int read = await req.ReadAsync(owner.Memory, ct);
            end = read != owner.Memory.Length;
            ReadOnlyMemory<byte> used = owner.Memory.Slice(0, read);
            await _ws.SendAsync(used, WebSocketMessageType.Text, end, ct);

            ThrowIfDisconnected();
            ct.ThrowIfCancellationRequested();
        }
        Debug.Assert(end, "Unfinished message sent!");
    }

    [DoesNotReturn]
    private static void ThrowHeaderId() {
        throw new InvalidOperationException("Header has no associated id!");
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
    private static void ThrowParseHead(string err, long off) {
        throw new JsonException(err, default, default, off);
    }
}
