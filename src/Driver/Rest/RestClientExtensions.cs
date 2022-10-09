using System.Buffers;
using System.Diagnostics;
using System.Net;
using System.Text.Json;

using SurrealDB.Common;
using SurrealDB.Json;
using SurrealDB.Models;
using SurrealDB.Models.Result;

using DriverResponse = SurrealDB.Models.Result.DriverResponse;

namespace SurrealDB.Driver.Rest;

internal static class RestClientExtensions {
    internal static async Task<DriverResponse> ToSurreal(this HttpResponseMessage msg, CancellationToken ct = default) {
#if NET6_0_OR_GREATER
        Stream stream = await msg.Content.ReadAsStreamAsync(ct);
#else
        Stream stream = await msg.Content.ReadAsStreamAsync();
#endif
        if (!msg.IsSuccessStatusCode) {
            RestError restError = await JsonSerializer.DeserializeAsync<RestError>(stream, SerializerOptions.Shared, ct);
            return new DriverResponse(restError.ToErrorResult());
        }

        if (await PeekIsEmpty(stream, ct)) {
            // Success and empty message -> invalid json
            return default;
        }

        ArrayBuilder<RawResult> builder = new();
        await foreach (OkOrErrorResult res in JsonSerializer.DeserializeAsyncEnumerable<OkOrErrorResult>(stream, SerializerOptions.Shared, ct)) {
            if (!res.IsDefault) {
                builder.Append(res.ToResult());
            }
        }

        return DriverResponse.FromOwned(builder.AsSegment());
    }

    internal static async Task<DriverResponse> ToSurrealFromAuthResponse(this HttpResponseMessage msg, CancellationToken ct = default) {

            // Signin and Signup returns a different object to the other response
            // And for that reason needs it's on deserialization path
            // The whole response is ultimately shoved into the DriverResponse.Success.result field
            // {"code":200,"details":"Authentication succeeded","token":"a.jwt.token"}

#if NET6_0_OR_GREATER
        Stream stream = await msg.Content.ReadAsStreamAsync(ct);
#else
        Stream stream = await msg.Content.ReadAsStreamAsync();
#endif
        if (msg.StatusCode != HttpStatusCode.OK) {
            RestError restError = await JsonSerializer.DeserializeAsync<RestError>(stream, SerializerOptions.Shared, ct);
            return new DriverResponse(restError.ToErrorResult());
        }

        AuthResult result = await JsonSerializer.DeserializeAsync<AuthResult>(stream, SerializerOptions.Shared, ct);

        return new DriverResponse(result.ToResult());
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

    private readonly record struct RestError(int Code,
        string Details,
        string Description,
        string Information) {
        internal RawResult ToErrorResult() {
            return RawResult.TransportError(Code, Details, $"{Description}\n{Information}");
        }
    }

    public readonly record struct AuthResult(
        HttpStatusCode Code,
        string Details,
        JsonElement Token) {
        internal RawResult ToResult() {
            return RawResult.Ok(default, Token);
        }
    }
}
