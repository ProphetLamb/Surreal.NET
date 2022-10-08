using System.Diagnostics.CodeAnalysis;
using System.Text.Json;

namespace SurrealDB.Models;

public interface IResponse {
    protected List<RawResult> RawResults { get; }

    IEnumerable<IResult> Results { get; }
    public static IEnumerable<IResult> GetResults(IResponse response) {
        return response.RawResults.Select(e => e.ToResult());
    }

    IEnumerable<OkResult> AllOkResults { get; }
    public static IEnumerable<OkResult> GetAllOkResults(IResponse response) {
        return response.RawResults.Where(e => e.IsOkResult).Select(e => e.ToOkResult());
    }

    IEnumerable<ErrorResult> AllErrorResults { get; }
    public static IEnumerable<ErrorResult> GetAllErrorResults(IResponse response) {
        return response.RawResults.Where(e => !e.IsOkResult).Select(e => e.ToErrorResult());
    }

    bool HasErrors { get; }
    public static bool GetHasErrors(IResponse response) {
        return response.RawResults.Any(e => !e.IsOkResult);
    }

    bool IsEmpty { get; }
    public static bool GetIsEmpty(IResponse response) {
        return !response.RawResults.Any();
    }

    public bool TryGetFirstErrorResult(out ErrorResult errorResult);
    public static bool TryGetFirstErrorResult(IResponse response, out ErrorResult errorResult) {
        foreach (var result in response.RawResults) {
            if (!result.IsOkResult) {
                errorResult = result.ToErrorResult();
                return true;
            }
        }
        errorResult = default;
        return false;
    }

    /// <summary>
    /// Returns the first non null <see cref="OkResult"/> in the result set
    /// </summary>
    public bool TryGetFirstOkResult(out OkResult okResult);
    public static bool TryGetFirstOkResult(IResponse response, out OkResult okResult) {
        foreach (var result in response.RawResults) {
            if (result.IsOkResult && result.result.HasValue) {
                if (result.result.Value.ValueKind == JsonValueKind.Null) {
                    continue;
                }

                okResult = result.ToOkResult();
                return true;
            }
        }
        okResult = default;
        return false;
    }
}
