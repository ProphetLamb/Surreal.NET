using System.Runtime.CompilerServices;

namespace SurrealDB.Common;

/// <summary>Extension methods for Tasks</summary>
public static class TaskExtensions {
    /// <summary>The task is <see cref="SynchronizationContext"/> invariant.
    /// The previous context is not restored upon completion.</summary>
    /// <remarks>Equivalent to <code>Task.ConfigureAwait(false)</code>.</remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ConfiguredTaskAwaitable Inv(this Task t) => t.ConfigureAwait(false);

    /// <inheritdoc cref="Inv(Task)"/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ConfiguredTaskAwaitable<T> Inv<T>(this Task<T> t) => t.ConfigureAwait(false);
    /// <inheritdoc cref="Inv(Task)"/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ConfiguredValueTaskAwaitable Inv(in this ValueTask t) => t.ConfigureAwait(false);
    /// <inheritdoc cref="Inv(Task)"/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ConfiguredValueTaskAwaitable<T> Inv<T>(in this ValueTask<T> t) => t.ConfigureAwait(false);
}
