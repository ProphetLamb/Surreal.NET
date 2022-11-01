using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace SurrealDB.Common;

public abstract record LatentReadonly {
    private bool _isReadonly;

    protected void MakeReadonly() {
        _isReadonly = true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected bool IsReadonly() => _isReadonly;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected void Set<T>(out T field, in T value) {
        ThrowIfReadonly();
        field = value;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void ThrowIfReadonly() {
        if (_isReadonly) {
            ThrowReadonly();
        }
    }

    [DoesNotReturn, MethodImpl(MethodImplOptions.NoInlining)]
    private static void ThrowReadonly() {
        throw new InvalidOperationException("The object is readonly and cannot be mutated.");
    }
}
