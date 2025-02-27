using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SimpleRHI.D3D12.Descriptors
{
    internal interface IDescriptorAllocator : IDisposable
    {
        public DescriptorHeapAllocation Allocate(uint count);
        public void Free(DescriptorHeapAllocation allocation);
    }
}
