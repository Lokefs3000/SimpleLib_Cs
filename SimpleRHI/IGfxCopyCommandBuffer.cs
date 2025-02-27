using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vortice.Mathematics;

namespace SimpleRHI
{
    public interface IGfxCopyCommandBuffer : IDisposable
    {
        public CreateInfo Desc { get; }

        public void Begin();
        public void Close();

        public bool CopyBuffer(BufferCopyArguments arguments);
        public bool CopyTexture(TextureCopyArguments arguments);

        public bool CopyCPUBuffer(BufferCopyArguments arguments);
        public bool CopyCPUTexture(TextureCopyArguments arguments);

        public bool IsCopySourceCapable(IGfxBuffer buffer);

        public bool IsCopyDestCapable(IGfxBuffer buffer);
        public bool IsCopyDestCapable(IGfxTexture texture);

        public struct BufferCopyArguments
        {
            public IGfxBuffer? Source;
            public nint SourceData;
            public ulong SourceOffset;

            public IGfxBuffer? Destination;
            public ulong DestinationOffset;

            public ulong Length;

            public BufferCopyArguments()
            {
                Source = null;
                SourceData = nint.Zero;
                SourceOffset = 0;

                Destination = null;
                DestinationOffset = 0;

                Length = 0;
            }
        }

        public struct TextureCopyArguments
        {
            public IGfxBuffer? Source;
            public nint SourceData;
            public ulong SourceOffset;

            public uint Width;
            public uint Height;
            public uint Depth;
            public uint RowPitch;

            public IGfxTexture? Destination;
            public uint DestinationMipSlice;
            public uint DestinationArraySlice;
            public Box? DestinationBox;

            public ulong Length;

            public TextureCopyArguments()
            {
                Source = null;
                SourceData = nint.Zero;
                SourceOffset = 0;
               
                Width = 0;
                Height = 0;
                Depth = 0;
                RowPitch = 0;

                Destination = null;
                DestinationMipSlice = 0u;
                DestinationArraySlice = 0u;
                DestinationBox = null;

                Length = 0;
            }
        }

        public struct CreateInfo
        {
            public string Name;

            public CreateInfo()
            {
                Name = string.Empty;
            }
        }
    }
}
