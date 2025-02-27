namespace SimpleRHI
{
    public interface IGfxPipelineStateCache : IDisposable
    {
        public CreateInfo Desc { get; }

        public ReadOnlySpan<byte> Serialize();

        public struct CreateInfo
        {
            public byte[]? CacheBinary;
        }
    }
}
