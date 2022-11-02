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

    /// <summary>Creates a task awaiting the <see cref="WaitHandle"/>.</summary>
    /// <exception cref="ArgumentNullException">the handle is null</exception>
    public static Task ToTask(this WaitHandle handle)
    {
        if (handle == null) {
            throw new ArgumentNullException(nameof(handle));
        }

        TaskCompletionSource<object?> tcs = new();
        RegisteredWaitHandle? shared = null;
        RegisteredWaitHandle produced = ThreadPool.RegisterWaitForSingleObject(
            handle,
            (state, timedOut) =>
            {
                tcs.SetResult(null);

                while (true)
                {
                    RegisteredWaitHandle? consumed = Interlocked.CompareExchange(ref shared, null, null);
                    if (consumed is not null)
                    {
                        consumed.Unregister(null);
                        break;
                    }
                }
            },
            state: null,
            millisecondsTimeOutInterval: Timeout.Infinite,
            executeOnlyOnce: true);

        // Publish the RegisteredWaitHandle so that the callback can see it.
        Interlocked.CompareExchange(ref shared, produced, null);

        return tcs.Task;
    }

}
