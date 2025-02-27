using SimpleRHI;

namespace SimpleLib.Render.Components
{
    public class GraphicsDeviceManager : IDisposable
    {
        public readonly IGfxDevice RenderDevice;
        public readonly IGfxCommandQueue ImmediateContext;

        internal GraphicsDeviceManager(API api)
        {
            IGfxDevice.CreateInfo desc = new IGfxDevice.CreateInfo();
            desc.MessageLogger = LogTypes.RHI;
            desc.ValidationEnabled = true;
#if DEBUG
            desc.DebuggingEnabled = true;
#endif

            RenderDevice =  SimpleRHI.D3D12.EngineFactory.Create(desc);
            ImmediateContext = RenderDevice.CreateCommandQueue(new IGfxCommandQueue.CreateInfo { Type = GfxQueueType.Graphics });
        }

        public void Dispose()
        {
            ImmediateContext.Dispose();
            RenderDevice.Dispose();

            GC.SuppressFinalize(this);
        }

        public enum API : byte
        {
            Direct3D12,
            Vulkan
        }
    }
}
