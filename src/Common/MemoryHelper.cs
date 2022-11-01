using System.Runtime.CompilerServices;

namespace SurrealDB.Common;

internal static class MemoryHelper {
    /// <summary>
    /// Returns a reference to the 0th element of <paramref name="array"/>. If the array is empty, returns a reference to where the 0th element
    /// would have been stored. Such a reference may be used for pinning but must never be dereferenced.
    /// </summary>
    /// <exception cref="NullReferenceException"><paramref name="array"/> is <see langword="null"/>.</exception>
    /// <remarks>
    /// This method does not perform array variance checks. The caller must manually perform any array variance checks
    /// if the caller wishes to write to the returned reference.
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ref T GetArrayDataReference<T>(T[] array) {
#if !NET6_0_OR_GREATER
        return ref Unsafe.As<byte, T>(ref Unsafe.As<RawArrayData>(array).Data);
#else
        return ref System.Runtime.InteropServices.MemoryMarshal.GetArrayDataReference(array);
#endif
    }


    // CLR arrays are laid out in memory as follows (multidimensional array bounds are optional):
    // [ sync block || pMethodTable || num components || MD array bounds || array data .. ]
    //                 ^               ^                 ^                  ^ returned reference
    //                 |               |                 \-- ref Unsafe.As<RawArrayData>(array).Data
    //                 \-- array       \-- ref Unsafe.As<RawData>(array).Data
    // The BaseSize of an array includes all the fields before the array data,
    // including the sync block and method table. The reference to RawData.Data
    // points at the number of components, skipping over these two pointer-sized fields.
    internal sealed class RawArrayData
    {
        public uint Length; // Array._numComponents padded to IntPtr
#if TARGET_64BIT
        public uint Padding;
#endif
        public byte Data;
    }
}
