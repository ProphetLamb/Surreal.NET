using System.Collections;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace SurrealDB.Common;

internal static class SequenceHelper {
    /// <summary>
    /// Attempts to consume two elements, if exactly one element was consumed returns that element
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool TrySingle<U, T>(ref U seq, [NotNullWhen(true)] out T? value)
        where U : IEnumerator<T> {
        if (!seq.MoveNext()) {
            value = default!;
            return false;
        }

        value = seq.Current!;

        if (seq.MoveNext()) {
            value = default!;
            return false;
        }

        return true;
    }

    public static unsafe FilterEn<U, T> Filter<U, T>(in U seq, delegate*<in T, bool> sel)
        where U : IEnumerator<T> => new(seq, sel);

    public unsafe struct FilterEn<U, T> : IEnumerator<T>
        where U: IEnumerator<T> {
        private U _en;
        private readonly delegate*<in T, bool> _sel;

        public FilterEn(U en, delegate*<in T, bool> sel) {
            _en = en;
            _sel = sel;
        }

        public bool MoveNext() {
            while (_en.MoveNext()) {
                if (_sel(_en.Current)) {
                    return true;
                }
            }

            return false;
        }

        public void Reset() {
            _en.Reset();
        }

        public T Current => _en.Current;

        object IEnumerator.Current => ((IEnumerator)_en).Current!;

        public void Dispose() {
            _en.Dispose();
        }
    }
}
