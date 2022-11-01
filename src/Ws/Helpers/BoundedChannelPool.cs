using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Threading.Channels;

// ReSharper disable once CheckNamespace
namespace System.Buffers;

public abstract class BoundedChannelPool<T> {
    private static readonly TlsOverPerCoreLockedStacksBoundedChannelPool<T> s_boundedShared = new();

    public static BoundedChannelPool<T> Shared => s_boundedShared;

    public abstract BoundedChannel<T> Rent(int minimumLength);

    public abstract void Return(BoundedChannel<T> channel);
}

public sealed class BoundedChannel<T> : Channel<T>, IDisposable {
    private BoundedChannelPool<T>? _owner;

    public BoundedChannel(Channel<T, T> wrapped, int capacity, BoundedChannelPool<T> owner) {
        Reader = wrapped.Reader;
        Writer = wrapped.Writer;
        Capacity = capacity;
        _owner = owner;
    }

    public int Capacity { get; }

    public void Dispose() {
        var owner = _owner;
        _owner = null;
        if (owner is not null) {
            owner.Return(this);
        }
    }
}

// Source: https://source.dot.net/#System.Private.CoreLib/src/libraries/System.Private.CoreLib/src/System/Buffers/TlsOverPerCoreLockedStacksArrayPool.cs
// modified for use with channels
public sealed class TlsOverPerCoreLockedStacksBoundedChannelPool<T> : BoundedChannelPool<T> {
    /// <summary>The number of buckets (array sizes) in the pool, one for each array length, starting from length 16.</summary>
    private const int NumBuckets = 27; // GCHelper.SelectBucketIndex(1024 * 1024 * 1024 + 1)
    /// <summary>Maximum number of per-core stacks to use per array size.</summary>
    private const int MaxPerCorePerArraySizeStacks = 64; // selected to avoid needing to worry about processor groups
    /// <summary>The maximum number of buffers to store in a bucket's global queue.</summary>
    private const int MaxBuffersPerArraySizePerCore = 8;

    /// <summary>A per-thread array of arrays, to cache one array per array size per thread.</summary>
    [ThreadStatic]
    private static ThreadLocalArray[]? t_tlsBuckets;
    /// <summary>Used to keep track of all thread local buckets for trimming if needed.</summary>
    private readonly ConditionalWeakTable<ThreadLocalArray[], object?> _allTlsBuckets = new ConditionalWeakTable<ThreadLocalArray[], object?>();
    /// <summary>
    /// An array of per-core array stacks. The slots are lazily initialized to avoid creating
    /// lots of overhead for unused array sizes.
    /// </summary>
    private readonly PerCoreLockedStacks?[] _buckets = new PerCoreLockedStacks[NumBuckets];
    /// <summary>Whether the callback to trim arrays in response to memory pressure has been created.</summary>
    private int _trimCallbackCreated;

    /// <summary>Allocate a new PerCoreLockedStacks and try to store it into the <see cref="_buckets"/> array.</summary>
    private PerCoreLockedStacks CreatePerCoreLockedStacks(int bucketIndex)
    {
        var inst = new PerCoreLockedStacks();
        return Interlocked.CompareExchange(ref _buckets[bucketIndex], inst, null) ?? inst;
    }

    /// <summary>Gets an ID for the pool to use with events.</summary>
    private int Id => GetHashCode();

    public override BoundedChannel<T> Rent(int minimumLength)
    {
        BoundedChannel<T>? buffer;

        // Get the bucket number for the array length. The result may be out of range of buckets,
        // either for too large a value or for 0 and negative values.
        int bucketIndex = GCHelper.SelectBucketIndex(minimumLength);

        // First, try to get an array from TLS if possible.
        ThreadLocalArray[]? tlsBuckets = t_tlsBuckets;
        if (tlsBuckets is not null && (uint)bucketIndex < (uint)tlsBuckets.Length)
        {
            buffer = tlsBuckets[bucketIndex].Channel;
            if (buffer is not null)
            {
                tlsBuckets[bucketIndex].Channel = null;
                return buffer;
            }
        }

        // Next, try to get an array from one of the per-core stacks.
        PerCoreLockedStacks?[] perCoreBuckets = _buckets;
        if ((uint)bucketIndex < (uint)perCoreBuckets.Length)
        {
            PerCoreLockedStacks? b = perCoreBuckets[bucketIndex];
            if (b is not null)
            {
                buffer = b.TryPop();
                if (buffer is not null)
                {
                    return buffer;
                }
            }

            // No buffer available.  Ensure the length we'll allocate matches that of a bucket
            // so we can later return it.
            minimumLength = GCHelper.GetMaxSizeForBucket(bucketIndex);
        }
        else if (minimumLength <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(minimumLength));
        }

        // allocate a new bounded channel, that belongs to this instance
        buffer = new(Channel.CreateBounded<T>(minimumLength), minimumLength, this);

        return buffer;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override void Return(BoundedChannel<T>? channel) {
        if (channel is null) {
            ThrowChannelNull();
        }

        if (channel.Reader.Completion.IsCompleted) {
            ThrowChannelCompleted();
        }

        // Determine with what bucket this array length is associated
        int bucketIndex = GCHelper.SelectBucketIndex(channel.Capacity);

        // Make sure our TLS buckets are initialized.  Technically we could avoid doing
        // this if the array being returned is erroneous or too large for the pool, but the
        // former condition is an error we don't need to optimize for, and the latter is incredibly
        // rare, given a max size of 1B elements.
        ThreadLocalArray[] tlsBuckets = t_tlsBuckets ?? InitializeTlsBucketsAndTrimming();

        bool haveBucket = false;
        bool returned = true;
        if ((uint)bucketIndex < (uint)tlsBuckets.Length)
        {
            haveBucket = true;

            // Check to see if the buffer is the correct size for this bucket.
            if (channel.Capacity != GCHelper.GetMaxSizeForBucket(bucketIndex)) {
                ThrowChannelNotOfPool();
            }

            // Store the array into the TLS bucket.  If there's already an array in it,
            // push that array down into the per-core stacks, preferring to keep the latest
            // one in TLS for better locality.
            ref ThreadLocalArray tla = ref tlsBuckets[bucketIndex];
            BoundedChannel<T>? prev = tla.Channel;
            tla = new ThreadLocalArray(channel);
            if (prev is not null)
            {
                PerCoreLockedStacks stackBucket = _buckets[bucketIndex] ?? CreatePerCoreLockedStacks(bucketIndex);
                returned = stackBucket.TryPush(prev);
            }
        }
    }

    public bool Trim()
    {
        int currentMilliseconds = Environment.TickCount;
        GCHelper.MemoryPressure pressure = GCHelper.GetMemoryPressure();

        // Trim each of the per-core buckets.
        PerCoreLockedStacks?[] perCoreBuckets = _buckets;
        for (int i = 0; i < perCoreBuckets.Length; i++)
        {
            perCoreBuckets[i]?.Trim(currentMilliseconds, Id, pressure, GCHelper.GetMaxSizeForBucket(i));
        }

        // Trim each of the TLS buckets. Note that threads may be modifying their TLS slots concurrently with
        // this trimming happening. We do not force synchronization with those operations, so we accept the fact
        // that we may end up firing a trimming event even if an array wasn't trimmed, and potentially
        // trim an array we didn't need to.  Both of these should be rare occurrences.

        // Under high pressure, release all thread locals.
        if (pressure == GCHelper.MemoryPressure.High) {
            foreach (KeyValuePair<ThreadLocalArray[], object?> tlsBuckets in _allTlsBuckets)
            {
#if NET6_0_OR_GREATER
                Array.Clear(tlsBuckets.Key);
#else
                tlsBuckets.Key.AsSpan().Clear();
#endif
            }
        }
        else
        {
            // Otherwise, release thread locals based on how long we've observed them to be stored. This time is
            // approximate, with the time set not when the array is stored but when we see it during a Trim, so it
            // takes at least two Trim calls (and thus two gen2 GCs) to drop an array, unless we're in high memory
            // pressure. These values have been set arbitrarily; we could tune them in the future.
            uint millisecondsThreshold = pressure switch
            {
                GCHelper.MemoryPressure.Medium => 15_000,
                _ => 30_000,
            };

            foreach (KeyValuePair<ThreadLocalArray[], object?> tlsBuckets in _allTlsBuckets)
            {
                ThreadLocalArray[] buckets = tlsBuckets.Key;
                for (int i = 0; i < buckets.Length; i++)
                {
                    if (buckets[i].Channel is null)
                    {
                        continue;
                    }

                    // We treat 0 to mean it hasn't yet been seen in a Trim call. In the very rare case where Trim records 0,
                    // it'll take an extra Trim call to remove the array.
                    int lastSeen = buckets[i].MillisecondsTimeStamp;
                    if (lastSeen == 0)
                    {
                        buckets[i].MillisecondsTimeStamp = currentMilliseconds;
                    }
                    else if ((currentMilliseconds - lastSeen) >= millisecondsThreshold)
                    {
                        // Time noticeably wrapped, or we've surpassed the threshold.
                        // Clear out the array, and log its being trimmed if desired.
                        Interlocked.Exchange(ref buckets[i].Channel, null);
                    }
                }
            }
        }

        return true;
    }

    private ThreadLocalArray[] InitializeTlsBucketsAndTrimming()
    {
        Debug.Assert(t_tlsBuckets is null, $"Non-null {nameof(t_tlsBuckets)}");

        var tlsBuckets = new ThreadLocalArray[NumBuckets];
        t_tlsBuckets = tlsBuckets;

        _allTlsBuckets.Add(tlsBuckets, null);
        if (Interlocked.Exchange(ref _trimCallbackCreated, 1) == 0)
        {
            Gen2GcCallback.Register(s => ((TlsOverPerCoreLockedStacksBoundedChannelPool<T>)s).Trim(), this);
        }

        return tlsBuckets;
    }

    /// <summary>Stores a set of stacks of arrays, with one stack per core.</summary>
    private sealed class PerCoreLockedStacks
    {
        /// <summary>Number of locked stacks to employ.</summary>
        private static readonly int s_lockedStackCount = Math.Min(Environment.ProcessorCount, MaxPerCorePerArraySizeStacks);
        /// <summary>The stacks.</summary>
        private readonly LockedStack[] _perCoreStacks;

        /// <summary>Initializes the stacks.</summary>
        public PerCoreLockedStacks()
        {
            // Create the stacks.  We create as many as there are processors, limited by our max.
            var stacks = new LockedStack[s_lockedStackCount];
            for (int i = 0; i < stacks.Length; i++)
            {
                stacks[i] = new LockedStack();
            }
            _perCoreStacks = stacks;
        }

        /// <summary>Try to push the array into the stacks. If each is full when it's tested, the array will be dropped.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryPush(BoundedChannel<T> array)
        {
            // Try to push on to the associated stack first.  If that fails,
            // round-robin through the other stacks.
            LockedStack[] stacks = _perCoreStacks;
            int index = (int)((uint)Thread.GetCurrentProcessorId() % (uint)s_lockedStackCount); // mod by constant in tier 1
            for (int i = 0; i < stacks.Length; i++)
            {
                if (stacks[index].TryPush(array)) return true;
                if (++index == stacks.Length) index = 0;
            }

            return false;
        }

        /// <summary>Try to get an array from the stacks.  If each is empty when it's tested, null will be returned.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public BoundedChannel<T>? TryPop()
        {
            // Try to pop from the associated stack first.  If that fails, round-robin through the other stacks.
            BoundedChannel<T>? arr;
            LockedStack[] stacks = _perCoreStacks;
            int index = (int)((uint)Thread.GetCurrentProcessorId() % (uint)s_lockedStackCount); // mod by constant in tier 1
            for (int i = 0; i < stacks.Length; i++)
            {
                if ((arr = stacks[index].TryPop()) is not null) return arr;
                if (++index == stacks.Length) index = 0;
            }
            return null;
        }

        public void Trim(int currentMilliseconds, int id, GCHelper.MemoryPressure pressure, int bucketSize)
        {
            LockedStack[] stacks = _perCoreStacks;
            for (int i = 0; i < stacks.Length; i++)
            {
                stacks[i].Trim(currentMilliseconds, id, pressure, bucketSize);
            }
        }
    }

    /// <summary>Provides a simple, bounded stack of arrays, protected by a lock.</summary>
    private sealed class LockedStack
    {
        /// <summary>The arrays in the stack.</summary>
        private readonly BoundedChannel<T>?[] _arrays = new BoundedChannel<T>[MaxBuffersPerArraySizePerCore];
        /// <summary>Number of arrays stored in <see cref="_arrays"/>.</summary>
        private int _count;
        /// <summary>Timestamp set by Trim when it sees this as 0.</summary>
        private int _millisecondsTimestamp;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryPush(BoundedChannel<T> array)
        {
            bool enqueued = false;
            Monitor.Enter(this);
            BoundedChannel<T>?[] arrays = _arrays;
            int count = _count;
            if ((uint)count < (uint)arrays.Length)
            {
                if (count == 0)
                {
                    // Reset the time stamp now that we're transitioning from empty to non-empty.
                    // Trim will see this as 0 and initialize it to the current time when Trim is called.
                    _millisecondsTimestamp = 0;
                }

                arrays[count] = array;
                _count = count + 1;
                enqueued = true;
            }
            Monitor.Exit(this);
            return enqueued;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public BoundedChannel<T>? TryPop()
        {
            BoundedChannel<T>? arr = null;
            Monitor.Enter(this);
            BoundedChannel<T>?[] arrays = _arrays;
            int count = _count - 1;
            if ((uint)count < (uint)arrays.Length)
            {
                arr = arrays[count];
                arrays[count] = null;
                _count = count;
            }
            Monitor.Exit(this);
            return arr;
        }

        public void Trim(int currentMilliseconds, int id, GCHelper.MemoryPressure pressure, int bucketSize)
        {
            const int StackTrimAfterMS = 60 * 1000;                        // Trim after 60 seconds for low/moderate pressure
            const int StackHighTrimAfterMS = 10 * 1000;                    // Trim after 10 seconds for high pressure
            const int StackLowTrimCount = 1;                                // Trim one item when pressure is low
            const int StackMediumTrimCount = 2;                             // Trim two items when pressure is moderate
            const int StackHighTrimCount = MaxBuffersPerArraySizePerCore;   // Trim all items when pressure is high
            const int StackLargeBucket = 16384;                             // If the bucket is larger than this we'll trim an extra when under high pressure
            const int StackModerateTypeSize = 16;                           // If T is larger than this we'll trim an extra when under high pressure
            const int StackLargeTypeSize = 32;                              // If T is larger than this we'll trim an extra (additional) when under high pressure

            if (_count == 0)
            {
                return;
            }

            int trimMilliseconds = pressure == GCHelper.MemoryPressure.High ? StackHighTrimAfterMS : StackTrimAfterMS;

            lock (this)
            {
                if (_count == 0)
                {
                    return;
                }

                if (_millisecondsTimestamp == 0)
                {
                    _millisecondsTimestamp = currentMilliseconds;
                    return;
                }

                if ((currentMilliseconds - _millisecondsTimestamp) <= trimMilliseconds)
                {
                    return;
                }

                // We've elapsed enough time since the first item went into the stack.
                // Drop the top item so it can be collected and make the stack look a little newer.

                int trimCount = StackLowTrimCount;
                switch (pressure)
                {
                    case GCHelper.MemoryPressure.High:
                        trimCount = StackHighTrimCount;

                        // When pressure is high, aggressively trim larger arrays.
                        if (bucketSize > StackLargeBucket)
                        {
                            trimCount++;
                        }
                        if (Unsafe.SizeOf<T>() > StackModerateTypeSize)
                        {
                            trimCount++;
                        }
                        if (Unsafe.SizeOf<T>() > StackLargeTypeSize)
                        {
                            trimCount++;
                        }
                        break;

                    case GCHelper.MemoryPressure.Medium:
                        trimCount = StackMediumTrimCount;
                        break;
                }

                while (_count > 0 && trimCount-- > 0)
                {
                    BoundedChannel<T>? array = _arrays[--_count];
                    Debug.Assert(array is not null, "No nulls should have been present in slots < _count.");
                    _arrays[_count] = null;
                }

                _millisecondsTimestamp = _count > 0 ?
                    _millisecondsTimestamp + (trimMilliseconds / 4) : // Give the remaining items a bit more time
                    0;
            }
        }
    }

    /// <summary>Wrapper for arrays stored in ThreadStatic buckets.</summary>
    private struct ThreadLocalArray
    {
        /// <summary>The stored array.</summary>
        public BoundedChannel<T>? Channel;
        /// <summary>Environment.TickCount timestamp for when this array was observed by Trim.</summary>
        public int MillisecondsTimeStamp;

        public ThreadLocalArray(BoundedChannel<T> channel)
        {
            Channel = channel;
            MillisecondsTimeStamp = 0;
        }
    }

    [DoesNotReturn, DebuggerStepThrough, MethodImpl(MethodImplOptions.NoInlining)]
    private static void ThrowChannelNull() {
        throw new ArgumentNullException("channel");
    }

    [DoesNotReturn, DebuggerStepThrough, MethodImpl(MethodImplOptions.NoInlining)]
    private static void ThrowChannelNotOfPool() {
        throw new ArgumentException("The channel does not belong to the bool", "channel");
    }

    [DoesNotReturn, DebuggerStepThrough, MethodImpl(MethodImplOptions.NoInlining)]
    private static void ThrowChannelCompleted() {
        throw new ArgumentException("Cannot add a completed channel to the pool", "channel");
    }
}

