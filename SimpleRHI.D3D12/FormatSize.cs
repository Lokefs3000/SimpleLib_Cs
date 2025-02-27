using System.Runtime.CompilerServices;
using Vortice.DXGI;

namespace SimpleRHI.D3D12
{
    internal static class FormatSize
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static uint GetSize(GfxValueType vt)
        {
            switch (vt)
            {
                case GfxValueType.Single1: return 4;
                case GfxValueType.Single2: return 8;
                case GfxValueType.Single3: return 12;
                case GfxValueType.Single4: return 16;
                case GfxValueType.Int1: return 4;
                case GfxValueType.Int2: return 8;
                case GfxValueType.Int3: return 12;
                case GfxValueType.Int4: return 16;
                case GfxValueType.UInt1: return 4;
                case GfxValueType.UInt2: return 8;
                case GfxValueType.UInt3: return 12;
                case GfxValueType.UInt4: return 16;
                default: return 0;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Format GetSizeFormat(GfxValueType vt)
        {
            switch (vt)
            {
                case GfxValueType.Single1: return Format.R32_Float;
                case GfxValueType.Single2: return Format.R32G32_Float;
                case GfxValueType.Single3: return Format.R32G32B32_Float;
                case GfxValueType.Single4: return Format.R32G32B32A32_Float;
                case GfxValueType.Int1: return Format.R32_SInt;
                case GfxValueType.Int2: return Format.R32G32_SInt;
                case GfxValueType.Int3: return Format.R32G32B32_SInt;
                case GfxValueType.Int4: return Format.R32G32B32A32_SInt;
                case GfxValueType.UInt1: return Format.R32_UInt;
                case GfxValueType.UInt2: return Format.R32G32_UInt;
                case GfxValueType.UInt3: return Format.R32G32B32_UInt;
                case GfxValueType.UInt4: return Format.R32G32B32A32_UInt;
                default: return Format.Unknown;
            }
        }
    }
}
