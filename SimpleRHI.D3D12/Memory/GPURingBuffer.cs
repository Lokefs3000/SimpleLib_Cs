using SharpGen.Runtime;
using Vortice.Direct3D12;
using Vortice.DXGI;

namespace SimpleRHI.D3D12.Memory
{
    internal class GPURingBuffer : RingBuffer, IDisposable
    {
        private nint _cpuVirtualAddress = nint.Zero;
        private ulong _gpuVirtualAddress = 0;
        private ID3D12Resource _buffer;

        public GPURingBuffer(ulong maxSize, ID3D12Device14 device, bool allowCPUAccess) : base(maxSize)
        {
            GfxDevice.Logger?.Debug("Creating new GPU ring buffer with size: {a}mb", maxSize / 1024.0 / 1024.0);

            HeapProperties heapProps = new HeapProperties();
            heapProps.CPUPageProperty = CpuPageProperty.Unknown;
            heapProps.MemoryPoolPreference = MemoryPool.Unknown;
            heapProps.CreationNodeMask = 0;
            heapProps.VisibleNodeMask = 0;

            ResourceDescription resDesc = new ResourceDescription();
            resDesc.Dimension = ResourceDimension.Buffer;
            resDesc.Alignment = 0;
            resDesc.Width = maxSize;
            resDesc.Height = 1;
            resDesc.DepthOrArraySize = 1;
            resDesc.MipLevels = 1;
            resDesc.Format = Format.Unknown;
            resDesc.SampleDescription = SampleDescription.Default;
            resDesc.Layout = TextureLayout.RowMajor;

            ResourceStates defaultUsage = ResourceStates.Common;
            if (allowCPUAccess)
            {
                heapProps.Type = HeapType.Upload;
                resDesc.Flags = ResourceFlags.None;
                defaultUsage = ResourceStates.GenericRead;
            }
            else
            {
                heapProps.Type = HeapType.Default;
                resDesc.Flags = ResourceFlags.AllowUnorderedAccess;
                defaultUsage = ResourceStates.UnorderedAccess;
            }

            Result r = device.CreateCommittedResource(heapProps, HeapFlags.None, resDesc, defaultUsage, null, out ID3D12Resource? v);
            if (r.Failure || v == null)
            {
                GfxDevice.Logger?.Error("Failed to create GPU ring buffer resource!");
                throw new Exception();
            }
            else
            {
                _buffer = v;
            }

            v.Name = "Upload Ring Buffer";

            _gpuVirtualAddress = _buffer.GPUVirtualAddress;

            if (allowCPUAccess)
            {
                unsafe
                {
                    void* ptr = _cpuVirtualAddress.ToPointer();
                    r = _buffer.Map(0, &ptr);

                    _cpuVirtualAddress = (nint)ptr;
                }

                if (r.Failure)
                {
                    GfxDevice.Logger?.Error("Failed to map GPU ring buffer resource!");
                    throw new Exception();
                }
            }
        }

        public override void Dispose()
        {
            if (_cpuVirtualAddress != nint.Zero)
            {
                _buffer.Unmap(0);
            }

            _buffer.Dispose();
        }

        public new DynamicAllocation Allocate(ulong sizeInBytes)
        {
            ulong offset = base.Allocate(sizeInBytes);
            if (offset != RingBuffer.InvalidOffset)
            {
                DynamicAllocation dynAlloc = new DynamicAllocation { Buffer = _buffer, Offset = offset, Size = sizeInBytes };
                dynAlloc.GPUAddress = _gpuVirtualAddress + offset;
                dynAlloc.CPUAddress = _cpuVirtualAddress;
                if (dynAlloc.CPUAddress != nint.Zero)
                    dynAlloc.CPUAddress = _cpuVirtualAddress + (nint)offset;
                return dynAlloc;
            }
            else
            {
                return new DynamicAllocation { Buffer = null, Offset = 0, Size = 0 };
            }
        }
    }
}
