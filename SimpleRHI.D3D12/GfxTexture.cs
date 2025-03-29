using SharpGen.Runtime;
using SimpleRHI.D3D12.Helpers;
using System.Runtime.CompilerServices;
using TerraFX.Interop.DirectX;
using Vortice.Direct3D12;

using ID3D12Resource = Vortice.Direct3D12.ID3D12Resource;

namespace SimpleRHI.D3D12
{
    internal unsafe class GfxTexture : IGfxTexture, ITransitionableResource
    {
        public IGfxTexture.CreateInfo Desc => _desc;
        private readonly IGfxTexture.CreateInfo _desc;

        private ID3D12Resource _resource;
        private D3D12MA_Allocation* _allocation = null;

        private GfxDevice _device;

        public GfxTexture(in IGfxTexture.CreateInfo ci, GfxDevice device, D3D12MA_Allocator* allocator)
        {
            _desc = ci;
            _device = device;

            D3D12_RESOURCE_STATES resourceState = D3D12_RESOURCE_STATES.D3D12_RESOURCE_STATE_COMMON;
            switch (ci.Bind)
            {
                case GfxBindFlags.ShaderResource: resourceState = D3D12_RESOURCE_STATES.D3D12_RESOURCE_STATE_ALL_SHADER_RESOURCE; break;
                case GfxBindFlags.DepthStencil: resourceState = D3D12_RESOURCE_STATES.D3D12_RESOURCE_STATE_DEPTH_WRITE; break;
                case GfxBindFlags.RenderTarget: resourceState = D3D12_RESOURCE_STATES.D3D12_RESOURCE_STATE_RENDER_TARGET; break;
                case GfxBindFlags.Unkown:
                default:
                    {
                        GfxDevice.Logger?.Error("Unkown or invalid bind flag specified for texture: \"{a}\"!", ci.Bind);
                        throw new Exception();
                    }
            }

            D3D12_RESOURCE_DIMENSION dimension = D3D12_RESOURCE_DIMENSION.D3D12_RESOURCE_DIMENSION_UNKNOWN;
            switch (ci.Dimension)
            {
                case GfxTextureDimension.Texture1D: dimension = D3D12_RESOURCE_DIMENSION.D3D12_RESOURCE_DIMENSION_TEXTURE1D; break;
                case GfxTextureDimension.Texture2D: dimension = D3D12_RESOURCE_DIMENSION.D3D12_RESOURCE_DIMENSION_TEXTURE2D; break;
                case GfxTextureDimension.Texture3D: dimension = D3D12_RESOURCE_DIMENSION.D3D12_RESOURCE_DIMENSION_TEXTURE3D; break;
                default:
                    {
                        GfxDevice.Logger?.Error("Unkown or invalid dimension specified for texture: \"{a}\"!", ci.Dimension);
                        throw new Exception();
                    }
            }

            _state = (ResourceStates)resourceState;

            {
                D3D12MA_ALLOCATION_DESC allocDesc = new D3D12MA_ALLOCATION_DESC();
                allocDesc.Flags = D3D12MA_ALLOCATION_FLAGS.D3D12MA_ALLOCATION_FLAG_NONE;
                allocDesc.HeapType = D3D12_HEAP_TYPE.D3D12_HEAP_TYPE_DEFAULT;
                allocDesc.ExtraHeapFlags = D3D12_HEAP_FLAGS.D3D12_HEAP_FLAG_NONE;

                D3D12_RESOURCE_DESC resourceDesc = new D3D12_RESOURCE_DESC();
                resourceDesc.Dimension = D3D12_RESOURCE_DIMENSION.D3D12_RESOURCE_DIMENSION_TEXTURE2D;
                resourceDesc.Alignment = 0;
                resourceDesc.Width = ci.Width;
                resourceDesc.Height = ci.Height;
                resourceDesc.DepthOrArraySize = (ushort)ci.Depth;
                resourceDesc.MipLevels = (ushort)ci.MipLevels;
                resourceDesc.Format = (DXGI_FORMAT)FormatConverter.Translate(ci.Format);
                resourceDesc.Layout = D3D12_TEXTURE_LAYOUT.D3D12_TEXTURE_LAYOUT_UNKNOWN;
                resourceDesc.SampleDesc = new DXGI_SAMPLE_DESC(1, 0);
                
                //make capable for RTs aswell
                D3D12_CLEAR_VALUE clearValue = new D3D12_CLEAR_VALUE();
                clearValue.Format = resourceDesc.Format;
                clearValue.DepthStencil.Depth = 1.0f;
                clearValue.DepthStencil.Stencil = 0xff;
                
                if (ci.Bind.HasFlag(GfxBindFlags.RenderTarget))
                    resourceDesc.Flags |= D3D12_RESOURCE_FLAGS.D3D12_RESOURCE_FLAG_ALLOW_RENDER_TARGET;
                if (ci.Bind.HasFlag(GfxBindFlags.DepthStencil))
                    resourceDesc.Flags |= D3D12_RESOURCE_FLAGS.D3D12_RESOURCE_FLAG_ALLOW_DEPTH_STENCIL;

                void* res = null;
                fixed (D3D12MA_Allocation** alloc = &_allocation)
                {
                    Guid guid = typeof(ID3D12Resource).GUID;
                    Result r = new Result(allocator->CreateResource(&allocDesc, &resourceDesc, resourceState, (ci.Bind == GfxBindFlags.DepthStencil ? &clearValue : null), alloc, &guid, &res).Value);

                    if (r.Failure)
                    {
                        GfxDevice.Logger?.Error("Failed to create texture: {a}!", r.Code);
                        throw new Exception(r.Code.ToString());
                    }
                }

                _resource = new ID3D12Resource((nint)res);

                _resource.Name = ci.Name;
            }
        }

        public void Dispose()
        {
            _resource?.Dispose();
            if (_allocation != null)
                _allocation->Release();
        }

        public IGfxTextureView CreateView(in IGfxTextureView.CreateInfo ci)
        {
            return new GfxTextureView(ci, this, _device);
        }

        #region ITransitionableResource
        private ResourceStates _state;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void TransitionIfRequired(ID3D12GraphicsCommandList10 commandBuffer, ResourceStates newState, bool isPlacebo = false)
        {
            if (_state != newState)
            {
                commandBuffer.ResourceBarrierTransition(_resource, _state, newState);
                if (!isPlacebo)
                    _state = newState;
            }
        }

        public ResourceStates CurrentState => _state;
        #endregion

        public ID3D12Resource D3D12Resource => _resource;
    }
}
