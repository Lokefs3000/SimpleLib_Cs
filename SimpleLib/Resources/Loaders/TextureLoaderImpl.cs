using CommunityToolkit.HighPerformance;
using Schedulers;
using SimpleLib.Files;
using SimpleLib.Resources.Data;
using SimpleRHI;
using StbImageSharp;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace SimpleLib.Resources.Loaders
{
    internal class TextureLoaderImpl : IJob
    {
        public static readonly Queue<Payload> Pending = new Queue<Payload>();
        public static readonly TextureLoaderImpl Impl = new TextureLoaderImpl();

        private TextureLoaderImpl()
        {

        }

        public void Execute()
        {
            Payload payload;
            lock (Pending)
            {
                payload = Pending.Dequeue();
            }

            ReadOnlyMemory<byte> raw = payload.Filesystem.ReadBytes(payload.Object.Id);
            if (raw.IsEmpty)
            {
                LogTypes.Resources.Error("Buffer is empty for resource id: {a}!", payload.Object.Id);
                return;
            }

            using Stream stream = raw.AsStream();
            using BinaryReader br = new BinaryReader(stream);

            if (IsDDSFile(br))
                ReadAsDDS(payload.Device, payload.Object, br, raw);
            else
                ReadAsIMG(payload.Device, payload.Object, br);
        }

        private bool IsDDSFile(BinaryReader stream)
        {
            stream.BaseStream.Seek(0, SeekOrigin.Begin);
            return stream.ReadUInt32() == 0x20534444;
        }

        private void ReadAsDDS(IGfxDevice device, Texture texture, BinaryReader br, ReadOnlyMemory<byte> src)
        {
            /*
		BC7 - yes
		BC6S - yes
		BC6U - yes
		ASTC - no
		BC5u - yes
		BC4u - yes
		BC3 - yes
		BC3n - yes
		BC3n_agbr - no
		BC2 - yes
		BC1a - yes
		BC1 - yes
		_8a - yes
		_8l - yes
		_888_bgr - no
		_8888_bgra - yes
		_888x_bgrx - yes
		_888_rgb - no
		_8888_rgba - yes
		_16f - yes
		_16f16f - yes
		_16x4f - yes
		_32f - yes
		_32f32f - yes
		_32x4f - yes
			*/

            br.BaseStream.Seek(0, SeekOrigin.Begin);

            bool expandRGB = false;
            GfxFormat tex_format = GfxFormat.Unkown;

            uint magic = br.ReadUInt32();
            DDS_HEADER header = new DDS_HEADER(br);
            if (header.ddspf.fourCC == MAKEFOURCC('D', 'X', '1', '0'))
            {
                DDS_HEADER_DXT10 header10 = new DDS_HEADER_DXT10(br);
                switch (header10.dxgiFormat)
                {
                    case Vortice.DXGI.Format.BC6H_Uf16: { tex_format = GfxFormat.BC6H_UF16; break; }
                    case Vortice.DXGI.Format.BC6H_Sf16: { tex_format = GfxFormat.BC6H_SF16; break; }
                    case Vortice.DXGI.Format.BC7_UNorm: { tex_format = GfxFormat.BC7_UNORM; break; }
                    default: { throw new InvalidDataException($"Unkwon DX10 dxgi format: \"{header10.dxgiFormat}\"!"); };
                }
            }
            else
            {
                if (!PixelFormatConversionTable.TryGetValue(header.ddspf.fourCC, out tex_format))
                {
                    switch (header.ddspf.RGBBitCount)
                    {
                        case 8: { tex_format = GfxFormat.R8_UNORM; break; }
                        case 16: { tex_format = GfxFormat.R8G8_UNORM; break; }
                        case 32: { tex_format = GfxFormat.R8G8B8A8_UNORM; break; }
                        default: { throw new InvalidDataException("Unkown fourCC or rgb bit count!"); }
                    }
                }
            }

            long bufferOffset;
            unsafe
            {
                bufferOffset = sizeof(uint) + sizeof(DDS_HEADER) + ((header.ddspf.fourCC == MAKEFOURCC('D', 'X', '1', '0')) ? sizeof(DDS_HEADER_DXT10) : 0);
            }
            long bufferSize = br.BaseStream.Length - bufferOffset;

            br.BaseStream.Seek(bufferOffset, SeekOrigin.Begin);

            long elementSize = 0;
            long blockWidth = 0;
            switch (tex_format)
            {
                case GfxFormat.R8_UNORM: { elementSize = 1L; break; }
                case GfxFormat.R8G8_UNORM: { elementSize = 2L; break; }
                case GfxFormat.R8G8B8A8_UNORM: { elementSize = 4L; break; }
                case GfxFormat.R16_FLOAT: { elementSize = 2L; break; }
                case GfxFormat.R16G16_FLOAT: { elementSize = 4L; break; }
                case GfxFormat.R16G16B16A16_FLOAT: { elementSize = 8L; break; }
                case GfxFormat.R32_FLOAT: { elementSize = 4L; break; }
                case GfxFormat.R32G32_FLOAT: { elementSize = 8L; break; }
                case GfxFormat.R32G32B32A32_FLOAT: { elementSize = 16L; break; }
                case GfxFormat.BC1_UNORM: { elementSize = 8L; blockWidth = 4; break; }
                case GfxFormat.BC2_UNORM: { elementSize = 16L; blockWidth = 4; break; }
                case GfxFormat.BC3_UNORM: { elementSize = 16L; blockWidth = 4; break; }
                case GfxFormat.BC4_UNORM: { elementSize = 8L; blockWidth = 4; break; }
                case GfxFormat.BC4_SNORM: { elementSize = 8L; blockWidth = 4; break; }
                case GfxFormat.BC5_UNORM: { elementSize = 16L; blockWidth = 4; break; }
                case GfxFormat.BC5_SNORM: { elementSize = 16L; blockWidth = 4; break; }
                case GfxFormat.BC6H_UF16: { elementSize = 16L; blockWidth = 4; break; }
                case GfxFormat.BC6H_SF16: { elementSize = 16L; blockWidth = 4; break; }
                case GfxFormat.BC7_UNORM: { elementSize = 16L; blockWidth = 4; break; }
                default: { throw new InvalidDataException($"Unkown texture format specified: \"{tex_format}\"!"); }
            }

            long width = header.width;
            long height = header.height;

            IGfxTexture? tex = null;
            IGfxTextureView? view = null;

            unsafe
            {
                fixed (byte* buffer = src.Span)
                {
                    IGfxTexture.CreateInfo.SubresourceData[] subresources = new IGfxTexture.CreateInfo.SubresourceData[header.mipMapCount];

                    for (int i = 0; i < header.mipMapCount; i++)
                    {
                        IGfxTexture.CreateInfo.SubresourceData data = new IGfxTexture.CreateInfo.SubresourceData();
                        data.Data = (nint)(buffer + bufferOffset);

                        if (tex_format >= GfxFormat.BC1_TYPELESS)
                        {
                            data.Stride = (ulong)((((uint)width + (blockWidth - 1)) / blockWidth) * elementSize);
                            bufferOffset += (((uint)width + 3) / 4) * (((uint)height + 3) / 4) * elementSize;
                        }
                        else
                        {
                            data.Stride = (ulong)(width * elementSize);
                            bufferOffset += (long)data.Stride * height;
                        }

                        subresources[i] = data;

                        width /= 2;
                        height /= 2;
                    }

                    {
                        IGfxTexture.CreateInfo desc = new IGfxTexture.CreateInfo();
                        desc.Width = header.width;
                        desc.Height = header.height;
                        desc.MipLevels = header.mipMapCount;
                        desc.Bind = GfxBindFlags.ShaderResource;
                        desc.MemoryUsage = GfxMemoryUsage.Immutable;
                        desc.Format = tex_format;
                        desc.Dimension = GfxTextureDimension.Texture2D;
                        desc.Subresources = subresources;

                        tex = device.CreateTexture(desc);
                    }

                    if (tex != null)
                    {
                        IGfxTextureView.CreateInfo viewDesc = new IGfxTextureView.CreateInfo();
                        viewDesc.Type = GfxTextureViewType.ShaderResource;

                        view = tex.CreateView(viewDesc);
                    }
                }
            }

            if (tex == null || view == null)
            {
                LogTypes.Resources.Error("Failed to create some graphics objects for resource: {a}!", texture.Id);

                tex?.Dispose();
                view?.Dispose();

                return;
            }

            texture.BindResources(tex, view);
        }

        private void ReadAsIMG(IGfxDevice device, Texture texture, BinaryReader br)
        {
            br.BaseStream.Seek(0, SeekOrigin.Begin);
            ImageResult result = ImageResult.FromStream(br.BaseStream, ColorComponents.RedGreenBlueAlpha);

            IGfxTexture? tex = null;
            IGfxTextureView? view = null;

            unsafe
            {
                fixed (byte* ptr = result.Data)
                {
                    IGfxTexture.CreateInfo desc = new IGfxTexture.CreateInfo();
                    desc.Width = (uint)result.Width;
                    desc.Height = (uint)result.Height;
                    desc.MipLevels = 1;
                    desc.Bind = GfxBindFlags.ShaderResource;
                    desc.MemoryUsage = GfxMemoryUsage.Immutable;
                    desc.Format = GfxFormat.R8G8B8A8_UNORM;
                    desc.Dimension = GfxTextureDimension.Texture2D;

                    IGfxTexture.CreateInfo.SubresourceData data = new IGfxTexture.CreateInfo.SubresourceData();
                    data.Data = (nint)ptr;
                    data.Stride = (ulong)(result.Width * 4);

                    desc.Subresources = [data];

                    tex = device.CreateTexture(desc);
                }
            }

            if (tex != null)
            {
                IGfxTextureView.CreateInfo viewDesc = new IGfxTextureView.CreateInfo();
                viewDesc.Type = GfxTextureViewType.ShaderResource;

                view = tex.CreateView(viewDesc);
            }

            if (tex == null || view == null)
            {
                LogTypes.Resources.Error("Failed to create some graphics objects for resource: {a}!", texture.Id);

                tex?.Dispose();
                view?.Dispose();

                return;
            }

            texture.BindResources(tex, view);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static uint MAKEFOURCC(uint r, uint g, uint b, uint a)
        {
            return ((uint)r) | ((uint)g << 8) | ((uint)b << 16) | ((uint)a << 24);
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        private struct DDS_HEADER
        {
            public uint size;
            public uint flags;
            public uint height;
            public uint width;
            public uint pitchOrLinearSize;
            public uint depth;
            public uint mipMapCount;
            public uint[] reserved1;
            public DDS_PIXELFORMAT ddspf;
            public uint caps;
            public uint caps2;
            public uint caps3;
            public uint caps4;
            public uint reserved2;

            public DDS_HEADER(BinaryReader br)
            {
                size = br.ReadUInt32();
                flags = br.ReadUInt32();
                height = br.ReadUInt32();
                width = br.ReadUInt32();
                pitchOrLinearSize = br.ReadUInt32();
                depth = br.ReadUInt32();
                mipMapCount = br.ReadUInt32();
                reserved1 = new uint[11];
                ddspf = new DDS_PIXELFORMAT(br);
                caps = br.ReadUInt32();
                caps2 = br.ReadUInt32();
                caps3 = br.ReadUInt32();
                caps4 = br.ReadUInt32();
                reserved2 = br.ReadUInt32();
            }
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        private struct DDS_HEADER_DXT10
        {
            public Vortice.DXGI.Format dxgiFormat;
            public uint resourceDimension;
            public uint miscFlag;
            public uint arraySize;
            public uint miscFlags2;

            public DDS_HEADER_DXT10(BinaryReader br)
            {
                dxgiFormat = (Vortice.DXGI.Format)br.ReadUInt32();
                resourceDimension = br.ReadUInt32();
                miscFlag = br.ReadUInt32();
                arraySize = br.ReadUInt32();
                miscFlags2 = br.ReadUInt32();
            }
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        private struct DDS_PIXELFORMAT
        {
            public uint size;
            public uint flags;
            public uint fourCC;
            public uint RGBBitCount;
            public uint RBitMask;
            public uint GBitMask;
            public uint BBitMask;
            public uint ABitMask;

            public DDS_PIXELFORMAT(BinaryReader br)
            {
                size = br.ReadUInt32();
                flags = br.ReadUInt32();
                fourCC = br.ReadUInt32();
                RGBBitCount = br.ReadUInt32();
                RBitMask = br.ReadUInt32();
                GBitMask = br.ReadUInt32();
                BBitMask = br.ReadUInt32();
                ABitMask = br.ReadUInt32();
            }
        }

        public struct Payload
        {
            public IGfxDevice Device;
            public Texture Object;
            public Filesystem Filesystem;
        }

        private static Dictionary<uint, GfxFormat> PixelFormatConversionTable = new Dictionary<uint, GfxFormat>()
        {
            { 111, GfxFormat.R16_FLOAT },
            { 112, GfxFormat.R16G16_FLOAT },
            { 113, GfxFormat.R16G16B16A16_FLOAT },
            { 114, GfxFormat.R32_FLOAT },
            { 115, GfxFormat.R32G32_FLOAT },
            { 116, GfxFormat.R32G32B32A32_FLOAT },
            { MAKEFOURCC('D', 'X', 'T', '1'), GfxFormat.BC1_UNORM },
            { MAKEFOURCC('D', 'X', 'T', '3'), GfxFormat.BC2_UNORM },
            { MAKEFOURCC('D', 'X', 'T', '5'), GfxFormat.BC3_UNORM },
            { MAKEFOURCC('B', 'C', '4', 'U'), GfxFormat.BC4_UNORM },
            { MAKEFOURCC('B', 'C', '4', 'S'), GfxFormat.BC4_SNORM },
            { MAKEFOURCC('B', 'C', '5', 'U'), GfxFormat.BC5_UNORM },
            { MAKEFOURCC('B', 'C', '5', 'S'), GfxFormat.BC5_SNORM },
        };
    }
}
