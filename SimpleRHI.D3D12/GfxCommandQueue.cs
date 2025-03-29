using SharpGen.Runtime;
using System.Runtime.CompilerServices;
using Vortice.Direct3D12;

namespace SimpleRHI.D3D12
{
    internal class GfxCommandQueue : IGfxCommandQueue
    {
        public IGfxCommandQueue.CreateInfo Desc => _desc;
        private IGfxCommandQueue.CreateInfo _desc;

        private ID3D12CommandQueue _commandQueue;

        private ID3D12Fence _fence;
        private ulong _frameIndex;
        private ManualResetEvent _event;

        public GfxCommandQueue(IGfxCommandQueue.CreateInfo ci, ID3D12Device14 device)
        {
            _desc = ci;

            CommandQueueDescription queueDesc = new CommandQueueDescription();
            queueDesc.Priority = 0;
            queueDesc.Flags = CommandQueueFlags.None;
            queueDesc.NodeMask = 0;

            switch (ci.Type)
            {
                case GfxQueueType.Graphics: queueDesc.Type = CommandListType.Direct; break;
                case GfxQueueType.Copy: queueDesc.Type = CommandListType.Copy; break;
                case GfxQueueType.Compute: queueDesc.Type = CommandListType.Compute; break;
                default:
                    {
                        GfxDevice.Logger?.Error("Unkown command queue type: \"{}\"", ci.Type);
                        throw new Exception();
                    }
            }

            Result r = device.CreateCommandQueue(queueDesc, out ID3D12CommandQueue? v);
            if (r.Failure || v == null)
            {
                GfxDevice.Logger?.Error("Failed to create command queue!");
                throw new Exception(r.Code.ToString());
            }
            else
            {
                _commandQueue = v;
            }

            r = device.CreateFence(0, FenceFlags.None, out _fence);
            if (r.Failure)
            {
                GfxDevice.Logger?.Error("Failed to create fence!");
                throw new Exception(r.Code.ToString());
            }

            _frameIndex = 0;
            _event = new ManualResetEvent(false);
        }

        public void Dispose()
        {
            _commandQueue?.Dispose();
            _fence?.Dispose();
            _event?.Dispose();
        }

        public ulong WaitForCompletion()
        {
            ulong nextFence = _frameIndex + 1;
            _commandQueue.Signal(_fence, nextFence);

            if (_fence.CompletedValue < nextFence)
            {
                _fence.SetEventOnCompletion(nextFence, _event);
                _event.WaitOne();
                _event.Reset();
            }

            _frameIndex = nextFence;
            return nextFence;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Submit(in IGfxGraphicsCommandBuffer commandBuffer)
        {
            OnCommandBufferSubmit?.Invoke(GfxQueueType.Graphics, ((GfxGraphicsCommandBuffer)commandBuffer).Id);

            GfxGraphicsCommandBuffer buffer = (GfxGraphicsCommandBuffer)commandBuffer;
            _commandQueue.ExecuteCommandList(buffer.D3D12GraphicsCommandList);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Submit(in IGfxCopyCommandBuffer commandBuffer)
        {
            OnCommandBufferSubmit?.Invoke(GfxQueueType.Copy, ((GfxCopyCommandBuffer)commandBuffer).Id);

            GfxCopyCommandBuffer buffer = (GfxCopyCommandBuffer)commandBuffer;
            _commandQueue.ExecuteCommandList(buffer.D3D12GraphicsCommandList);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Wait(IGfxFence fence, ulong value)
        {
            _commandQueue.Wait(((GfxFence)fence).D3D12Fence, value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Signal(IGfxFence fence, ulong value)
        {
            _commandQueue.Signal(((GfxFence)fence).D3D12Fence, value);
        }

        public ID3D12CommandQueue D3D12CommandQueue => _commandQueue;
        public ID3D12Fence D3D12Fence => _fence;

        public Action<GfxQueueType, ushort>? OnCommandBufferSubmit = null; //happens BEFORE any actual api call is made
    }
}
