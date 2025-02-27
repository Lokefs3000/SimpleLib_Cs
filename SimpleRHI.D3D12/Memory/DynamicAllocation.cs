using Vortice.Direct3D12;

namespace SimpleRHI.D3D12.Memory
{
    internal struct DynamicAllocation
    {
        public ID3D12Resource? Buffer;
        public ulong Offset;
        public ulong Size;
        public nint CPUAddress;
        public ulong GPUAddress;
    }
}
