using System.Text.Json;

using SurrealDB.Common;

namespace SurrealDB.Models.Result;

public static class ResponseExtensions {
    public static bool TryGetFirstError(in this Result.DriverResponse rsp, out ErrorResult result) {
        foreach (ErrorResult res in rsp.Errors) {
            result = res;
            return true;
        }

        result = default;
        return false;
    }

    public static ErrorResult FirstError(in this Result.DriverResponse rsp) => rsp.TryGetFirstError(out var err) ? err : ResultContentException.ExpectedAnyError();
    public static ErrorResult FirstError(in this Result.DriverResponse rsp, in ErrorResult fallback) => rsp.TryGetFirstError(out var err) ? err : fallback;

    public static bool TryGetFirstValue(in this Result.DriverResponse rsp, out ResultValue result) {
        foreach (OkResult res in rsp.Oks) {
            if (res.Value.Inner.ValueKind is not JsonValueKind.Undefined) {
                result = res.Value;
                return true;
            }
        }

        result = default;
        return false;
    }

    public static ResultValue FirstValue(in this Result.DriverResponse rsp) => rsp.TryGetFirstValue(out var err) ? err : ResultContentException.ExpectedAnyOk().Value;

    public static ResultValue FirstValue(in this Result.DriverResponse rsp, in ResultValue fallback) => rsp.TryGetFirstValue(out var err) ? err : fallback;

    public static bool TryGetSingleError(in this Result.DriverResponse rsp, out ErrorResult result) {
        Result.DriverResponse.ErrorIterator en = rsp.Errors;
        return SequenceHelper.TrySingle(ref en, out result);
    }

    public static ErrorResult SingleError(in this Result.DriverResponse rsp) => rsp.TryGetSingleError(out var err) ? err : ResultContentException.ExpectedSingleError();
    public static ErrorResult SingleError(in this Result.DriverResponse rsp, in ErrorResult fallback) => rsp.TryGetSingleError(out var err) ? err : fallback;

    public static bool TryGetSingleValue(in this Result.DriverResponse rsp, out ResultValue result) {
        Result.DriverResponse.OkIterator en = rsp.Oks;
        bool success = SequenceHelper.TrySingle(ref en, out OkResult ok);
        result = ok.Value;
        return success;
    }

    public static ResultValue SingleValue(in this Result.DriverResponse rsp) => rsp.TryGetSingleValue(out var err) ? err : ResultContentException.ExpectedSingleOk().Value;
    public static ResultValue SingleValue(in this Result.DriverResponse rsp, in ResultValue fallback) => rsp.TryGetSingleValue(out var err) ? err : fallback;
}
