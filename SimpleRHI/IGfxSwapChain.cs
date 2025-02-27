namespace SimpleRHI
{
    public interface IGfxSwapChain : IDisposable
    {
        public CreateInfo Desc { get; }

        public IGfxTextureView RenderTargetView { get; }
        public IGfxTextureView? DepthStencilView { get; }

        public void Present(uint vsync);

        public struct CreateInfo
        {
            public string Name;

            public uint Width;
            public uint Height;
            public byte BufferCount;

            public GfxFormat ColorFormat;
            public GfxFormat DepthFormat;

            public IGfxCommandQueue GraphicsCommandQueue;

            public nint WindowHandle;

            public CreateInfo()
            {
                Name = string.Empty;

                Width = 0;
                Height = 0;
                BufferCount = 0;

                ColorFormat = GfxFormat.Unkown;
                DepthFormat = GfxFormat.Unkown;

                WindowHandle = nint.Zero;
            }
        }
    }
}
