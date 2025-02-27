using SharpGen.Runtime;
using System.Runtime.CompilerServices;
using Vortice.Direct3D12;

namespace SimpleRHI.D3D12
{
    internal class GfxFence : IGfxFence
    {
        public IGfxFence.CreateInfo Desc => _desc;
        private IGfxFence.CreateInfo _desc;

        public ulong CompletedValue => _fence.CompletedValue;

        private ID3D12Fence1 _fence;

        public GfxFence(in IGfxFence.CreateInfo ci, GfxDevice device)
        {
            _desc = ci;

            Result r = device.D3D12Device.CreateFence(ci.InitialValue, FenceFlags.None, out ID3D12Fence? v);
            if (r.Failure || v == null)
            {
                GfxDevice.Logger?.Error("Failed to create fence!");
                throw new Exception(r.Code.ToString());
            }

            ID3D12Fence1 v2 = v.QueryInterfaceOrNull<ID3D12Fence1>();
            if (v2 == null)
            {
                GfxDevice.Logger?.Error("Failed to query for \"ID3D12Fence1\"!");
                throw new Exception();
            }

            _fence = v2;
        }

        public void Dispose()
        {
            _fence.Dispose();

            GC.SuppressFinalize(this);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetEventOnCompletion(ulong value, WaitHandle? @event)
        {
            _fence.SetEventOnCompletion(value, @event);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Signal(ulong value)
        {
            _fence.Signal(value);
        }

        public ID3D12Fence D3D12Fence => _fence;
    }
}
