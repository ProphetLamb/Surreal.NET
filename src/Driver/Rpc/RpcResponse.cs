using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;

using SurrealDB.Common;
using SurrealDB.Models;
using SurrealDB.Ws;

namespace SurrealDB.Driver.Rpc;

/// <summary>
///     The response from a query to the Surreal database via rpc.
/// </summary>
[DebuggerDisplay("{ToString(),nq}")]
public readonly struct RpcResponse : IResponse {
    private readonly Error _error;

public RpcResponse(
            string id,
            Error error,
            Result result) {
        Id = id;
        _error = error;
        UncheckedResult = result;
    }

    public string Id { get; }

    public bool IsOk => _error.Code == 0;
    public bool IsError => _error.Code != 0;

    public Result UncheckedResult { get; }
    public Error UncheckedError => _error;

    public bool TryGetError(out Error error) {
        error = _error;
        return IsError;
    }

    public bool TryGetResult(out Result result) {
        result = UncheckedResult;
        return IsOk;
    }

    public bool TryGetResult(
        out Result result,
        out Error error) {
        result = UncheckedResult;
        error = _error;
        return IsOk;
    }

    public void Deconstruct(
        out Result result,
        out Error error) {
        (result, error) = (UncheckedResult, _error);
    }

public static RpcResponse From(in WsClient.Response rsp) {
        if (rsp.id is null) {
            ThrowIdMissing();
        }

        if (rsp.error != default) {
            return new(rsp.id, new(rsp.error.code, rsp.error.message), default);
        }

        var result = UnpackFromStatusDocument(rsp.result);
        result = result.IntoSingle();

        // SurrealDB likes to returns a list of one result. Unbox this response, to conform with the REST client
        Result res = Result.From(result);
        return new(rsp.id, default, res);
    }

    public static JsonElement UnpackFromStatusDocument(in JsonElement root) {
        // Some results come as a simple array of objects (basically just a results array)
        // [ { }, { }, ... ]
        // Others come embedded into a 'status document' that can have multiple result sets
        //[
        //  {
        //    "result": [ { }, { }, ... ],
        //    "status": "OK",
        //    "time": "71.775µs"
        //  }
        //]

        if (root.ValueKind != JsonValueKind.Array) {
            return root;
        }

        foreach (var resultStatusDoc in root.EnumerateArray()) {
            if (resultStatusDoc.ValueKind != JsonValueKind.Object) {
                // if this was a status document, we would expect an object here
                return root;
            }

            var propertyCount = 0;
            JsonElement? resultProperty = null;
            foreach (var resultStatusDocProperty in resultStatusDoc.EnumerateObject()) {
                propertyCount++;
                if (resultStatusDocProperty.NameEquals("result")) {
                    resultProperty = resultStatusDocProperty.Value;
                }
                else if (!resultStatusDocProperty.NameEquals("status") && !resultStatusDocProperty.NameEquals("time")) {
                    // this property is not part of the 'status document',
                    // at this point we can be confident that it is just a simple array of objects
                    // so lets just return it
                    return root;
                }
            }

            if (propertyCount == 3) {
                // We ended up with 3 properties, so this must be a status document
                return resultProperty!.Value;
            }
        }

        // if we get here then all the properties had valid status document names
        // but was missing some of them
        return root;
    }

    public override string ToString() {
        string body = TryGetResult(out Result res, out Error err) ? res.ToString() : err.ToString();
        string status = IsError ? "ERR" : "OK";
        return $"{status}: {body}";
    }

    [DoesNotReturn]
    private static void ThrowIdMissing() {
        throw new InvalidOperationException("Response does not have an id.");
    }
}
