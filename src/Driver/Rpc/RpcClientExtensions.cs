using System.Diagnostics.CodeAnalysis;
using System.Text.Json;

using SurrealDB.Common;
using SurrealDB.Json;
using SurrealDB.Models;
using SurrealDB.Ws;
namespace SurrealDB.Driver.Rpc;

internal static class RpcClientExtensions {

    internal static async Task<RpcResponse> ToSurreal(this Task<WsClient.Response> rsp) => ToSurreal(await rsp);
    internal static RpcResponse ToSurreal(this WsClient.Response rsp){
        if (rsp.id is null) {
            ThrowIdMissing();
        }
        
        if (rsp.error != default) {
            var rawResult = new RawResult(rsp.error.code, ErrorResult.ERR, rsp.error.message);
            return new RpcResponse(rawResult);
        }

        var rawResults = UnpackFromStatusDocument(rsp.result);

        if (rawResults == null) {
            return new RpcResponse();
        }
        
        return new RpcResponse(rawResults);
    }

    [DoesNotReturn]
    private static void ThrowIdMissing() {
        throw new InvalidOperationException("Response does not have an id.");
    }

    private static List<RawResult>? UnpackFromStatusDocument(in JsonElement root) {
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
        
        if (root.ValueKind != JsonValueKind.Array) {
            return ToSingleRawResult(root);
        }

        foreach (var resultStatusDoc in root.EnumerateArray()) {
            if (resultStatusDoc.ValueKind != JsonValueKind.Object) {
                // if this was a status document, we would expect an object here
                return ToSingleRawResult(root);
            }

            var propertyCount = 0;
            JsonElement? resultProperty = null;
            foreach (var resultStatusDocProperty in resultStatusDoc.EnumerateObject()) {
                propertyCount++;
                if (resultStatusDocProperty.NameEquals("result")) {
                    resultProperty = resultStatusDocProperty.Value;
                } else if (!resultStatusDocProperty.NameEquals("status")
                 && !resultStatusDocProperty.NameEquals("time")) {
                    // this property is not part of the 'status document',
                    // at this point we can be confident that it is just a simple array of objects
                    // so lets just return it
                    return ToSingleRawResult(root);
                }
            }

            if (propertyCount == 3) {
                // We ended up with 3 properties, so this must be a status document
                var results = root.Deserialize<List<RawResult>>(SerializerOptions.Shared);
                return results;
            }
        }
        
        // if we get here then all the properties had valid status document names
        // but was missing some of them
        return ToSingleRawResult(root);
    }

    private static List<RawResult> ToSingleRawResult(in JsonElement element) {
        var result = new RawResult(null, OkResult.OK, null, element.IntoSingle());
        var rawResultList = new List<RawResult> { result };
        return rawResultList;
    }

}
