using System.Collections;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace SurrealDB.Models.Result;

/// <summary>
///     The response from a query to the Surreal database via REST.
/// </summary>
[DebuggerDisplay("{ToString(),nq}")]
public readonly partial record struct DriverResponse : IEnumerable<RawResult> {
    internal static DriverResponse EmptyOk = new(ArraySegment<RawResult>.Empty);

    // arraysegment is faster then readonlymemory, tho we do need expicit write protection
    private readonly ArraySegment<RawResult> _raw;
    private readonly RawResult _single;

    /// <summary>
    /// Initializes a new instance of <see cref="DriverResponse"/> from existing results, by copying the data.
    /// </summary>
    /// <param name="results">The existing data</param>
    /// <param name="owned">Whether the data is already owned; does not copy if true AND (<paramref name="results"/> is a array, OR <see cref="ArraySegment{T}"/>)!</param>
    public DriverResponse(IEnumerable<RawResult> results, bool owned = false) {
        // do not copy owned memory!
        if (owned && results is RawResult[] arr) {
            _raw = arr;
            return;
        }

        if (owned && results is ArraySegment<RawResult> seg) {
            _raw = seg;
            return;
        }

        RawResult[] raw = results.ToArray(); // by now ToArray is faster, then manually checking the CopyTo method, by using the IListProvider interface
        if (raw.Length == 0) {
            // ignore empty arrays
            return;
        }
        // keep boxed array
        _raw = new(raw);
    }

    private DriverResponse(ArraySegment<RawResult> owned) {
        _raw = owned;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static DriverResponse FromOwned(ArraySegment<RawResult> owned) => new(owned); // protect ctor

    /// <summary>
    /// Boxes the result.
    /// </summary>
    /// <param name="result"></param>
    public DriverResponse(RawResult result) {
        _single = result;
    }

    public bool IsDefault => _raw.Array is null && _single.IsDefault;

    /// <summary>
    /// Returns the memory occupied by the result data, returns an empty span if empty or single
    /// </summary>
    public ReadOnlySpan<RawResult> Results => _raw.AsSpan();

    public OkIterator Oks => new(GetEnumerator());

    public ErrorIterator Errors => new(GetEnumerator());
    public bool HasErrors => Errors.Any();
    public bool IsEmpty => _raw.Array is null && _single.IsDefault;

    public bool IsSingle => _raw.Array is null && !_single.IsDefault;

    IEnumerator IEnumerable.GetEnumerator() {
        return GetEnumerator();
    }
}
