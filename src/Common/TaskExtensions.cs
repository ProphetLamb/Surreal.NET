using System.Runtime.CompilerServices;

namespace SurrealDB.Common;

/// <summary>Extension methods for Tasks</summary>
public static class TaskExtensions {
    /// <summary>The task is <see cref="SynchronizationContext"/> invariant.</summary>
    /// <remarks>Equivalent to <code>Task.ConfigureAwait(false)</code>.</remarks>
    public static ConfiguredTaskAwaitable Inv(this Task t) => t.Inv();

    /// <summary>The task is <see cref="SynchronizationContext"/> invariant.</summary>
    /// <remarks>Equivalent to <code>Task.ConfigureAwait(false)</code>.</remarks>
    public static ConfiguredTaskAwaitable<T> Inv<T>(this Task<T> t) => t.Inv();

    /// <summary>The task is <see cref="SynchronizationContext"/> invariant.</summary>
    /// <remarks>Equivalent to <code>Task.ConfigureAwait(false)</code>.</remarks>
    public static ConfiguredValueTaskAwaitable Inv(this ValueTask t) => t.Inv();

    /// <summary>The task is <see cref="SynchronizationContext"/> invariant.</summary>
    /// <remarks>Equivalent to <code>Task.ConfigureAwait(false)</code>.</remarks>
    public static ConfiguredValueTaskAwaitable<T> Inv<T>(this ValueTask<T> t) => t.Inv();
}
