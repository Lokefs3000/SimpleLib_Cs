using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace SimpleLib.Utility
{
    public static class StreamUtility
    {
        public static unsafe void Deserialize<TStruct>(Stream stream, out TStruct @struct)
            where TStruct : unmanaged
        {
            Unsafe.SkipInit(out @struct);

            byte* memory = (byte*)NativeMemory.Alloc((nuint)sizeof(TStruct));
            Span<byte> raw = new Span<byte>(memory, sizeof(TStruct));

            stream.ReadExactly(raw);

            @struct = new TStruct();
            fixed (TStruct* ptr = &@struct)
            {
                NativeMemory.Copy(memory, ptr, (nuint)raw.Length);
            }

            NativeMemory.Free(memory);
        }

        public static unsafe void Serialize<TStruct>(Stream stream, ref TStruct @struct)
           where TStruct : unmanaged
        {
            byte* memory = (byte*)NativeMemory.Alloc((nuint)sizeof(TStruct));
            Span<byte> raw = new Span<byte>(memory, sizeof(TStruct));

            fixed (TStruct* ptr = &@struct)
            {
                NativeMemory.Copy(ptr, memory, (nuint)raw.Length);
            }

            stream.Write(raw);

            NativeMemory.Free(memory);
        }

        public static unsafe void Serialize<TGeneric>(Stream stream, TGeneric @struct)
          where TGeneric : unmanaged
        {
            byte* memory = (byte*)NativeMemory.Alloc((nuint)sizeof(TGeneric));
            Span<byte> raw = new Span<byte>(memory, sizeof(TGeneric));

            {
                TGeneric* ptr = &@struct;
                NativeMemory.Copy(ptr, memory, (nuint)raw.Length);
            }

            stream.Write(raw);

            NativeMemory.Free(memory);
        }

        public static unsafe void Serialize<TGeneric>(Stream stream, TGeneric @struct, int size)
          where TGeneric : unmanaged
        {
            byte* memory = (byte*)NativeMemory.Alloc((nuint)sizeof(TGeneric));
            Span<byte> raw = new Span<byte>(memory, sizeof(TGeneric));

            {
                TGeneric* ptr = &@struct;
                NativeMemory.Copy(ptr, memory, (nuint)Math.Min(raw.Length, size));
            }

            stream.Write(raw);

            NativeMemory.Free(memory);
        }
    }
}
