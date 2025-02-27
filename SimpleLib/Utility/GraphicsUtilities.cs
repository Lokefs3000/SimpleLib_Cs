using SimpleRHI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;
using System.Text;
using System.Threading.Tasks;

namespace SimpleLib.Utility
{
    //what genius thought to name this "Utilities" instead of the agreed upon "Utility"?
    //oh wait that would be me (@Lokefs3000)..
    public static class GraphicsUtilities
    {
        //If the format is compressed "elementCount" becomes block width
        //Not fully acurrate as this is to provide strides
        public static (uint ElementSize, uint ElementCount) GetElementSize(GfxFormat format)
        {
            switch (format)
            {
                case GfxFormat.Unkown:                     return (0u, 0u);
                case GfxFormat.R32G32B32A32_TYPELESS:      return (4u, 4u);
                case GfxFormat.R32G32B32A32_FLOAT:         return (4u, 4u);
                case GfxFormat.R32G32B32A32_UINT:          return (4u, 4u);
                case GfxFormat.R32G32B32A32_SINT:          return (4u, 4u);
                case GfxFormat.R32G32B32_TYPELESS:         return (4u, 3u);
                case GfxFormat.R32G32B32_FLOAT:            return (4u, 3u);
                case GfxFormat.R32G32B32_UINT:             return (4u, 3u);
                case GfxFormat.R32G32B32_SINT:             return (4u, 3u);
                case GfxFormat.R16G16B16A16_TYPELESS:      return (2u, 4u);
                case GfxFormat.R16G16B16A16_FLOAT:         return (2u, 4u);
                case GfxFormat.R16G16B16A16_UNORM:         return (2u, 4u);
                case GfxFormat.R16G16B16A16_UINT:          return (2u, 4u);
                case GfxFormat.R16G16B16A16_SNORM:         return (2u, 4u);
                case GfxFormat.R16G16B16A16_SINT:          return (2u, 4u);
                case GfxFormat.R32G32_TYPELESS:            return (4u, 2u);
                case GfxFormat.R32G32_FLOAT:               return (4u, 2u);
                case GfxFormat.R32G32_UINT:                return (4u, 2u);
                case GfxFormat.R32G32_SINT:                return (4u, 2u);
                case GfxFormat.R32G8X24_TYPELESS:          return (4u, 2u); //not accurate
                case GfxFormat.D32_FLOAT_S8X24_UINT:       return (4u, 2u); //not accurate
                case GfxFormat.R32_FLOAT_X8X24_TYPELESS:   return (4u, 2u); //not accurate
                case GfxFormat.X32_TYPELESS_G8X24_UINT:    return (4u, 2u); //not accurate
                case GfxFormat.R10G10B10A2_TYPELESS:       return (1u, 4u);
                case GfxFormat.R10G10B10A2_UNORM:          return (1u, 4u);
                case GfxFormat.R10G10B10A2_UINT:           return (1u, 4u);
                case GfxFormat.R11G11B10_FLOAT:            return (1u, 4u); //not accurate
                case GfxFormat.R8G8B8A8_TYPELESS:          return (1u, 4u);
                case GfxFormat.R8G8B8A8_UNORM:             return (1u, 4u);
                case GfxFormat.R8G8B8A8_UNORM_SRGB:        return (1u, 4u);
                case GfxFormat.R8G8B8A8_UINT:              return (1u, 4u);
                case GfxFormat.R8G8B8A8_SNORM:             return (1u, 4u);
                case GfxFormat.R8G8B8A8_SINT:              return (1u, 4u);
                case GfxFormat.R16G16_TYPELESS:            return (2u, 2u);
                case GfxFormat.R16G16_FLOAT:               return (2u, 2u);
                case GfxFormat.R16G16_UNORM:               return (2u, 2u);
                case GfxFormat.R16G16_UINT:                return (2u, 2u);
                case GfxFormat.R16G16_SNORM:               return (2u, 2u);
                case GfxFormat.R16G16_SINT:                return (2u, 2u);
                case GfxFormat.R32_TYPELESS:               return (4u, 1u);
                case GfxFormat.D32_FLOAT:                  return (4u, 1u);
                case GfxFormat.R32_FLOAT:                  return (4u, 1u);
                case GfxFormat.R32_UINT:                   return (4u, 1u);
                case GfxFormat.R32_SINT:                   return (4u, 1u);
                case GfxFormat.R24G8_TYPELESS:             return (2u, 2u); //not accurate
                case GfxFormat.D24_UNORM_S8_UINT:          return (2u, 2u); //not accurate
                case GfxFormat.R24_UNORM_X8_TYPELESS:      return (2u, 2u); //not accurate
                case GfxFormat.X24_TYPELESS_G8_UINT:       return (2u, 2u); //not accurate
                case GfxFormat.R8G8_TYPELESS:              return (1u, 2u);
                case GfxFormat.R8G8_UNORM:                 return (1u, 2u);
                case GfxFormat.R8G8_UINT:                  return (1u, 2u);
                case GfxFormat.R8G8_SNORM:                 return (1u, 2u);
                case GfxFormat.R8G8_SINT:                  return (1u, 2u);
                case GfxFormat.R16_TYPELESS:               return (2u, 1u);
                case GfxFormat.R16_FLOAT:                  return (2u, 1u);
                case GfxFormat.D16_UNORM:                  return (2u, 1u);
                case GfxFormat.R16_UNORM:                  return (2u, 1u);
                case GfxFormat.R16_UINT:                   return (2u, 1u);
                case GfxFormat.R16_SNORM:                  return (2u, 1u);
                case GfxFormat.R16_SINT:                   return (2u, 1u);
                case GfxFormat.R8_TYPELESS:                return (1u, 1u);
                case GfxFormat.R8_UNORM:                   return (1u, 1u);
                case GfxFormat.R8_UINT:                    return (1u, 1u);
                case GfxFormat.R8_SNORM:                   return (1u, 1u);
                case GfxFormat.R8_SINT:                    return (1u, 1u);
                case GfxFormat.A8_UNORM:                   return (1u, 1u);
                case GfxFormat.R1_UNORM:                   return (1u, 1u);
                case GfxFormat.R9G9B9E5_SHAREDEXP:         return (1u, 2u); //not accurate
                case GfxFormat.R8G8_B8G8_UNORM:            return (1u, 4u);
                case GfxFormat.G8R8_G8B8_UNORM:            return (1u, 4u);
                case GfxFormat.BC1_TYPELESS:               return (8u, 4u);
                case GfxFormat.BC1_UNORM:                  return (8u, 4u);
                case GfxFormat.BC1_UNORM_SRGB:             return (8u, 4u);
                case GfxFormat.BC2_TYPELESS:               return (16u, 4u);
                case GfxFormat.BC2_UNORM:                  return (16u, 4u);
                case GfxFormat.BC2_UNORM_SRGB:             return (16u, 4u);
                case GfxFormat.BC3_TYPELESS:               return (16u, 4u);
                case GfxFormat.BC3_UNORM:                  return (16u, 4u);
                case GfxFormat.BC3_UNORM_SRGB:             return (16u, 4u);
                case GfxFormat.BC4_TYPELESS:               return (8u, 4u);
                case GfxFormat.BC4_UNORM:                  return (8u, 4u);
                case GfxFormat.BC4_SNORM:                  return (8u, 4u);
                case GfxFormat.BC5_TYPELESS:               return (16u, 4u);
                case GfxFormat.BC5_UNORM:                  return (16u, 4u);
                case GfxFormat.BC5_SNORM:                  return (16u, 4u);
                case GfxFormat.B5G6R5_UNORM:               return (1u, 3u);
                case GfxFormat.B5G5R5A1_UNORM:             return (1u, 4u);
                case GfxFormat.B8G8R8A8_UNORM:             return (1u, 4u);
                case GfxFormat.B8G8R8X8_UNORM:             return (1u, 4u);
                case GfxFormat.R10G10B10_XR_BIAS_A2_UNORM: return (1u, 4u);
                case GfxFormat.B8G8R8A8_TYPELESS:          return (1u, 4u);
                case GfxFormat.B8G8R8A8_UNORM_SRGB:        return (1u, 4u);
                case GfxFormat.B8G8R8X8_TYPELESS:          return (1u, 4u);
                case GfxFormat.B8G8R8X8_UNORM_SRGB:        return (1u, 4u);
                case GfxFormat.BC6H_TYPELESS:              return (16u, 4u);
                case GfxFormat.BC6H_UF16:                  return (16u, 4u);
                case GfxFormat.BC6H_SF16:                  return (16u, 4u);
                case GfxFormat.BC7_TYPELESS:               return (16u, 4u);
                case GfxFormat.BC7_UNORM:                  return (16u, 4u);
                case GfxFormat.BC7_UNORM_SRGB:             return (16u, 4u);
                default:                                   return (0u, 0u);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static uint GetStride(GfxFormat format)
        {
            var data = GetElementSize(format);
            return data.ElementSize * data.ElementCount;
        }

        public static ulong CalculateMemorySizeForMips(Vector3 size, GfxFormat format, int mipLevels)
        {
            //performance test this
            //also verify result
            if (Vector128.IsHardwareAccelerated)
            {
                ulong memorySize = 0ul;

                Vector128<uint> whd = Vector128.Create((uint)size.X, (uint)size.Y, (uint)size.Z, 1u);
                whd = Vector128.Max(One, whd);

                for (int i = 0; i < mipLevels; i++)
                {
                    memorySize = whd.GetElement(0) * whd.GetElement(1) * whd.GetElement(2);

                    whd = Vector128.Divide(whd, Two);
                    whd = Vector128.Max(whd, One);
                }

                return memorySize * (ulong)GetStride(format);
            }
            else
            {
                ulong memorySize = 0ul;

                uint width = Math.Max((uint)size.X, 1u);
                uint height = Math.Max((uint)size.Y, 1u);
                uint depth = Math.Max((uint)size.Z, 1u);

                for (int i = 0; i < mipLevels; i++)
                {
                    memorySize += width * height * depth;

                    width = Math.Max(width / 2u, 1u);
                    height = Math.Max(width / 2u, 1u);
                    depth = Math.Max(width / 2u, 1u);
                }

                return memorySize * (ulong)GetStride(format);
            }
        }
        public static ulong CalculateMemorySizeForMip(Vector3 size, GfxFormat format, int mipLevel)
        {
            //performance test this
            //also verify result
            if (Vector128.IsHardwareAccelerated)
            {
                Vector128<uint> whd = Vector128.Create((uint)size.X, (uint)size.Y, (uint)size.Z, 1u);

                if (mipLevel > 0)
                {
                    uint mip2 = (uint)(2 << mipLevel);
                    whd = Vector128.Divide(whd, mip2);
                }

                whd = Vector128.Max(whd, One);
                return (ulong)whd.GetElement(0) * (ulong)whd.GetElement(1) * (ulong)whd.GetElement(2) * (ulong)GetStride(format);
            }
            else
            {
                uint width = (uint)size.X;
                uint height = (uint)size.Y;
                uint depth = (uint)size.Z;

                /*while (mipLevel-- > 0)
                {
                    width /= 2;
                    height /= 2;
                    depth /= 2;
                }*/

                if (mipLevel > 0)
                {
                    uint mip2 = (uint)(2 << mipLevel);
                    width /= mip2;
                    height /= mip2;
                    depth /= mip2;
                }

                return Math.Max(width, 1ul) * Math.Max(height, 1ul) * Math.Max(depth, 1ul) * (ulong)GetStride(format);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static uint PadSizeForAlignment(uint required, uint alignment)
        {
            return (required + (alignment - 1u)) & ~(alignment - 1u);
        }

        private static readonly Vector128<uint> One = Vector128.Create(1u);
        private static readonly Vector128<uint> Two = Vector128.Create(2u);

        public const uint TextureUploadAlignment = 256u;
    }
}
