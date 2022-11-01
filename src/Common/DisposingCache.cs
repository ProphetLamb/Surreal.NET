using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace SurrealDB.Common;

/// <summary>Thread-safe sliding cache that disposed values when evicting</summary>
internal sealed class DisposingCache<K, V>
    where K : notnull
    where V : IDisposable {
    private int _evictLock;
    private long _lastEvictedTicks; // timestamp of latest eviction operation.
    private readonly long _evictionIntervalTicks; // min timespan needed to trigger a new evict operation.
    private readonly long _slidingExpirationTicks; // max timespan allowed for cache entries to remain inactive.
    private readonly ConcurrentDictionary<K, CacheEntry> _cache = new();

    public DisposingCache(TimeSpan slidingExpiration, TimeSpan evictionInterval) {
        _slidingExpirationTicks = slidingExpiration.Ticks;
        _evictionIntervalTicks = evictionInterval.Ticks;
        _lastEvictedTicks = DateTime.UtcNow.Ticks;
    }

    public V GetOrAdd(K key, Func<K, V> valueFactory) {
        CacheEntry entry = _cache.GetOrAdd(key, static (key, valueFactory) => new(valueFactory(key)), valueFactory);
        EnsureSlidingEviction(entry);

        return entry.Value;
    }

    public bool TryAdd(K key, V value) {
        CacheEntry entry = new(value);
        bool added = _cache.TryAdd(key, entry);
        EnsureSlidingEviction(entry);

        return added;
    }

    public bool TryRemove(K key, [MaybeNullWhen(false)] out V value) {
        if (_cache.TryRemove(key, out var entry)) {
            EnsureSlidingEviction(entry);
            value = entry.Value;
            return true;
        }

        value = default;
        return false;
    }

    public bool TryGetValue(K key, [MaybeNullWhen(false)] out V value) {
        if (_cache.TryRemove(key, out var entry)) {
            EnsureSlidingEviction(entry);
            value = entry.Value;
            return true;
        }

        value = default;
        return false;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void EnsureSlidingEviction(CacheEntry entry) {
        long utcNowTicks = DateTime.UtcNow.Ticks;
        Volatile.Write(ref entry.LastUsedTicks, utcNowTicks);

        if (utcNowTicks - Volatile.Read(ref _lastEvictedTicks) >= _evictionIntervalTicks) {
            if (Interlocked.CompareExchange(ref _evictLock, 1, 0) == 0) {
                if (utcNowTicks - _lastEvictedTicks >= _evictionIntervalTicks) {
                    EvictStaleCacheEntries(utcNowTicks);
                    Volatile.Write(ref _lastEvictedTicks, utcNowTicks);
                }

                Volatile.Write(ref _evictLock, 0);
            }
        }
    }

    public void Clear() {
        _cache.Clear();
        _lastEvictedTicks = DateTime.UtcNow.Ticks;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private void EvictStaleCacheEntries(long utcNowTicks) {
        foreach (KeyValuePair<K, CacheEntry> kvp in _cache) {
            if (utcNowTicks - Volatile.Read(ref kvp.Value.LastUsedTicks) >= _slidingExpirationTicks) {
                if (_cache.TryRemove(kvp.Key, out var entry)) {
                    entry.Value.Dispose();
                }
            }
        }
    }

    private sealed class CacheEntry {
        public readonly V Value;
        public long LastUsedTicks;

        public CacheEntry(V value) {
            Value = value;
        }
    }
}
