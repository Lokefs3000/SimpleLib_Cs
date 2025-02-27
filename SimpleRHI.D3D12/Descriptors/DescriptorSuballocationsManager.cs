using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace SimpleRHI.D3D12.Descriptors
{
    internal class DescriptorSuballocationsManager : IDescriptorHeap
    {
        private GPUDescriptorHeap _parentHeap;

        private uint _dynamicChunkSize = 0;

        private List<DescriptorHeapAllocation> _suballocations = new List<DescriptorHeapAllocation>();
        private uint _currentSuballocationOffset = 0;

        public DescriptorSuballocationsManager(GPUDescriptorHeap parentHeap, uint chunkSize)
        {
            _parentHeap = parentHeap;
            _dynamicChunkSize = chunkSize;
        }

        public DescriptorHeapAllocation Allocate(uint size)
        {
            if (_suballocations.Count == 0 || _currentSuballocationOffset + size > _suballocations[_suballocations.Count - 1].NumDescriptors)
            {
                ulong suballocationSize = Math.Max(_dynamicChunkSize, size);
                DescriptorHeapAllocation newAllocation = _parentHeap.Allocate(size);

                _suballocations.Add(newAllocation);
                _currentSuballocationOffset = 0;
            }

            Span<DescriptorHeapAllocation> span = CollectionsMarshal.AsSpan(_suballocations);
            ref DescriptorHeapAllocation suballocation = ref span[span.Length - 1];

            ushort managerId = suballocation.ManagerId;
            DescriptorHeapAllocation allocation = new DescriptorHeapAllocation(this, suballocation.GetCPUHandle(_currentSuballocationOffset), suballocation.GetGPUHandle(_currentSuballocationOffset), 0, (ushort)size, suballocation.DescriptorSize, managerId);

            return allocation;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Free(ref DescriptorHeapAllocation allocation)
        {

        }

        public void DiscardAllocations(ulong frameNumber)
        {
            for (int i = 0; i < _suballocations.Count; i++)
            {
                _suballocations[i].Free();
            }

            _suballocations.Clear();
        }
    }
}
