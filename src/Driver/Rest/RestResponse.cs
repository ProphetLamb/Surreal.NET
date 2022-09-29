using System.Buffers;
using System.Diagnostics;
using System.Net;
using System.Text.Json;

using SurrealDB.Common;
using SurrealDB.Json;
using SurrealDB.Models;

namespace SurrealDB.Driver.Rest;

/// <summary>
///     The response from a query to the Surreal database via rest.
/// </summary>
[DebuggerDisplay("{ToString,nq}")]
public readonly struct RestResponse : IResponse {
    private readonly string? _status;
    private readonly string? _detail;
    private readonly string? _description;
    private readonly JsonElement _result;

    public RestResponse(
            string? time,
            string? status,
            string? description,
            string? detail,
            JsonElement result) {
        Time = time;
        _status = status;
        _detail = detail;
        _result = result;
        _description = description;
    }

    public string? Time { get; }

    public bool IsOk => string.Equals(_status, "ok", StringComparison.OrdinalIgnoreCase);

    public bool IsError => !IsOk;

    public bool TryGetError(out Models.Error error) {
        if (IsOk) {
            error = default;
            return false;
        }

        error = new(1, $"{_detail}: {_description}");
        return true;
    }

    public bool TryGetResult(out Result result) {
        if (IsError) {
            result = default;
            return false;
        }

        result = Result.From(_result);
        return true;
    }

    public bool TryGetResult(
        out Result result,
        out Models.Error error) {
        if (IsError) {
            result = default;
            error = new(1, _detail);
            return false;
        }

        result = Result.From(_result);
        error = default;
        return true;
    }

    /// <summary>
    /// Parses a <see cref="HttpResponseMessage"/> containing JSON to a <see cref="RestResponse"/>.
    /// </summary>
    public static async Task<RestResponse> From(
        HttpResponseMessage msg,
        CancellationToken ct = default) {
        #if NET6_0_OR_GREATER
        Stream stream = await msg.Content.ReadAsStreamAsync(ct);
        #else
        Stream stream = await msg.Content.ReadAsStreamAsync();
        #endif
        if (msg.StatusCode != HttpStatusCode.OK) {
            Error? err = await JsonSerializer.DeserializeAsync<Error>(stream, SerializerOptions.Shared, ct);
            return From(err);
        }

        if (await PeekIsEmpty(stream, ct)) {
            // Success and empty message -> invalid json
            return EmptyOk;
        }

        List<Success>? docs = await JsonSerializer.DeserializeAsync<List<Success>>(stream, SerializerOptions.Shared, ct);
        Success doc = (docs?.FirstOrDefault(e => e.result.ValueKind != JsonValueKind.Null)).GetValueOrDefault(default);

        return From(doc);
    }

    /// <summary>
    /// Attempts to peek the next byte of the stream.
    /// </summary>
    /// <remarks>
    /// Resets the stream to the original position.
    /// </remarks>
    private static async Task<bool> PeekIsEmpty(
        Stream stream,
        CancellationToken ct) {
        Debug.Assert(stream.CanSeek && stream.CanRead);
        using IMemoryOwner<byte> buffer = MemoryPool<byte>.Shared.Rent(1);
        // This is more efficient, then ReadByte.
        // Async, because this is the first request to the networkstream, thus no readahead is possible.
        int read = await stream.ReadAsync(buffer.Memory.Slice(0, 1), ct);
        stream.Seek(-read, SeekOrigin.Current);
        return read <= 0;
    }

    public static RestResponse EmptyOk => new(null, "ok", null, null, default);

    private static RestResponse From(Error? error) {
        return new(null, "HTTP_ERR", error?.description, error?.details, default);
    }

    private static RestResponse From(Success success) {
        if (success == default) {
            return new(success.time, success.status, null, null, default);
        }
        JsonElement e = success.result.IntoSingle();
        return new(success.time, success.status, null, null, e);
    }

    public override string ToString() {
        string body = TryGetResult(out Result res, out Models.Error err) ? res.ToString() : err.ToString();
        return $"{_status}: {body}";
    }

    private readonly record struct Error(
        int code,
        string details,
        string description,
        string information);

    private readonly record struct Success(
        string time,
        string status,
        JsonElement result);
}
