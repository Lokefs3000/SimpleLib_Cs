namespace SimpleRHI
{
    public interface IGfxCommandQueue : IDisposable
    {
        public CreateInfo Desc { get; }

        public void Submit(in IGfxGraphicsCommandBuffer commandBuffer);
        public void Submit(in IGfxCopyCommandBuffer commandBuffer);

        public void Wait(IGfxFence fence, ulong value);
        public void Signal(IGfxFence fence, ulong value);

        public struct CreateInfo
        {
            public GfxQueueType Type;

            public CreateInfo()
            {
                Type = GfxQueueType.Graphics;
            }
        }
    }
}
