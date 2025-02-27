using Microsoft.Extensions.ObjectPool;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vortice.Direct3D12;

namespace SimpleRHI.D3D12.Descriptors
{
    internal class DescriptorHeapAllocationManager : IDisposable
    {
        private ObjectPool<DescriptorHeapAllocation> _pool;

        private VariableSizeGPUAllocationsManager _freeBlockManager;
        private DescriptorHeapDescription _heapDesc;

        private ID3D12DescriptorHeap _heap;

        private CpuDescriptorHandle _firstCpuHandle;
        private GpuDescriptorHandle _firstGpuHandle;

        private ushort _descriptorSize;
        private uint _numDescriptorsInAllocation;

        private GfxDevice _device;
        private IDescriptorAllocator _allocator;

        private ushort _thisManagerId;

        public DescriptorHeapAllocationManager(ObjectPool<DescriptorHeapAllocation> pool, GfxDevice device, IDescriptorAllocator allocator, ushort thisManagerId, DescriptorHeapDescription desc)
        {
            _pool = pool;
            _device = device;
            _allocator = allocator;
            _heapDesc = desc;
            _freeBlockManager = new VariableSizeGPUAllocationsManager();
            _thisManagerId = thisManagerId;
            _descriptorSize = (ushort)device.D3D12Device.GetDescriptorHandleIncrementSize(_heapDesc.Type);
            _heap = device.D3D12Device.CreateDescriptorHeap(_heapDesc); //TODO: ERROR HANDLING!!
            _numDescriptorsInAllocation = _heapDesc.DescriptorCount;
        }

        public DescriptorHeapAllocationManager(ObjectPool<DescriptorHeapAllocation> pool, GfxDevice device, IDescriptorAllocator allocator, ushort thisManagerId, ID3D12DescriptorHeap heap, uint firstDescriptor, uint numDescriptors)
        {
            _pool = pool;
            _device = device;
            _allocator = allocator;
            _thisManagerId = thisManagerId;
            _heap = heap;
            _descriptorSize = (ushort)device.D3D12Device.GetDescriptorHandleIncrementSize(_heapDesc.Type);
            _firstCpuHandle = _heap.GetCPUDescriptorHandleForHeapStart().Offset((int)(firstDescriptor * _descriptorSize));
            _numDescriptorsInAllocation = numDescriptors;
        }

        public void Dispose()
        {

        }

        public DescriptorHeapAllocation? Allocate(uint count)
        {
            lock (_freeBlockManager)
            {
                ulong descriptorHandleOffset = _freeBlockManager.Allocate(count);
                if (descriptorHandleOffset == VariableSizeGPUAllocationsManager.InvalidOffset)
                    return null;

                CpuDescriptorHandle cpuHandle = _firstCpuHandle;
                cpuHandle.Ptr += (nuint)(descriptorHandleOffset * _descriptorSize);

                GpuDescriptorHandle gpuHandle = _firstGpuHandle;
                if (_heapDesc.Flags.HasFlag(DescriptorHeapFlags.ShaderVisible))
                    gpuHandle.Ptr += descriptorHandleOffset * _descriptorSize;

                DescriptorHeapAllocation allocation = _pool.Get();
                allocation.Initialize(_allocator, _heap, cpuHandle, gpuHandle, count, _thisManagerId, _descriptorSize);
                return allocation;
            }
        }

        public void Free(DescriptorHeapAllocation allocation)
        {
            lock (_freeBlockManager)
            {
                ulong descriptorOffset = (allocation.GetCpuHandle().Ptr - _firstCpuHandle.Ptr) / _descriptorSize;
                _freeBlockManager.Free(descriptorOffset, allocation.NumHandles, _device.CurrentFrame);
                allocation.Dispose();

                _pool.Return(allocation);
            }
        }

        public void ReleaseStaleAllocations(ulong numCompletedFrames)
        {
            lock (_freeBlockManager)
            {
                _freeBlockManager.ReleaseCompletedFrames(numCompletedFrames);
            }
        }

        public ulong NumAvailableDescriptors => _freeBlockManager.FreeSize;
    }
}
