using System.Buffers;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;

namespace SimpleLib.Utility
{
    public static class CastingUtility
    {
        public static unsafe void Cast<TObject>(Span<byte> span, uint offset, out TObject @object)
            where TObject : unmanaged
        {
            Unsafe.SkipInit(out @object);

            if (sizeof(TObject) + offset > span.Length)
            {
                throw new ArgumentException("Specified data region is larger then Span!");
            }

            @object = new TObject();
            fixed (byte* ptr00 = span)
            {
                fixed (TObject* ptr1 = &@object)
                {
                    NativeMemory.Copy(ptr00 + offset, ptr1, (nuint)sizeof(TObject));
                }
            }
        }

        public static unsafe void Cast<TObject>(Span<byte> span, uint offset, out TObject @object, uint size)
            where TObject : unmanaged
        {
            Unsafe.SkipInit(out @object);

            if (size + offset > span.Length)
            {
                throw new ArgumentException("Specified data region is larger then Span!");
            }

            if (size > sizeof(TObject))
            {
                throw new ArgumentException("Specified size cannot be larger then casting object size!");
            }

            @object = new TObject();
            fixed (byte* ptr00 = span)
            {
                fixed (TObject* ptr1 = &@object)
                {
                    NativeMemory.Copy(ptr00 + offset, ptr1, size);
                }
            }
        }

        public static unsafe void ReadString(Span<byte> span, uint offset, uint length, out string @string)
        {
            Unsafe.SkipInit(out @string);

            if (length + offset > span.Length)
            {
                throw new ArgumentException("Specified data region is larger then Span!");
            }

            fixed (byte* ptr00 = span)
            {
                @string = Encoding.UTF8.GetString(ptr00 + offset, (int)length);
            }
        }

        public static unsafe void Cast<TObject>(ReadOnlyMemory<byte> span, uint offset, out TObject @object)
            where TObject : unmanaged
        {
            Unsafe.SkipInit(out @object);

            if (sizeof(TObject) + offset > span.Length)
            {
                throw new ArgumentException("Specified data region is larger then Span!");
            }

            @object = new TObject();

            using MemoryHandle handle = span.Pin();
            fixed (TObject* ptr1 = &@object)
            {
                NativeMemory.Copy(((byte*)handle.Pointer) + offset, ptr1, (nuint)sizeof(TObject));
            }
        }

        public static unsafe void Cast<TObject>(ReadOnlyMemory<byte> span, uint offset, out TObject @object, uint size)
            where TObject : unmanaged
        {
            Unsafe.SkipInit(out @object);

            if (size + offset > span.Length)
            {
                throw new ArgumentException("Specified data region is larger then Span!");
            }

            if (size > sizeof(TObject))
            {
                throw new ArgumentException("Specified size cannot be larger then casting object size!");
            }

            @object = new TObject();

            using MemoryHandle handle = span.Pin();
            fixed (TObject* ptr1 = &@object)
            {
                NativeMemory.Copy(((byte*)handle.Pointer) + offset, ptr1, size);
            }
        }

        public static unsafe void ReadString(ReadOnlyMemory<byte> span, uint offset, uint length, out string @string)
        {
            Unsafe.SkipInit(out @string);

            if (length + offset > span.Length)
            {
                throw new ArgumentException("Specified data region is larger then Span!");
            }

            using MemoryHandle handle = span.Pin();
            @string = Encoding.UTF8.GetString(((byte*)handle.Pointer) + offset, (int)length);
        }
    }
}
