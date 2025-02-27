namespace SimpleRHI
{
    public interface IGfxTexture : IDisposable
    {
        public CreateInfo Desc { get; }

        public IGfxTextureView CreateView(in IGfxTextureView.CreateInfo ci);

        public struct CreateInfo
        {
            public string Name;

            public uint Width;
            public uint Height;
            public uint Depth;

            public uint MipLevels;

            public GfxFormat Format;

            public GfxBindFlags Bind;
            public GfxMemoryUsage MemoryUsage;
            public GfxTextureDimension Dimension;

            public SubresourceData[]? Subresources;

            public CreateInfo()
            {
                Name = "";
                Width = 0;
                Height = 0;
                Depth = 0;
                Bind = GfxBindFlags.Unkown;
                MemoryUsage = GfxMemoryUsage.Default;
                Dimension = GfxTextureDimension.Texture2D;
            }

            public struct SubresourceData
            {
                public nint Data;
                public ulong Stride;
            }
        }
    }
}
