using System.Runtime.CompilerServices;
using Vortice.Direct3D12;

namespace SimpleRHI.D3D12.Descriptors
{
    internal struct DescriptorHeapAllocation
    {
        private IDescriptorHeap _heap;

        private CpuDescriptorHandle _firstCpuHandle;
        private GpuDescriptorHandle _firstGpuHandle;

        private ulong _offset;
        private ushort _numDescriptors;
        private ushort _descriptorSize;

        private ushort _managerId;

        public DescriptorHeapAllocation(IDescriptorHeap heap, CpuDescriptorHandle cpuHandle, GpuDescriptorHandle gpuHandle, ulong offset, ushort numDescriptors, ushort descriptorSize, ushort managerId)
        {
            _heap = heap;
            _firstCpuHandle = cpuHandle;
            _firstGpuHandle = gpuHandle;
            _offset = offset;
            _numDescriptors = numDescriptors;
            _descriptorSize = descriptorSize;
            _managerId = managerId;
        }

        public DescriptorHeapAllocation(CpuDescriptorHandle cpu)
        {
            _firstCpuHandle = cpu;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Free()
        {
            _heap.Free(ref this);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public CpuDescriptorHandle GetCPUHandle(uint offset = 0)
        {
            return offset == 0 ? _firstCpuHandle : new CpuDescriptorHandle(_firstCpuHandle, (int)(offset * _descriptorSize));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public GpuDescriptorHandle GetGPUHandle(uint offset = 0)
        {
            return offset == 0 ? _firstGpuHandle : new GpuDescriptorHandle(_firstGpuHandle, (int)(offset * _descriptorSize));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Reset()
        {
            _numDescriptors = 0;
            _managerId = ushort.MaxValue;
        }

        public ulong Offset => _offset;

        public ushort NumDescriptors => _numDescriptors;
        public ushort ManagerId => _managerId;
        public ushort DescriptorSize => _descriptorSize;

        public bool IsValid => _firstCpuHandle != CpuDescriptorHandle.Default;
    }
}
