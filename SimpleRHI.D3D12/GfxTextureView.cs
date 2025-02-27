using SimpleRHI.D3D12.Descriptors;
using SimpleRHI.D3D12.Helpers;

namespace SimpleRHI.D3D12
{
    internal class GfxTextureView : BindablePipelineResource, IGfxTextureView
    {
        public IGfxTextureView.CreateInfo Desc => _desc;
        private IGfxTextureView.CreateInfo _desc;

        private GfxTexture _parent;
        private DescriptorHeapAllocation _srv;

        public GfxTextureView(in IGfxTextureView.CreateInfo ci, GfxTexture parent, GfxDevice device)
        {
            _desc = ci;
            _parent = parent;

            switch (ci.Type)
            {
                case GfxTextureViewType.ShaderResource:
                    _srv = device.CPUDescriptors_SRV_CBV_UAV.Allocate(1);
                    device.D3D12Device.CreateShaderResourceView(parent.D3D12Resource, null, _srv.GetCPUHandle());
                    break;
                case GfxTextureViewType.RenderTarget:
                    _srv = device.CPUDescriptors_RTV.Allocate(1);
                    device.D3D12Device.CreateRenderTargetView(parent.D3D12Resource, null, _srv.GetCPUHandle());
                    break;
                case GfxTextureViewType.DepthStencil:
                    _srv = device.CPUDescriptors_DSV.Allocate(1);
                    device.D3D12Device.CreateDepthStencilView(parent.D3D12Resource, null, _srv.GetCPUHandle());
                    break;
                default: throw new Exception("TODO");
            }
        }

        public void Dispose()
        {
            _srv.Free();
        }

        public override DescriptorHeapAllocation GetHeapAllocation()
        {
            return _srv;
        }

        public DescriptorHeapAllocation Descriptor => _srv;
    }
}
