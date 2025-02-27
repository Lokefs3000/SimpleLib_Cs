using Microsoft.Extensions.ObjectPool;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SimpleRHI.D3D12.Descriptors
{
    internal class DynamicSuballocationsManager : IDescriptorAllocator
    {
        private GPUDescriptorHeap _parentHeap;
        private ObjectPool<DescriptorHeapAllocation> _pool;

        private uint _dynamicChunkSize = 0;

        private List<DescriptorHeapAllocation> _suballocations = new List<DescriptorHeapAllocation>();
        private List<DescriptorHeapAllocation> _frameAllocations = new List<DescriptorHeapAllocation>();

        private uint _currentSuballocationOffset = 0;

        private ushort _descriptorSize = 0;

        public DynamicSuballocationsManager(GPUDescriptorHeap parentHeap, ObjectPool<DescriptorHeapAllocation> pool, uint dynamicChunkSize, ushort descriptorSize)
        {
            _parentHeap = parentHeap;
            _pool = pool;
            _dynamicChunkSize = dynamicChunkSize;
            _descriptorSize = descriptorSize;
        }

        public DescriptorHeapAllocation Allocate(uint count)
        {
            if (_suballocations.Count == 0 || _currentSuballocationOffset + count > _suballocations[_suballocations.Count - 1].NumHandles)
            {
                ulong suballocSize = Math.Max(_dynamicChunkSize, count);
                DescriptorHeapAllocation? newDynamicAlloc = _parentHeap.Allocate(count);
                _suballocations.Add(newDynamicAlloc ?? throw new Exception());
                _currentSuballocationOffset = 0;
            }

            DescriptorHeapAllocation suballocation = _suballocations[_suballocations.Count - 1];

            ushort managerId = suballocation.ManagerId;

            DescriptorHeapAllocation allocation = _pool.Get();
            allocation.Initialize(this, suballocation.DescriptorHeap, suballocation.GetCpuHandle(_currentSuballocationOffset), suballocation.GetGpuHandle(_currentSuballocationOffset), count, managerId, _descriptorSize);
            
            _currentSuballocationOffset += count;
            return allocation;
        }

        public void Dispose()
        {

        }

        public void Free(DescriptorHeapAllocation allocation)
        {
            _pool.Return(allocation);
        }

        public void DiscardAllocations(ulong frameNumber)
        {
            for (int i = 0; i < _suballocations.Count; i++)
            {
                _suballocations[i].Dispose();
            }

            for (int i = 0; i < _frameAllocations.Count; i++)
            {
                _frameAllocations[i].Dispose();
            }

            _suballocations.Clear();
            _frameAllocations.Clear();
        }
    }
}
