using Serilog;

namespace SimpleRHI
{
    public interface IGfxDevice : IDisposable
    {
        public CreateInfo Desc { get; }

        public string Name { get; }

        public IGfxBuffer CreateBuffer(in IGfxBuffer.CreateInfo ci);
        public IGfxTexture CreateTexture(in IGfxTexture.CreateInfo ci);
        public IGfxPipelineStateCache CreatePipelineStateCache(in IGfxPipelineStateCache.CreateInfo ci);
        public IGfxGraphicsPipeline CreateGraphicsPipeline(in IGfxGraphicsPipeline.CreateInfo ci);
        public IGfxCommandQueue CreateCommandQueue(in IGfxCommandQueue.CreateInfo ci);
        public IGfxFence CreateFence(in IGfxFence.CreateInfo ci);
        public IGfxGraphicsCommandBuffer CreateGraphicsCommandBuffer(in IGfxGraphicsCommandBuffer.CreateInfo ci);
        public IGfxCopyCommandBuffer CreateCopyCommandBuffer(in IGfxCopyCommandBuffer.CreateInfo ci);
        public IGfxSwapChain CreateSwapChain(in IGfxSwapChain.CreateInfo ci);

        public void WaitForFrames();

        public struct CreateInfo
        {
            public bool ValidationEnabled;
            public bool DebuggingEnabled;

            public ILogger? MessageLogger;

            public CreateInfo()
            {
                ValidationEnabled = false;

                MessageLogger = null;
            }
        }
    }
}
