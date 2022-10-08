using System.Diagnostics;

using SurrealDB.Models;

namespace SurrealDB.Driver.Rest;

/// <summary>
///     The response from a query to the Surreal database via REST.
/// </summary>
[DebuggerDisplay("{ToString(),nq}")]
public readonly struct RestResponse : IResponse {
    internal static RestResponse EmptyOk = new ();

    private readonly List<RawResult> _rawResults;
    List<RawResult> IResponse.RawResults => _rawResults;

    public IEnumerable<IResult> Results => IResponse.GetResults(this);
    public IEnumerable<OkResult> AllOkResults => IResponse.GetAllOkResults(this);
    public IEnumerable<ErrorResult> AllErrorResults => IResponse.GetAllErrorResults(this);
    public bool HasErrors => IResponse.GetHasErrors(this);
    public bool IsEmpty => IResponse.GetIsEmpty(this);

    public RestResponse() {
        _rawResults = new List<RawResult>();
    }
    public RestResponse(List<RawResult> results) {
        _rawResults = results;
    }
    public RestResponse(RawResult result) {
        _rawResults = new List<RawResult> { result };
    }

    public bool TryGetFirstErrorResult(out ErrorResult errorResult) {
        return IResponse.TryGetFirstErrorResult(this, out errorResult);
    }

    public bool TryGetFirstOkResult(out OkResult okResult) {
        return IResponse.TryGetFirstOkResult(this, out okResult);
    }
}
