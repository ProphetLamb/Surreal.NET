using System.Text.Json;

using SurrealDB.Common;

namespace SurrealDB.Models;

public readonly record struct RawResult {
    public RawResult(int code,
        string status,
        string? detail) {
        this.code = code;
        this.time = null;
        this.status = status;
        this.detail = detail;
        this.result = null;
    }

    public RawResult(int code,
        string status,
        string detail,
        JsonElement? result) {
        this.code = code;
        this.time = null;
        this.status = status;
        this.detail = detail;
        this.result = result;
    }

    public RawResult(
        string time,
        string status,
        string detail,
        JsonElement? result) {
        this.code = 0;
        this.time = time;
        this.status = status;
        this.detail = detail;
        this.result = result;
    }

    public bool IsOkResult => status == OkResult.OK;
    public int code { get; init; }
    public string? time { get; init; }
    public string status { get; init; }
    public string? detail { get; init; }
    public JsonElement? result { get; init; }

    public IResult ToResult() {
        return IsOkResult ? ToOkResult() : ToErrorResult();
    }

    public OkResult ToOkResult() {
        if (!result.HasValue) {
            return default;
        }

        return OkResult.From(result.Value.IntoSingle());
    }

    public ErrorResult ToErrorResult() {
        return new ErrorResult(code, status, detail);
    }
}
