using System.Buffers;
using System.Runtime.InteropServices;

namespace SurrealDB.Common;

/// <summary>
/// <see cref="MemoryStream"/> associated with a owner, like <see cref="System.Buffers.IMemoryOwner{T}"/>
/// </summary>
public sealed class RentedMemoryStream : MemoryStream {
    private IDisposable? _owner;

    public RentedMemoryStream(IDisposable owner, ArraySegment<byte> buffer, bool writable, bool exposed)
        : base(buffer.Array!, buffer.Offset, buffer.Count, writable, exposed) {
        _owner = owner;
    }

    protected override void Dispose(bool disposing) {
        if (!disposing) {
            return;
        }

        base.Dispose();
        _owner?.Dispose();
        _owner = null;
    }

    public override void SetLength(long value) {
        int cap = Capacity;
        base.SetLength(value);
        if (cap != Capacity) {
            // Reallocated memory
            _owner?.Dispose();
            _owner = null;
        }
    }

    /// <inheritdoc cref="FromMemory(IDisposable,ReadOnlyMemory{byte},bool,bool)"/>
    public static MemoryStream FromMemory(IMemoryOwner<byte> owner, bool writable = false, bool exposed = false) {
        return FromMemory(owner, owner.Memory, writable, exposed);
    }

    /// <summary>
    /// Attempts to create a <see cref="RentedMemoryStream"/> with the owner and backing array of the <see cref="ReadOnlyMemory{byte}"/>.
    /// If that fails copies the memory and disposes the owner returning a <see cref="MemoryStream"/>; otherwise returns <see cref="RentedMemoryStream"/>,
    /// which disposed the owner when disposed itself.
    /// </summary>
    public static MemoryStream FromMemory(IDisposable owner, ReadOnlyMemory<byte> memory, bool writable = false, bool exposed = false) {
        // the array will always be accessible from the IMemoryOwner
        MemoryStream s;
        if (MemoryMarshal.TryGetArray(memory, out ArraySegment<byte> arr)) {
            s = new RentedMemoryStream(owner, arr, writable, exposed);
        } else {
            s = new MemoryStream(memory.ToArray(), 0, memory.Length, writable, exposed);
            // The owned memory wont be used, dispose the owner prematurely
            owner.Dispose();
        }

        return s;
    }
}
