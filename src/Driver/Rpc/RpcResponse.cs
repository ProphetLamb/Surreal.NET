using System.Diagnostics;

using SurrealDB.Models;

namespace SurrealDB.Driver.Rpc;

/// <summary>
///     The response from a query to the Surreal database via RPC.
/// </summary>
[DebuggerDisplay("{ToString(),nq}")]
public readonly struct RpcResponse : IResponse {
    internal static RpcResponse EmptyOk = new ();

    private readonly List<RawResult> _rawResults;
    List<RawResult> IResponse.RawResults => _rawResults;

    public IEnumerable<IResult> Results => IResponse.GetResults(this);
    public IEnumerable<OkResult> AllOkResults => IResponse.GetAllOkResults(this);
    public IEnumerable<ErrorResult> AllErrorResults => IResponse.GetAllErrorResults(this);
    public bool HasErrors => IResponse.GetHasErrors(this);
    public bool IsEmpty => IResponse.GetIsEmpty(this);

    public RpcResponse() {
        _rawResults = new List<RawResult>();
    }
    public RpcResponse(List<RawResult> results) {
        _rawResults = results;
    }
    public RpcResponse(RawResult result) {
        _rawResults = new List<RawResult> { result };
    }

    public bool TryGetFirstErrorResult(out ErrorResult errorResult) {
        return IResponse.TryGetFirstErrorResult(this, out errorResult);
    }

    public bool TryGetFirstOkResult(out OkResult okResult) {
        return IResponse.TryGetFirstOkResult(this, out okResult);
    }
}
