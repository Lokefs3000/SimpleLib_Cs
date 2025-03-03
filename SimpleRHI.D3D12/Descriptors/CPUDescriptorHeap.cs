using Vortice.Direct3D12;
using Vortice.DXGI;

namespace SimpleRHI.D3D12.Descriptors
{
    internal class CPUDescriptorHeap : IDescriptorHeap, IDisposable
    {
        private List<DescriptorHeapAllocationManager> _heapPool = new List<DescriptorHeapAllocationManager>();
        private HashSet<int> _availableHeaps = new HashSet<int>();

        private Queue<int> _removable = new Queue<int>();

        private GfxDevice _device;

        private DescriptorHeapType _heapType;
        private uint _heapSize;

        private DescriptorHeapAllocation _nullAlloc;

        public CPUDescriptorHeap(GfxDevice device, DescriptorHeapType heapType, uint heapSize)
        {
            _device = device;
            _heapType = heapType;
            _heapSize = heapSize;

            switch (heapType)
            {
                case DescriptorHeapType.RenderTargetView:
                    _nullAlloc = Allocate(1u);
                    device.D3D12Device.CreateRenderTargetView(null, new RenderTargetViewDescription
                    {
                        ViewDimension = RenderTargetViewDimension.Texture2D,
                        Texture2D = new Texture2DRenderTargetView
                        {
                            MipSlice = 0u,
                            PlaneSlice = 0u
                        },
                        Format = Format.R8G8B8A8_UNorm,
                    }, _nullAlloc.GetCPUHandle());
                    break;
                case DescriptorHeapType.DepthStencilView:
                    _nullAlloc = Allocate(1u);
                    device.D3D12Device.CreateDepthStencilView(null, new DepthStencilViewDescription
                    {
                        ViewDimension = DepthStencilViewDimension.Texture2D,
                        Texture2D = new Texture2DDepthStencilView
                        {
                            MipSlice = 0u
                        },
                        Format = Format.D24_UNorm_S8_UInt,
                        Flags = DepthStencilViewFlags.None,
                    }, _nullAlloc.GetCPUHandle());
                    break;
                default: GfxDevice.Logger?.Warning("Cannot create null descriptors for heap type: \"{a}\"!", heapType); break;
            }
        }

        public void Dispose()
        {
            for (int i = 0; i < _heapPool.Count; i++)
            {
                _heapPool[i].Dispose();
            }

            _heapPool.Clear();
            _availableHeaps.Clear();
        }

        public DescriptorHeapAllocation Allocate(uint size)
        {
            lock (_heapPool)
            {
                DescriptorHeapAllocation allocation = new DescriptorHeapAllocation();
                foreach (int i in _availableHeaps)
                {
                    allocation = _heapPool[i].Allocate(size);

                    if (_heapPool[i].FreeSize == 0)
                    {
                        _removable.Enqueue(i);
                    }

                    if (!allocation.IsValid)
                    {
                        break;
                    }
                }

                if (_removable.Count > 0)
                {
                    while (_removable.TryDequeue(out int r)) _availableHeaps.Remove(r);
                }

                if (!allocation.IsValid)
                {
                    uint newSize = Math.Max(_heapSize, size);
                    _heapSize = newSize;

                    GfxDevice.Logger?.Debug("Creating new descriptor heap manager with properties:\n    Heap: {a}\n    Size: {b}", _heapType, newSize);

                    DescriptorHeapAllocationManager manager = new DescriptorHeapAllocationManager(this, _device, _heapType, newSize, false, (ushort)_heapPool.Count);
                    allocation = manager.Allocate(size);

                    _heapPool.Add(manager);
                    _availableHeaps.Add(manager.Id);
                }

                return allocation;
            }
        }

        public void Free(ref DescriptorHeapAllocation allocation)
        {
            lock (_heapPool)
            {
                DescriptorHeapAllocationManager manager = _heapPool[allocation.ManagerId];
                manager.Free(ref allocation);

                _availableHeaps.Add(manager.Id);
            }
        }

        public void ReleaseStaleAllocations(ulong frameIndex)
        {
            for (int i = 0; i < _heapPool.Count; i++)
            {
                _heapPool[i].ReleaseStaleAllocations(frameIndex);
            }
        }

        public CpuDescriptorHandle NullDescriptor => _nullAlloc.GetCPUHandle();
    }
}
