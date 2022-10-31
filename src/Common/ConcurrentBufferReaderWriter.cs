using System.Buffers;

namespace SurrealDB.Common;

public sealed class ConcurrentBufferReaderWriter<T> {
    private readonly ArrayBufferWriter<T> _buf = new();
    private readonly ReaderWriterLockSlim _lock = new(LockRecursionPolicy.SupportsRecursion);
    /// <summary>State used by multiple threads. Only interact via <see cref="Interlocked"/> methods!</summary>
    private int _readerPosition;

    public WriteHandle Write() => new(this);

    public ReadHandle Read() => new(this);

    /// <summary>Allows writing the buffer. Proxy for <see cref="IBufferWriter{T}"/>.</summary>
    public readonly ref struct WriteHandle {
        private readonly ConcurrentBufferReaderWriter<T> _owner;

        internal WriteHandle(ConcurrentBufferReaderWriter<T> owner) {
            _owner = owner;
            _owner._lock.EnterWriteLock();
        }

        public ReadOnlyMemory<T> WrittenMemory => _owner._buf.WrittenMemory;
        public ReadOnlySpan<T> WrittenSpan => _owner._buf.WrittenSpan;
        public int WrittenCount => _owner._buf.WrittenCount;
        public int Capacity => _owner._buf.Capacity;
        public int FreeCapacity => _owner._buf.FreeCapacity;

        public void Clear() => _owner._buf.Clear();

        public void Advance(int count) => _owner._buf.Advance(count);

        public Memory<T> GetMemory(int sizeHint = 0) => _owner._buf.GetMemory(sizeHint);

        public Span<T> GetSpan(int sizeHint = 0) => _owner._buf.GetSpan(sizeHint);

        public void Dispose() {
            _owner._lock.ExitWriteLock();
        }
    }

    /// <summary>Allows concurrent reading from the buffer.</summary>
    public readonly ref struct ReadHandle {
        private readonly ConcurrentBufferReaderWriter<T> _owner;

        internal ReadHandle(ConcurrentBufferReaderWriter<T> owner) {
            _owner = owner;
            _owner._lock.EnterReadLock();
        }

        /// <summary>Reads a section of memory from the buffer</summary>
        /// <param name="expectedSize">The maximum expected amount of memory read.</param>
        public ReadOnlyMemory<T> ReadMemory(int expectedSize) {
            // [THEADSAFE] increment the position
            int newPosition = Interlocked.Add(ref _owner._readerPosition, expectedSize);
            ReadOnlyMemory<T> available = _owner._buf.WrittenMemory;
            int start = newPosition - expectedSize;
            int end = Math.Min(available.Length, newPosition);
            return (nuint)start <= (nuint)available.Length ? available.Slice(start, end - start) : default;
        }

        /// <inheritdoc cref="ReadMemory"/>
        public ReadOnlySpan<T> ReadSpan(int expectedSize) {
            // [THEADSAFE] increment the position
            int newPosition = Interlocked.Add(ref _owner._readerPosition, expectedSize);
            ReadOnlySpan<T> available = _owner._buf.WrittenSpan;
            int start = newPosition - expectedSize;
            int end = Math.Min(available.Length, newPosition);
            return (nuint)start <= (nuint)available.Length ? available.Slice(start, end - start) : default;
        }

        /// <summary>Reads at most the size of <paramref name="destination"/> from the buffer, and writes it to the <paramref name="destination"/>.</summary>
        /// <param name="destination">Where the elements are written to.</param>
        /// <returns>The number of elements read and also written to the <paramref name="destination"/>.</returns>
        public int CopyTo(Span<T> destination) {
            var source = ReadSpan(destination.Length);
            source.CopyTo(destination);
            return source.Length;
        }

        public void Dispose() {
            _owner._lock.ExitReadLock();
        }
    }
}


