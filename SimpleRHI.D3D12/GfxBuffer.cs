using SharpGen.Runtime;
using SimpleRHI.D3D12.Helpers;
using System.Runtime.CompilerServices;
using TerraFX.Interop.DirectX;
using Vortice.Direct3D12;

using ID3D12Resource = Vortice.Direct3D12.ID3D12Resource;

namespace SimpleRHI.D3D12
{
    internal unsafe class GfxBuffer : IGfxBuffer, ITransitionableResource
    {
        public IGfxBuffer.CreateInfo Desc => _desc;
        private readonly IGfxBuffer.CreateInfo _desc;

        private ID3D12Resource _resource;
        private D3D12MA_Allocation* _allocation = null;

        //private List<KeyValuePair<int, DynamicAllocation>> _allocations = new List<KeyValuePair<int, DynamicAllocation>>();

        private ulong _gpuVirtualAddress = 0;

        private nint _activeMap = nint.Zero;

        private GfxDevice _device;

        public GfxBuffer(in IGfxBuffer.CreateInfo ci, GfxDevice device, D3D12MA_Allocator* allocator)
        {
            _desc = ci;
            _device = device;

            D3D12_RESOURCE_STATES resourceState = D3D12_RESOURCE_STATES.D3D12_RESOURCE_STATE_COMMON;
            switch (ci.Bind)
            {
                case GfxBindFlags.VertexBuffer: resourceState = D3D12_RESOURCE_STATES.D3D12_RESOURCE_STATE_VERTEX_AND_CONSTANT_BUFFER; break;
                case GfxBindFlags.IndexBuffer: resourceState = D3D12_RESOURCE_STATES.D3D12_RESOURCE_STATE_INDEX_BUFFER; break;
                case GfxBindFlags.ConstantBuffer: resourceState = D3D12_RESOURCE_STATES.D3D12_RESOURCE_STATE_VERTEX_AND_CONSTANT_BUFFER; break;
                case GfxBindFlags.ShaderResource: resourceState = D3D12_RESOURCE_STATES.D3D12_RESOURCE_STATE_ALL_SHADER_RESOURCE; break;
                case GfxBindFlags.Unkown:
                default:
                    {
                        GfxDevice.Logger?.Error("Unkown or invalid bind flag specified for buffer: \"{a}\"!", ci.Bind);
                        throw new Exception();
                    }
            }

            _state = (ResourceStates)resourceState;

            {
                D3D12MA_ALLOCATION_DESC allocDesc = new D3D12MA_ALLOCATION_DESC();
                allocDesc.Flags = D3D12MA_ALLOCATION_FLAGS.D3D12MA_ALLOCATION_FLAG_NONE;
                allocDesc.HeapType = (ci.MemoryUsage == GfxMemoryUsage.Staging ? D3D12_HEAP_TYPE.D3D12_HEAP_TYPE_UPLOAD : D3D12_HEAP_TYPE.D3D12_HEAP_TYPE_DEFAULT);
                allocDesc.ExtraHeapFlags = D3D12_HEAP_FLAGS.D3D12_HEAP_FLAG_NONE;

                D3D12_RESOURCE_DESC resourceDesc = new D3D12_RESOURCE_DESC();
                resourceDesc.Dimension = D3D12_RESOURCE_DIMENSION.D3D12_RESOURCE_DIMENSION_BUFFER;
                resourceDesc.Alignment = 0;
                resourceDesc.Width = ci.Size;
                resourceDesc.Height = 1;
                resourceDesc.DepthOrArraySize = 1;
                resourceDesc.MipLevels = 1;
                resourceDesc.Format = DXGI_FORMAT.DXGI_FORMAT_UNKNOWN;
                resourceDesc.Layout = D3D12_TEXTURE_LAYOUT.D3D12_TEXTURE_LAYOUT_ROW_MAJOR;
                resourceDesc.SampleDesc = new DXGI_SAMPLE_DESC(1, 0);

                void* res = null;
                fixed (D3D12MA_Allocation** alloc = &_allocation)
                {
                    Guid guid = typeof(ID3D12Resource).GUID;
                    Result r = new Result(allocator->CreateResource(&allocDesc, &resourceDesc, resourceState, null, alloc, &guid, &res).Value);

                    if (r.Failure)
                    {
                        GfxDevice.Logger?.Error("Failed to create buffer: {a}!", r.Code);
                        _device.DumpInfoLog();
                        throw new Exception(r.Code.ToString());
                    }
                }

                _resource = new ID3D12Resource((nint)res);
                _gpuVirtualAddress = _resource.GPUVirtualAddress;

                _resource.Name = ci.Name;
            }
        }

        public void Dispose()
        {
            if (_activeMap != nint.Zero)
                Unmap();

            _resource?.Dispose();
            if (_allocation != null)
                _allocation->Release();
        }

        public IGfxBufferView CreateView(in IGfxBufferView.CreateInfo ci)
        {
            return new GfxBufferView(ci, this, _device);
        }

        public unsafe Span<T> Map<T>(GfxMapType type, GfxMapFlags flags) where T : unmanaged
        {
            if (_activeMap != nint.Zero)
            {
                return new Span<T>(_activeMap.ToPointer(), (int)(_desc.Size / (ulong)sizeof(T)));
            }

            void* ptr = null;
            Result r = _resource.Map(0, ptr);

            _activeMap = (nint)ptr;
            return new Span<T>(_activeMap.ToPointer(), (int)(_desc.Size / (ulong)sizeof(T)));
        }
        public unsafe nint Map(GfxMapType type, GfxMapFlags flags)
        {
            if (_activeMap != nint.Zero)
            {
                return _activeMap;
            }

            void* ptr = null;
            Result r = _resource.Map(0, &ptr);

            _activeMap = (nint)ptr;
            return _activeMap;
        }
        public unsafe void Unmap()
        {
            if (_activeMap != nint.Zero)
            {
                _resource.Unmap(0);
                _activeMap = nint.Zero;
            }
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
        public ulong GPUVirtualAddress => _gpuVirtualAddress;
    }
}
