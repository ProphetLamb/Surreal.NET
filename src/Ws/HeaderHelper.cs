using System.Diagnostics;
using System.Net.WebSockets;
using System.Text.Json;

using SurrealDB.Common;
using SurrealDB.Json;

namespace SurrealDB.Ws;

public static class HeaderHelper {
    /// <summary>Generates a random base64 string of the length specified.</summary>
    public static string GetRandomId(int length) {
        Span<byte> buf = stackalloc byte[length];
        ThreadRng.Shared.NextBytes(buf);
        return Convert.ToBase64String(buf);
    }

    public static WsHeader Parse(ReadOnlySpan<byte> utf8) {
        var (rsp, rspOff, rspErr) = RspHeader.Parse(utf8);
        if (rspErr is null) {
            return new(rsp, default, (int)rspOff);
        }
        var (nty, ntyOff, ntyErr) = NtyHeader.Parse(utf8);
        if (ntyErr is null) {
            return new(default, nty, (int)ntyOff);
        }

        return default;
    }
}

public readonly record struct WsHeader(RspHeader Response, NtyHeader Notify, int BytesLength) {
   public string? Id => (Response.IsDefault, Notify.IsDefault) switch {
        (true, false) => Notify.id,
        (false, true) => Response.id,
        _ => null
    };
}

public readonly record struct WsHeaderWithMessage(WsHeader Header, WsReceiverMessageReader Message) : IDisposable {
    public void Dispose() {
        Message.Dispose();
    }
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

