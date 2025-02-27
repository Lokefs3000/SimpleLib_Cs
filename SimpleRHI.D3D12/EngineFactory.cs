using System.Runtime.CompilerServices;

namespace SimpleRHI.D3D12
{
    public static class EngineFactory
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static IGfxDevice Create(in IGfxDevice.CreateInfo ci)
        {
            return new GfxDevice(ci);
        }
    }
}
