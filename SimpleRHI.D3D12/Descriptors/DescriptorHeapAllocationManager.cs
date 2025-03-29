using SimpleRHI.D3D12.Allocators;
using System.Numerics;
using System.Runtime.CompilerServices;
using Vortice.Direct3D12;

namespace SimpleRHI.D3D12.Descriptors
{
    internal class DescriptorHeapAllocationManager : IDisposable
    {
        private IDescriptorHeap _parentHeap;
        private GfxDevice _device;

        private IAllocator _allocator;
        private ID3D12DescriptorHeap _heap;

        private ushort _id;
        private ushort _descriptorSize;

        private CpuDescriptorHandle _firstCpuHandle;
        private GpuDescriptorHandle _firstGpuHandle;

        private bool _shaderVisible;

        public DescriptorHeapAllocationManager(IDescriptorHeap parentHeap, GfxDevice device, DescriptorHeapType type, uint size, bool shaderVisible, ushort id)
        {
            _parentHeap = parentHeap;
            _device = device;
            _allocator = (shaderVisible) ? new RingAllocator<uint>(size) : new DynamicGPUAllocator((uint)(size >= 512 ? 64 : 8), size);
            _heap = device.D3D12Device.CreateDescriptorHeap(new DescriptorHeapDescription(type, size, shaderVisible ? DescriptorHeapFlags.ShaderVisible : DescriptorHeapFlags.None));
            _id = id;
            _descriptorSize = (ushort)device.D3D12Device.GetDescriptorHandleIncrementSize(type);
            _firstCpuHandle = _heap.GetCPUDescriptorHandleForHeapStart();
            _firstGpuHandle = shaderVisible ? _heap.GetGPUDescriptorHandleForHeapStart() : GpuDescriptorHandle.Default;
            _shaderVisible = shaderVisible;
        }

        public void Dispose()
        {
            _heap.Dispose();
        }

        public DescriptorHeapAllocation Allocate(uint size)
        {
            uint rounded = BitOperations.RoundUpToPowerOf2(size);
            ulong offset = _allocator.Allocate(rounded);

            if (offset == IAllocator.Invalid)
                return new DescriptorHeapAllocation(_parentHeap, CpuDescriptorHandle.Default, GpuDescriptorHandle.Default, IAllocator.Invalid, 0, 0, ushort.MaxValue);
            return new DescriptorHeapAllocation(_parentHeap, new CpuDescriptorHandle(_firstCpuHandle, (int)(offset * _descriptorSize)), _shaderVisible ? new GpuDescriptorHandle(_firstGpuHandle, 0) : GpuDescriptorHandle.Default, offset, (ushort)rounded, _descriptorSize, _id);
        }

        public void Free(ref DescriptorHeapAllocation allocation)
        {
            _allocator.Free(allocation.NumDescriptors, (uint)allocation.Offset, _device.FrameIndex);
            allocation.Reset();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ReleaseStaleAllocations(ulong frameIndex)
        {
            _allocator.ReleaseStaleAllocations(frameIndex);
        }

        public uint FreeSpace => _allocator.FreeSpace;
        public ushort Id => _id;

        public ID3D12DescriptorHeap D3D12DescriptorHeap => _heap;
    }
}
