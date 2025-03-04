using SimpleRHI.D3D12.Descriptors;
using SimpleRHI.D3D12.Helpers;
using Vortice.Direct3D12;

namespace SimpleRHI.D3D12
{
    internal class GfxBufferView : BindablePipelineResource, IGfxBufferView
    {
        public IGfxBufferView.CreateInfo Desc => _desc;
        private IGfxBufferView.CreateInfo _desc;

        private GfxBuffer _parent;
        private DescriptorHeapAllocation? _allocation;

        public GfxBufferView(in IGfxBufferView.CreateInfo ci, GfxBuffer parent, GfxDevice device)
        {
            _desc = ci;
            _parent = parent;

            if (parent.Desc.Bind == GfxBindFlags.ConstantBuffer || parent.Desc.Bind == GfxBindFlags.ShaderResource)
            {
                _allocation = device.CPUDescriptors_SRV_CBV_UAV.Allocate(1);

                if (parent.Desc.Bind == GfxBindFlags.ConstantBuffer)
                {
                    device.D3D12Device.CreateConstantBufferView(null, _allocation.Value.GetCPUHandle());
                }
                else if (parent.Desc.Bind == GfxBindFlags.ShaderResource)
                {
                    device.D3D12Device.CreateShaderResourceView(parent.D3D12Resource, new ShaderResourceViewDescription
                    {
                        Buffer = new BufferShaderResourceView
                        {
                            FirstElement = 0u,
                            NumElements = (uint)(_parent.Desc.Size / ci.Stride),
                            StructureByteStride = ci.Stride,
                            Flags = BufferShaderResourceViewFlags.None
                        },
                        ViewDimension = ShaderResourceViewDimension.Buffer,
                        Shader4ComponentMapping = ShaderComponentMapping.Default
                    }, _allocation.Value.GetCPUHandle());
                }
            }
        }

        public void Dispose()
        {
            if (_allocation.HasValue)
                _allocation.Value.Free();
        }

        public override DescriptorHeapAllocation GetHeapAllocation()
        {
            return _allocation ?? throw new NullReferenceException();
        }

        public override ulong GetLocation()
        {
            return _parent.GPUVirtualAddress;
        }

        public GfxBuffer Buffer => _parent;
        public DescriptorHeapAllocation? Allocation => _allocation;
    }
}
