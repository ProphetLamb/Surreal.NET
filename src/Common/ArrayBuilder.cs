// Source: https://raw.githubusercontent.com/dotnet/runtime/0f04d34ac4254f2ede46840300d1251ac65354d9/src/libraries/Common/src/System/Text/ValueStringBuilder.cs
// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

// Modified for generic types and not backed by a pool

using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace SurrealDB.Common;

internal struct ArrayBuilder<T>
{
    private T[]? _array;
    private int _pos;

    public ArrayBuilder(int initialCapacity)
    {
        _array = new T[initialCapacity];
        _pos = 0;
    }

    public int Length
    {
        get => _pos;
        set
        {
            Debug.Assert(value >= 0);
            Debug.Assert(value <= Raw.Length);
            _pos = value;
        }
    }

    public int Capacity => Raw.Length;

    public void EnsureCapacity(int capacity)
    {
        // This is not expected to be called this with negative capacity
        Debug.Assert(capacity >= 0);

        if ((uint)capacity > (uint)Raw.Length)
            Grow(capacity - _pos);
    }

    /// <summary>
    /// Get a pinnable reference to the builder.
    /// Does not ensure there is a null char after <see cref="Length"/>
    /// This overload is pattern matched in the C# 7.3+ compiler so you can omit
    /// the explicit method call, and write eg "fixed (char* c = builder)"
    /// </summary>
    public ref T GetPinnableReference()
    {
        return ref MemoryMarshal.GetReference(Raw);
    }

    public ref T this[int index]
    {
        get
        {
            Debug.Assert(index < _pos);
            return ref Raw[index];
        }
    }

    public override string ToString()
    {
        string s = Raw.Slice(0, _pos).ToString();
        return s;
    }

    /// <summary>Returns the underlying storage of the builder.</summary>
    public Span<T> Raw => _array.AsSpan();

    public ReadOnlySpan<T> AsSpan() => Raw.Slice(0, _pos);
    public ReadOnlySpan<T> AsSpan(int start) => Raw.Slice(start, _pos - start);
    public ReadOnlySpan<T> AsSpan(int start, int length) => Raw.Slice(start, length);

    public ArraySegment<T> AsSegment() => new(_array ?? Array.Empty<T>());
    public ArraySegment<T> AsSegment(int start) => new(_array ?? Array.Empty<T>(), start, _pos - start);
    public ArraySegment<T> AsSegment(int start, int length) => new(_array ?? Array.Empty<T>(), start, length);

    public bool TryCopyTo(Span<T> destination, out int written) {
        if (Raw.Slice(0, _pos).TryCopyTo(destination))
        {
            written = _pos;
            return true;
        }

        written = 0;
        return false;
    }

    public void Insert(int index, in T value, int count)
    {
        if (_pos > Raw.Length - count)
        {
            Grow(count);
        }

        int remaining = _pos - index;
        Raw.Slice(index, remaining).CopyTo(Raw.Slice(index + count));
        Raw.Slice(index, count).Fill(value);
        _pos += count;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Append(T c)
    {
        int pos = _pos;
        if ((uint)pos < (uint)Raw.Length)
        {
            Raw[pos] = c;
            _pos = pos + 1;
        }
        else
        {
            GrowAndAppend(c);
        }
    }

    public void Append(in T c, int count)
    {
        if (_pos > Raw.Length - count)
        {
            Grow(count);
        }

        Span<T> dst = Raw.Slice(_pos, count);
        for (int i = 0; i < dst.Length; i++)
        {
            dst[i] = c;
        }
        _pos += count;
    }

    public void Append(ReadOnlySpan<T> value)
    {
        int pos = _pos;
        if (pos > Raw.Length - value.Length)
        {
            Grow(value.Length);
        }

        value.CopyTo(Raw.Slice(_pos));
        _pos += value.Length;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Span<T> AppendSpan(int length)
    {
        int origPos = _pos;
        if (origPos > Raw.Length - length)
        {
            Grow(length);
        }

        _pos = origPos + length;
        return Raw.Slice(origPos, length);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private void GrowAndAppend(T c)
    {
        Grow(1);
        Append(c);
    }

    /// <summary>
    /// Resize the internal buffer either by doubling current buffer size or
    /// by adding <paramref name="additionalCapacityBeyondPos"/> to
    /// <see cref="_pos"/> whichever is greater.
    /// </summary>
    /// <param name="additionalCapacityBeyondPos">
    /// Number of chars requested beyond current position.
    /// </param>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private void Grow(int additionalCapacityBeyondPos)
    {
        Debug.Assert(additionalCapacityBeyondPos > 0);
        Debug.Assert(_pos > Raw.Length - additionalCapacityBeyondPos, "Grow called incorrectly, no resize is needed.");

        T[] newArray = new T[(int)Math.Max((uint)(_pos + additionalCapacityBeyondPos), (uint)Raw.Length * 2)];
        Raw.Slice(0, _pos).CopyTo(newArray);
        _array = newArray;
    }
}
