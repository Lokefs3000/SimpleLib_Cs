using SimpleRHI.D3D12.Descriptors;
using System.Runtime.CompilerServices;
using Vortice.Direct3D12;
using Vortice.DXGI;

namespace SimpleRHI.D3D12
{
    internal class GfxSwapChain : IGfxSwapChain
    {
        public IGfxSwapChain.CreateInfo Desc => _desc;
        private IGfxSwapChain.CreateInfo _desc;

        public IGfxTextureView RenderTargetView => _rtv;
        public IGfxTextureView? DepthStencilView => _depthTextureView;

        private IDXGISwapChain4 _swapChain;

        private CustomTextureView _rtv;

        private bool _isTransitionedToRTV = false;

        private int _bufferIndex = 0;
        private ID3D12Resource[] _buffers;
        private DescriptorHeapAllocation _descriptor;

        private GfxTexture? _depthTexture;
        private GfxTextureView? _depthTextureView;

        private GfxGraphicsCommandBuffer? _lastInteractedCommandBuffer = null;

        public GfxSwapChain(IGfxSwapChain.CreateInfo ci, GfxDevice device)
        {
            _desc = ci;

            SwapChainDescription1 swapChainDesc = new SwapChainDescription1
            {
                Width = ci.Width,
                Height = ci.Height,
                Format = FormatConverter.Translate(ci.ColorFormat),
                Stereo = false,
                SampleDescription = SampleDescription.Default,
                BufferUsage = Usage.Backbuffer,
                BufferCount = ci.BufferCount,
                Scaling = Scaling.None,
                SwapEffect = SwapEffect.FlipSequential,
                AlphaMode = AlphaMode.Unspecified,
                Flags = SwapChainFlags.None
            };

            {
                try
                {
                    using IDXGISwapChain1 swapChain1 = device.DXGIFactory.CreateSwapChainForHwnd(((GfxCommandQueue)ci.GraphicsCommandQueue).D3D12CommandQueue, ci.WindowHandle, swapChainDesc);
                    _swapChain = swapChain1.QueryInterface<IDXGISwapChain4>();
                }
                catch (Exception)
                {
                    GfxDevice.Logger?.Error("Failed to create swapchain!");
                    throw;
                }
            }

            _buffers = new ID3D12Resource[ci.BufferCount];
            _descriptor = device.CPUDescriptors_RTV.Allocate((uint)_buffers.Length);
            for (int i = 0; i < _buffers.Length; i++)
            {
                _buffers[i] = _swapChain.GetBuffer<ID3D12Resource>((uint)i);
                device.D3D12Device.CreateRenderTargetView(_buffers[i], null, _descriptor.GetCPUHandle((uint)i));
            }

            _bufferIndex = (int)_swapChain.CurrentBackBufferIndex;

            _rtv = new CustomTextureView(this);
            _rtv.Set(_descriptor.GetCPUHandle(), _descriptor.GetGPUHandle());

            if (ci.DepthFormat != GfxFormat.Unkown)
            {
                _depthTexture = (GfxTexture)device.CreateTexture(new IGfxTexture.CreateInfo
                {
                    Name = "SwapChain_DSV",
                    Width = ci.Width,
                    Height = ci.Height,
                    Depth = 1,
                    Bind = GfxBindFlags.DepthStencil,
                    Dimension = GfxTextureDimension.Texture2D,
                    MipLevels = 1,
                    Format = ci.DepthFormat,
                });

                _depthTextureView = (GfxTextureView)_depthTexture.CreateView(new IGfxTextureView.CreateInfo
                {
                    Type = GfxTextureViewType.DepthStencil
                });
            }
        }

        public void Dispose()
        {
            _depthTextureView?.Dispose();
            _depthTexture?.Dispose();

            for (int i = 0; i < 3; i++)
            {
                
                _buffers[i].Dispose();
            }

            _descriptor.Free();
            _swapChain.Dispose();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Transition(GfxGraphicsCommandBuffer commandList, bool toRTV)
        {
            if (toRTV && !_isTransitionedToRTV)
            {
                commandList.D3D12GraphicsCommandList.ResourceBarrierTransition(_buffers[_bufferIndex], ResourceStates.Present, ResourceStates.RenderTarget);
            }
            else if (!toRTV && _isTransitionedToRTV)
            {
                commandList.D3D12GraphicsCommandList.ResourceBarrierTransition(_buffers[_bufferIndex], ResourceStates.RenderTarget, ResourceStates.Present);
            }

            _isTransitionedToRTV = toRTV;
            _lastInteractedCommandBuffer = toRTV ? commandList : null;
        }

        public void Present(uint vsync)
        {
            _swapChain.Present(vsync);
            _bufferIndex = (int)_swapChain.CurrentBackBufferIndex;

            _rtv.Set(_descriptor.GetCPUHandle((uint)_bufferIndex), _descriptor.GetGPUHandle((uint)_bufferIndex));
            _lastInteractedCommandBuffer = null;
        }

        public GfxGraphicsCommandBuffer? LastInteractedWithCommandBuffer => _lastInteractedCommandBuffer;

        public class CustomTextureView : IGfxTextureView
        {
            public IGfxTextureView.CreateInfo Desc => throw new ArgumentNullException("Not available");

            private GfxSwapChain _swapChain;

            private CpuDescriptorHandle _cpu;
            private GpuDescriptorHandle _gpu;

            public CustomTextureView(GfxSwapChain swapChain)
            {
                _swapChain = swapChain;
            }

            public void Dispose()
            {

            }

            public void Set(CpuDescriptorHandle cpu, GpuDescriptorHandle gpu)
            {
                _cpu = cpu;
                _gpu = gpu;
            }

            public GfxSwapChain SwapChain => _swapChain;
            public CpuDescriptorHandle CPU => _cpu;
            public GpuDescriptorHandle GPU => _gpu;
        }
    }
}
