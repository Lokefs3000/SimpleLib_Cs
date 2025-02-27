using Microsoft.Extensions.ObjectPool;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vortice.Direct3D12;

namespace SimpleRHI.D3D12.Descriptors
{
    internal class CPUDescriptorHeap : IDescriptorAllocator
    {
        private ObjectPool<DescriptorHeapAllocation> _pool;

        private List<DescriptorHeapAllocationManager> _heapPool = new List<DescriptorHeapAllocationManager>();
        private HashSet<int> _availableHeaps = new HashSet<int>();

        private Queue<int> _removing = new Queue<int>();

        private DescriptorHeapDescription _heapDesc;

        private GfxDevice _device;

        private uint _currentSize = 0;
        private uint _maxHeapSize = 0;

        public CPUDescriptorHeap(DescriptorHeapDescription description, GfxDevice device)
        {
            _pool = new DefaultObjectPool<DescriptorHeapAllocation>(new Policy());
            _heapDesc = description;
            _device = device;
        }

        public void Dispose()
        {
            for (int i = 0; i < _heapPool.Count; i++)
            {
                _heapPool[i].Dispose();
            }

            GC.SuppressFinalize(this);
        }

        public DescriptorHeapAllocation? Allocate(uint count)
        {
            lock (_heapPool)
            {
                DescriptorHeapAllocation? allocation = null;

                foreach (int i in _availableHeaps)
                {
                    allocation = _heapPool[i].Allocate(count);
                    if (_heapPool[i].NumAvailableDescriptors == 0)
                        _removing.Enqueue(i);

                    if (allocation?.GetCpuHandle().Ptr != 0)
                        break;
                }

                while (_removing.TryDequeue(out int r)) _availableHeaps.Remove(r);

                if (allocation?.GetCpuHandle().Ptr == 0)
                {
                    _heapDesc.DescriptorCount = Math.Max(_heapDesc.DescriptorCount, count);
                    _heapPool.Add(new DescriptorHeapAllocationManager(_pool, _device, this, (ushort)_heapPool.Count, _heapDesc));

                    _availableHeaps.Add(_heapPool.Count - 1);

                    allocation = _heapPool[_heapPool.Count - 1].Allocate(count);
                }

                _currentSize += (allocation?.GetCpuHandle().Ptr != 0) ? count : 0;
                _maxHeapSize = Math.Max(_maxHeapSize, _currentSize);

                return allocation;
            }
        }

        public void Free(DescriptorHeapAllocation allocation)
        {
            lock (_heapPool)
            {
                ushort managerId = allocation.ManagerId;
                _currentSize -= allocation.NumHandles;
                _heapPool[managerId].Free(allocation);
            }
        }

        public void ReleaseStaleAllocations(ulong numCompletedFrames)
        {
            lock (_heapPool)
            {
                for (int i = 0; i < _heapPool.Count; i++)
                {
                    _heapPool[i].ReleaseStaleAllocations(numCompletedFrames);

                    if (_heapPool[i].NumAvailableDescriptors > 0)
                        _availableHeaps.Add(i);
                }
            }
        }

        public class Policy : IPooledObjectPolicy<DescriptorHeapAllocation>
        {
            public DescriptorHeapAllocation Create()
            {
                return new DescriptorHeapAllocation();
            }

            public bool Return(DescriptorHeapAllocation obj)
            {
                obj.Dispose();
                return true;
            }
        }
    }
}
