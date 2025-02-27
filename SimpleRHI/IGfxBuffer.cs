namespace SimpleRHI
{
    public interface IGfxBuffer : IDisposable
    {
        public CreateInfo Desc { get; }

        public IGfxBufferView CreateView(in IGfxBufferView.CreateInfo ci);

        public Span<T> Map<T>(GfxMapType type, GfxMapFlags flags) where T : unmanaged;
        public nint Map(GfxMapType type, GfxMapFlags flags);
        public void Unmap();

        public struct CreateInfo
        {
            public string Name;

            public ulong Size;

            public GfxBindFlags Bind;
            public GfxBufferMode Mode;

            public GfxMemoryUsage MemoryUsage;
            public GfxCPUAccessFlags CpuAccess;

            public uint ElementByteStride;

            public nint Data;

            public CreateInfo()
            {
                Name = "";
                Size = 0;
                Bind = GfxBindFlags.Unkown;
                Mode = GfxBufferMode.None;
                MemoryUsage = GfxMemoryUsage.Default;
                CpuAccess = GfxCPUAccessFlags.None;
                ElementByteStride = 0;
                Data = nint.Zero;
            }
        }
    }
}
