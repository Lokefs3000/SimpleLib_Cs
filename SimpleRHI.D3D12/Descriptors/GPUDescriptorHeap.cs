using Vortice.Direct3D12;

namespace SimpleRHI.D3D12.Descriptors
{
    internal class GPUDescriptorHeap : IDescriptorHeap, IDisposable
    {
        private DescriptorHeapAllocationManager _allocationManager;
        private List<DescriptorSuballocationsManager> _suballocators = new List<DescriptorSuballocationsManager>();

        public GPUDescriptorHeap(GfxDevice device14, DescriptorHeapType heapType, uint heapSize)
        {
            _allocationManager = new DescriptorHeapAllocationManager(this, device14, heapType, heapSize, true, 0);
        }

        public void Dispose()
        {
            _allocationManager.Dispose();
        }

        public DescriptorHeapAllocation Allocate(uint size)
        {
            lock (_allocationManager)
            {
                return _allocationManager.Allocate(size);
            }
        }

        public void Free(ref DescriptorHeapAllocation allocation)
        {
            lock (_allocationManager)
            {
                _allocationManager.Free(ref allocation);
            }
        }

        public void ReleaseStaleAllocations(ulong frameIndex)
        {
            for (int i = 0; i < _suballocators.Count; i++)
            {
                _suballocators[i].DiscardAllocations(frameIndex);
            }

            _allocationManager.ReleaseStaleAllocations(frameIndex);
        }

        public DescriptorSuballocationsManager CreateSuballocator()
        {
            DescriptorSuballocationsManager allocator = new DescriptorSuballocationsManager(this, 64u);
            _suballocators.Add(allocator);
            return allocator;
        }

        public void DestroySuballocator(DescriptorSuballocationsManager allocator)
        {
            _suballocators.Remove(allocator);
        }

        public ID3D12DescriptorHeap D3D12DescriptorHeap => _allocationManager.D3D12DescriptorHeap;
    }
}
