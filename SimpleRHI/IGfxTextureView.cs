namespace SimpleRHI
{
    public interface IGfxTextureView : IDisposable
    {
        public CreateInfo Desc { get; }

        public struct CreateInfo
        {
            public GfxTextureViewType Type;

            public CreateInfo()
            {
                Type = GfxTextureViewType.ShaderResource;
            }
        }
    }
}
