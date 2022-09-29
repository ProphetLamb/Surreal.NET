using System.Buffers;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Net.WebSockets;
using System.Text.Json;

using SurrealDB.Common;
using SurrealDB.Json;

namespace SurrealDB.Ws;

public sealed class WsTx : IDisposable {
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
    public async Task<(string? id, RspHeader rsp, NtyHeader nty, Stream body)> Tr(CancellationToken ct) {
        ThrowIfDisconnected();
        // this method assumes that the header size never exceeds DefaultBufferSize!
        IMemoryOwner<byte> owner = MemoryPool<byte>.Shared.Rent(DefaultBufferSize);
        var r = await _ws.ReceiveAsync(owner.Memory, ct);

        if (r.Count <= 0) {
            return (default, default, default, default!);
        }

        // parse the header
        var (rsp, nty, off) = ParseHeader(owner.Memory.Span.Slice(0, r.Count));
        string? id = rsp.IsDefault ? nty.id : rsp.id;
        if (String.IsNullOrEmpty(id)) {
            ThrowHeaderId();
        }
        // returns a stream over the remainder of the body
        Stream body = CreateBody(r, owner, owner.Memory.Slice(off));
        return (id,  rsp, nty, body);
    }

    private static (RspHeader rsp, NtyHeader nty, int off) ParseHeader(ReadOnlySpan<byte> utf8) {
        var (rsp, rspOff, rspErr) = RspHeader.Parse(utf8);
        if (rspErr is null) {
            return (rsp, default, (int)rspOff);
        }
        var (nty, ntyOff, ntyErr) = NtyHeader.Parse(utf8);
        if (ntyErr is null) {
            return (default, nty, (int)ntyOff);
        }

        throw new JsonException($"Failed to parse RspHeader or NotifyHeader: {rspErr} \n--AND--\n {ntyErr}", null, 0, Math.Max(rspOff, ntyOff));
    }

    private Stream CreateBody(ValueWebSocketReceiveResult res, IDisposable owner, ReadOnlyMemory<byte> rem) {
        // check if rsp is already completely in the buffer
        if (res.EndOfMessage) {
            // create a rented stream from the remainder.
            MemoryStream s = RentedMemoryStream.FromMemory(owner, rem, true, true);
            s.SetLength(res.Count);
            return s;
        }

        // the rsp is not recv completely!
        // create a stream wrapping the websocket
        // with the recv portion as a prefix
        Debug.Assert(res.Count == rem.Length);
        return new WsStream(owner, rem, _ws);
    }

    /// <summary>
    /// Sends the stream over the socket.
    /// </summary>
    /// <remarks>
    /// Fast if used with a <see cref="MemoryStream"/> with exposed buffer!
    /// </remarks>
    public async Task Tw(Stream req, CancellationToken ct) {
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

    public readonly record struct NtyHeader(string? id, string? method, WsClient.Error err) {
        public bool IsDefault => default == this;

        /// <summary>
        /// Parses the head including the result propertyname, excluding the result array.
        /// </summary>
        internal static (NtyHeader head, long off, string? err) Parse(in ReadOnlySpan<byte> utf8) {
            Fsm fsm = new() {
                Lexer = new(utf8, false, new JsonReaderState(new() { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true })),
                State =  Fsms.Start,
            };
            while (fsm.MoveNext()) {}

            if (!fsm.Success) {
                return (default, fsm.Lexer.BytesConsumed, $"Error while parsing {nameof(RspHeader)} at {fsm.Lexer.TokenStartIndex}: {fsm.Err}");
            }
            return (new(fsm.Id, fsm.Method, fsm.Error), default, default);
        }

        private enum Fsms {
            Start, // -> Prop
            Prop, // -> PropId | PropAsync | PropMethod | ProsResult
            PropId, // -> Prop | End
            PropMethod, // -> Prop | End
            PropError, // -> End
            PropParams, // -> End
            End
        }

        private ref struct Fsm {
            public Fsms State;
            public Utf8JsonReader Lexer;
            public string? Err;
            public bool Success;

            public string? Name;
            public string? Id;
            public WsClient.Error Error;
            public string? Method;

            public bool MoveNext() {
                return State switch {
                    Fsms.Start => Start(),
                    Fsms.Prop => Prop(),
                    Fsms.PropId => PropId(),
                    Fsms.PropMethod => PropMethod(),
                    Fsms.PropError => PropError(),
                    Fsms.PropParams => PropParams(),
                    Fsms.End => End(),
                    _ => false
                };
            }

            private bool Start() {
                if (!Lexer.Read() || Lexer.TokenType != JsonTokenType.StartObject) {
                    Err = "Unable to read token StartObject";
                    return false;
                }

                State = Fsms.Prop;
                return true;

            }

            private bool End() {
                Success = !String.IsNullOrEmpty(Id) && !String.IsNullOrEmpty(Method);
                return false;
            }

            private bool Prop() {
                if (!Lexer.Read() || Lexer.TokenType != JsonTokenType.PropertyName) {
                    Err = "Unable to read PropertyName";
                    return false;
                }

                Name = Lexer.GetString();
                if ("id".Equals(Name, StringComparison.OrdinalIgnoreCase)) {
                    State = Fsms.PropId;
                    return true;
                }
                if ("method".Equals(Name, StringComparison.OrdinalIgnoreCase)) {
                    State = Fsms.PropMethod;
                    return true;
                }
                if ("error".Equals(Name, StringComparison.OrdinalIgnoreCase)) {
                    State = Fsms.PropError;
                    return true;
                }
                if ("params".Equals(Name, StringComparison.OrdinalIgnoreCase)) {
                    State = Fsms.PropParams;
                    return true;
                }

                Err = $"Unknown PropertyName `{Name}`";
                return false;
            }

            private bool PropId() {
                if (!Lexer.Read() || Lexer.TokenType != JsonTokenType.String) {
                    Err = "Unable to read `id` property value";
                    return false;
                }

                State = Fsms.Prop;
                Id = Lexer.GetString();
                return true;
            }

            private bool PropError() {
                Error = JsonSerializer.Deserialize<WsClient.Error>(ref Lexer, SerializerOptions.Shared);
                State = Fsms.End;
                return true;
            }

            private bool PropMethod() {
                if (!Lexer.Read() || Lexer.TokenType != JsonTokenType.String) {
                    Err = "Unable to read `method` property value";
                    return false;
                }

                State = Fsms.Prop;
                Method = Lexer.GetString();
                return true;
            }

            private bool PropParams() {
                // Do not parse the result!
                // The complete result is not present in the buffer!
                // The result is returned as a unevaluated asynchronous stream!
                State = Fsms.End;
                return true;
            }
        }
    }

    public readonly record struct RspHeader(string? id, WsClient.Error err) {
        public bool IsDefault => default == this;

        /// <summary>
        /// Parses the head including the result propertyname, excluding the result array.
        /// </summary>
        internal static (RspHeader head, long off, string? err) Parse(in ReadOnlySpan<byte> utf8) {
            Fsm fsm = new() {
                Lexer = new(utf8, false, new JsonReaderState(new() { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true })),
                State =  Fsms.Start,
            };
            while (fsm.MoveNext()) {}

            if (!fsm.Success) {
                return (default, fsm.Lexer.BytesConsumed, $"Error while parsing {nameof(RspHeader)} at {fsm.Lexer.TokenStartIndex}: {fsm.Err}");
            }
            return (new(fsm.Id, fsm.Error), default, default);
        }

        private enum Fsms {
            Start, // -> Prop
            Prop, // -> PropId | PropError | ProsResult
            PropId, // -> Prop | End
            PropError, // -> End
            PropResult, // -> End
            End
        }

        private ref struct Fsm {
            public Fsms State;
            public Utf8JsonReader Lexer;
            public string? Err;
            public bool Success;

            public string? Name;
            public string? Id;
            public WsClient.Error Error;

            public bool MoveNext() {
                return State switch {
                    Fsms.Start => Start(),
                    Fsms.Prop => Prop(),
                    Fsms.PropId => PropId(),
                    Fsms.PropError => PropError(),
                    Fsms.PropResult => PropResult(),
                    Fsms.End => End(),
                    _ => false
                };
            }

            private bool Start() {
                if (!Lexer.Read() || Lexer.TokenType != JsonTokenType.StartObject) {
                    Err = "Unable to read token StartObject";
                    return false;
                }

                State = Fsms.Prop;
                return true;

            }

            private bool End() {
                Success = !String.IsNullOrEmpty(Id);
                return false;
            }

            private bool Prop() {
                if (!Lexer.Read() || Lexer.TokenType != JsonTokenType.PropertyName) {
                    Err = "Unable to read PropertyName";
                    return false;
                }

                Name = Lexer.GetString();
                if ("id".Equals(Name, StringComparison.OrdinalIgnoreCase)) {
                    State = Fsms.PropId;
                    return true;
                }
                if ("result".Equals(Name, StringComparison.OrdinalIgnoreCase)) {
                    State = Fsms.PropResult;
                    return true;
                }
                if ("error".Equals(Name, StringComparison.OrdinalIgnoreCase)) {
                    State = Fsms.PropError;
                    return true;
                }

                Err = $"Unknown PropertyName `{Name}`";
                return false;
            }

            private bool PropId() {
                if (!Lexer.Read() || Lexer.TokenType != JsonTokenType.String) {
                    Err = "Unable to read `id` property value";
                    return false;
                }

                State = Fsms.Prop;
                Id = Lexer.GetString();
                return true;
            }

            private bool PropError() {
                Error = JsonSerializer.Deserialize<WsClient.Error>(ref Lexer, SerializerOptions.Shared);
                State = Fsms.End;
                return true;
            }


            private bool PropResult() {
                // Do not parse the result!
                // The complete result is not present in the buffer!
                // The result is returned as a unevaluated asynchronous stream!
                State = Fsms.End;
                return true;
            }
        }
    }
}
