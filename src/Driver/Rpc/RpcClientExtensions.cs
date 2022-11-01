using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Text.Json;

using SurrealDB.Common;
using SurrealDB.Json;
using SurrealDB.Models;
using SurrealDB.Models.Result;
using SurrealDB.Ws;

using DriverResponse = SurrealDB.Models.Result.DriverResponse;

namespace SurrealDB.Driver.Rpc;

internal static class RpcClientExtensions {
    internal static async Task<DriverResponse> ToSurreal(this Task<WsClient.Response> rsp) => ToSurreal(await rsp);
    internal static DriverResponse ToSurreal(this WsClient.Response rsp){
        if (rsp.id is null) {
            ThrowIdMissing();
        }

        if (rsp.error != default) {
            return new DriverResponse(RawResult.TransportError(rsp.error.code, string.Empty, rsp.error.message ?? ""));
        }

        return UnpackFromStatusDocument(in rsp);
    }

    private static DriverResponse UnpackFromStatusDocument(in WsClient.Response rsp) {
        // Some results come as a simple object or an array of objects or even and empty string
        // [ { }, { }, ... ]
        // Others come embedded into a 'status document' that can have multiple result sets
        //[
        //  {
        //    "result": [ { }, { }, ... ],
        //    "status": "OK",
        //    "time": "71.775µs"
        //  }
        //]

        if (rsp.result.ValueKind != JsonValueKind.Array) {
            return new(RawResult.Ok(default, rsp.result));
        }

        foreach (var resultStatusDoc in rsp.result.EnumerateArray()) {
            if (resultStatusDoc.ValueKind != JsonValueKind.Object) {
                // if this was a status document, we would expect an object here
                return ToSingleAny(in rsp);
            }

            if (resultStatusDoc.TryGetProperty("result", out _)
             && resultStatusDoc.TryGetProperty("status", out _)
             && resultStatusDoc.TryGetProperty("time", out _)) {
                return FromNestedStatus(in rsp);
            }

            return ToSingleAny(in rsp);
        }

        // if we get here then all the properties had valid status document names
        // but was missing some of them
        return new(RawResult.Ok(default, rsp.result));
    }

    private static DriverResponse ToSingleAny(in WsClient.Response rsp) {
        JsonElement root = rsp.result;
        if (root.ValueKind == JsonValueKind.Object) {
            var okOrErr = root.Deserialize<OkOrErrorResult>(SerializerOptions.Shared);
            return new(okOrErr.ToResult());
        }

        if (root.ValueKind == JsonValueKind.Array) {
            ArrayBuilder<RawResult> results = new();
            foreach (JsonElement element in root.EnumerateArray()) {
                results.Append(RawResult.Ok(default, element));
            }
            return new(results.AsSegment());
        }

        return new(RawResult.Ok(default, root));
    }

    private static DriverResponse FromNestedStatus(in WsClient.Response rsp) {
        ArrayBuilder<RawResult> builder = new();
        foreach (JsonElement e in rsp.result.EnumerateArray()) {
            OkOrErrorResult res = e.Deserialize<OkOrErrorResult>(SerializerOptions.Shared);
            builder.Append(res.ToResult());
        }

        return DriverResponse.FromOwned(builder.AsSegment());
    }

    [DoesNotReturn, DebuggerStepThrough, MethodImpl(MethodImplOptions.NoInlining)]
    private static void ThrowIdMissing() {
        throw new InvalidOperationException("Response does not have an id.");
    }

}
