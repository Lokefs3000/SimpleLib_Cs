using SimpleRHI;
using Vortice.Mathematics;

namespace SimpleLib.Render.Components
{
    public class CommandBufferPool : IDisposable
    {
        private Queue<IGfxGraphicsCommandBuffer> _deferredContexts = new Queue<IGfxGraphicsCommandBuffer>();
        private List<IGfxGraphicsCommandBuffer> _deferredCommands = new List<IGfxGraphicsCommandBuffer>();

        private List<IGfxGraphicsCommandBuffer> _rememberedContexts = new List<IGfxGraphicsCommandBuffer>();

        private int _totalContexts = 0;

        private IGfxDevice _device;

        private RenderPassContainer _renderPassContainer;

        public CommandBufferPool(GraphicsDeviceManager deviceManager, RenderPassContainer renderPassContainer)
        {
            for (int i = 0; i < 4; i++)
            {
                IGfxGraphicsCommandBuffer commandBuffer = deviceManager.RenderDevice.CreateGraphicsCommandBuffer(new IGfxGraphicsCommandBuffer.CreateInfo
                {
                    Name = $"{i}",
                    Type = GfxQueueType.Graphics,
                });

                _deferredContexts.Enqueue(commandBuffer);
                _rememberedContexts.Add(commandBuffer);
            }

            _totalContexts = _deferredContexts.Count;
            _device = deviceManager.RenderDevice;
            _renderPassContainer = renderPassContainer;
        }

        public void Dispose()
        {
            foreach (var command in _deferredCommands)
            {
                command.Dispose();
            }

            foreach (var command in _deferredContexts)
            {
                command.Dispose();
            }
        }

        public void EndFrame(IGfxCommandQueue immediate)
        {
            for (int i = 0; i < _deferredCommands.Count; i++)
            {
                immediate.Submit(_deferredCommands[i]);
                _deferredContexts.Enqueue(_deferredCommands[i]);
            }

            _deferredCommands.Clear();

            for (int i = 0; i < _rememberedContexts.Count; i++)
            {
                if (!_deferredContexts.Contains(_rememberedContexts[i]))
                {
                    LogTypes.Graphics.Error("Leaked command buffer!!");

                    IGfxGraphicsCommandBuffer commandBuffer = _rememberedContexts[i];
                    commandBuffer.Close();

                    _deferredContexts.Enqueue(commandBuffer);
                }
            }
        }

        public IGfxGraphicsCommandBuffer GetContext(string? name = null)
        {
            bool isEmpty = false;
            lock (_deferredContexts) { isEmpty = _deferredContexts.Count == 0; }

            if (isEmpty)
            {
                IGfxGraphicsCommandBuffer commandBuffer = _device.CreateGraphicsCommandBuffer(new IGfxGraphicsCommandBuffer.CreateInfo
                {
                    Name = "New",
                    Type = GfxQueueType.Graphics,
                });

                lock (_deferredContexts)
                {
                    _deferredContexts.Enqueue(commandBuffer);
                }
            }

            lock (_deferredContexts)
            {
                IGfxGraphicsCommandBuffer context = _deferredContexts.Dequeue();

                context.Begin();
                context.BeginDebugGroup(name ?? (_renderPassContainer.CurrentRenderPass?.Name ?? "Unkown"), _renderPassContainer.CurrentRenderPass?.Color ?? new Color4(1.0f));

                return context;
            }
        }

        public void ReturnContext(IGfxGraphicsCommandBuffer context)
        {
            context.EndDebugGroup();
            context.Close();

            lock (_deferredContexts)
            {
                _deferredCommands.Add(context);
            }
        }
    }
}
