namespace SimpleRHI.D3D12.Descriptors
{
    internal interface IDescriptorHeap
    {
        public DescriptorHeapAllocation Allocate(uint size);
        public void Free(ref DescriptorHeapAllocation allocation);
    }
}
