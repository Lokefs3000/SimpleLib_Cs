using SimpleLib.Debugging;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace SimpleLib.Utility
{
    public unsafe class UnsafeList<T> : IDisposable
        where T : unmanaged
    {
        public string Name = "UnsafeList<" + typeof(T).Name + ">";

        public nint Pointer { get; private set; }
        public uint Count { get; private set; }
        public uint Capacity { get; private set; }

        public static readonly uint Stride = (uint)Unsafe.SizeOf<T>();

        public UnsafeList(uint capacity = 32u)
        {
            Pointer = nint.Zero;
            Count = 0;
            Capacity = capacity;
        }

        public void Dispose()
        {
            if (Pointer != nint.Zero)
                NativeMemory.Free(Pointer.ToPointer());
            MemoryCounter.IncrementCounter(Name, Stride * Capacity);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Add(T item)
        {
            if (Count + 1 >= Capacity || Pointer == nint.Zero)
                ResizeToFit(Count + 1u);

            ((T*)Pointer)[Count++] = item;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void AddNoResize(T item)
        {
            ((T*)Pointer)[Count++] = item;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Ensure(uint count)
        {
            if (Count + count + 1 >= Capacity || Pointer == nint.Zero)
                ResizeToFit(Count + count + 1u);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Clear()
        {
            Count = 0;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void CopyNativeTo(nint dataPointer)
        {
            NativeMemory.Copy(Pointer.ToPointer(), dataPointer.ToPointer(), Count * Stride);
        }

        private void ResizeToFit(uint minCapacity)
        {
            uint nextCapacity = Math.Max((uint)((Capacity + 1) * 2), 8);
            while (nextCapacity < minCapacity)
            {
                nextCapacity *= 2;
            }

            nint newBuffer = (nint)NativeMemory.Alloc((nuint)(Stride * nextCapacity));
            if (Pointer != nint.Zero)
            {
                NativeMemory.Copy(Pointer.ToPointer(), newBuffer.ToPointer(), Capacity * Stride);
                MemoryCounter.DecrementCounter(Name, Stride * Capacity);
                NativeMemory.Free(Pointer.ToPointer());
            }

            Pointer = newBuffer;

            MemoryCounter.IncrementCounter(Name, Stride * nextCapacity);

            Capacity = nextCapacity;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ref T AsRef(uint index)
        {
            if (index > Capacity)
                throw new ArgumentOutOfRangeException("index");
            return ref Unsafe.AsRef<T>(&((T*)Pointer)[index]);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Span<T> AsSpan()
        {
            return new Span<T>((void*)Pointer, (int)Count);
        }

        public T this[uint index]
        {
            get => ((T*)Pointer)[index];
            set => ((T*)Pointer)[index] = value;
        }

        public nint Last => (nint)(&((T*)Pointer)[Count - 1]);
    }
}
