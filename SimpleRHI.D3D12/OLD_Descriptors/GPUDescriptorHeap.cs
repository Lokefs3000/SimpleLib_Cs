using Microsoft.Extensions.ObjectPool;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vortice.Direct3D12;

namespace SimpleRHI.D3D12.Descriptors
{
    internal class GPUDescriptorHeap : IDescriptorAllocator
    {
        private ObjectPool<DescriptorHeapAllocation> _pool;
        private GfxDevice _device;

        private DescriptorHeapAllocationManager _dynamicAllocationManager;

        public GPUDescriptorHeap(DescriptorHeapDescription description, GfxDevice device)
        {
            _pool = new DefaultObjectPool<DescriptorHeapAllocation>(new CPUDescriptorHeap.Policy());
            _device = device;

            _dynamicAllocationManager = new DescriptorHeapAllocationManager(_pool, _device, this, 0, description);
        }

        public void Dispose()
        {
            _dynamicAllocationManager.Dispose();
        }

        public DescriptorHeapAllocation? Allocate(uint count)
        {
            lock (_dynamicAllocationManager)
            {
                return _dynamicAllocationManager.Allocate(count);
            }
        }

        public void Free(DescriptorHeapAllocation allocation)
        {
            ushort mgrId = allocation.ManagerId;
            lock (_dynamicAllocationManager)
            {
                _dynamicAllocationManager.Free(allocation);
            }
        }
    }
}
