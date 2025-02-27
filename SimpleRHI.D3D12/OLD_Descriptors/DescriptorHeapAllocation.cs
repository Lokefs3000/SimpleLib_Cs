using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vortice.Direct3D12;

namespace SimpleRHI.D3D12.Descriptors
{
    internal class DescriptorHeapAllocation : IDisposable
    {
        private CpuDescriptorHandle _firstCpuHandle;
        private GpuDescriptorHandle _firstGpuHandle;

        private IDescriptorAllocator? _allocator;
        private ID3D12DescriptorHeap? _heap;

        uint _numHandles;
        ushort _allocationManagerId;
        ushort _descriptorSize;

        public DescriptorHeapAllocation()
        {
            
        }

        public void Initialize(IDescriptorAllocator allocator, ID3D12DescriptorHeap heap, CpuDescriptorHandle cpuHandle, GpuDescriptorHandle gpuHandle, uint numHandles, ushort allocationManagerId, ushort descriptorSize)
        {
            _allocator = allocator;
            _heap = heap;
            _firstCpuHandle = cpuHandle;
            _firstGpuHandle = gpuHandle;
            _numHandles = numHandles;
            _allocationManagerId = allocationManagerId;
            _descriptorSize = descriptorSize;
        }

        public void Dispose()
        {
            _allocator?.Free(this);

            _allocator = null;
            _heap = null;
            _firstCpuHandle = CpuDescriptorHandle.Default;
            _firstGpuHandle = GpuDescriptorHandle.Default;
            _numHandles = 0;
            _allocationManagerId = ushort.MaxValue;
            _descriptorSize = 0;
        }

        public CpuDescriptorHandle GetCpuHandle(uint offset = 0)
        {
            CpuDescriptorHandle handle = _firstCpuHandle;
            if (offset != 0)
                handle.Ptr += _descriptorSize * offset;
            return handle;
        }

        public GpuDescriptorHandle GetGpuHandle(uint offset = 0)
        {
            GpuDescriptorHandle handle = _firstGpuHandle;
            if (offset != 0)
                handle.Ptr += _descriptorSize * offset;
            return handle;
        }

        public uint NumHandles => _numHandles;

        public bool IsNull => _firstCpuHandle.Ptr == 0;
        public bool IsShaderVisible => _firstGpuHandle.Ptr == 0;

        public ushort ManagerId => _allocationManagerId;

        public ID3D12DescriptorHeap? DescriptorHeap => _heap;
    }
}
