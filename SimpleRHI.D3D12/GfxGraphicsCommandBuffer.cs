using SharpGen.Runtime;
using SimpleRHI.D3D12.Allocators;
using SimpleRHI.D3D12.Descriptors;
using SimpleRHI.D3D12.Helpers;
using SimpleRHI.D3D12.Memory;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Vortice;
using Vortice.Direct3D;
using Vortice.Direct3D12;
using Vortice.DXGI;
using Vortice.Mathematics;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace SimpleRHI.D3D12
{
    internal class GfxGraphicsCommandBuffer : IGfxGraphicsCommandBuffer
    {
        public IGfxGraphicsCommandBuffer.CreateInfo Desc { get; }
        private IGfxGraphicsCommandBuffer.CreateInfo _desc;

        private GfxDevice _device;
        private ushort _id;

        private ID3D12CommandAllocator _commandAllocator;
        private ID3D12GraphicsCommandList10 _commandList;

        private DescriptorSuballocationsManager _descriptorSuballocator;
        private DynamicUploadHeap _ringBuffer;

        private AlignedBlockAllocator _constantAllocator;

        private bool _enablePIX;
        private bool _hasValidation;
        private bool _isOpen;

        private DirtyHandle<DescriptorHeapAllocation>[] _rtv;
        private DirtyHandle<DescriptorHeapAllocation> _dsv;

        private DirtyHandle<Vector4>[] _viewports;
        private DirtyHandle<Vector4>[] _scissors;

        private DirtyHandle<VertexBufferViewData>[] _vertexBuffer;
        private DirtyHandle<IndexBufferViewData> _indexBuffer;

        private DirtyHandleClass<GfxGraphicsPipeline> _pipelineState;
        private DirtyHandle<GfxPrimitiveTopology> _primitiveTopology;

        private DirtyHandleClass<BindablePipelineResource>[] _srv;
        private DirtyHandleClass<BindablePipelineResource>[] _uav;
        private DirtyHandleClass<BindablePipelineResource>[] _cbv;

        private DirtyHandleSpan<uint>[] _constants;

        private bool _rtv_dsv_modified = false;
        private bool _input_layout_modified = false;
        private bool _pipeline_modified = false;
        private bool _shader_resources_modified = false;
        private bool _topology_modified = false;

        private List<BindablePipelineResource> _resources = new List<BindablePipelineResource>();
        private List<KeyValuePair<GfxBuffer, DynamicAllocation>> _mapped = new List<KeyValuePair<GfxBuffer, DynamicAllocation>>();

        private List<GfxSwapChain> _transtionedSwapChains = new List<GfxSwapChain>();

        public GfxGraphicsCommandBuffer(in IGfxGraphicsCommandBuffer.CreateInfo ci, GfxDevice device, bool enablePIX, bool validation, ushort id)
        {
            _device = device;
            _id = id;

            Result r = device.D3D12Device.CreateCommandAllocator(FormatConverter.Translate(ci.Type), out _commandAllocator);
            if (r.Failure || _commandAllocator == null)
            {
                GfxDevice.Logger?.Error("Failed to create command allocator!");
                throw new Exception(r.Code.ToString());
            }

            r = device.D3D12Device.CreateCommandList(0, FormatConverter.Translate(ci.Type), _commandAllocator, null, out _commandList);
            if (r.Failure || _commandList == null)
            {
                _commandAllocator?.Dispose();

                GfxDevice.Logger?.Error("Failed to create command allocator!");
                throw new Exception(r.Code.ToString());
            }

            _commandList?.Close();

            _descriptorSuballocator = device.GPUDescriptors_CBV_SRV_UAV.CreateSuballocator();
            _ringBuffer = device.GetRingBuffer();

            _constantAllocator = new AlignedBlockAllocator(128, 16);

            _enablePIX = enablePIX;
            _hasValidation = validation;
            _isOpen = false;

            _rtv = new DirtyHandle<DescriptorHeapAllocation>[8];
            _dsv = new DirtyHandle<DescriptorHeapAllocation>();

            _viewports = new DirtyHandle<Vector4>[8];
            _scissors = new DirtyHandle<Vector4>[8];

            _vertexBuffer = new DirtyHandle<VertexBufferViewData>[16];
            _indexBuffer = new DirtyHandle<IndexBufferViewData>();

            _pipelineState = new DirtyHandleClass<GfxGraphicsPipeline>();
            _primitiveTopology = new DirtyHandle<GfxPrimitiveTopology>();

            _srv = new DirtyHandleClass<BindablePipelineResource>[16];
            _uav = new DirtyHandleClass<BindablePipelineResource>[16];
            _cbv = new DirtyHandleClass<BindablePipelineResource>[16];

            _constants = new DirtyHandleSpan<uint>[16];

            for (int i = 0; i < 8; i++)
            {
                _rtv[i] = new DirtyHandle<DescriptorHeapAllocation>();
                _viewports[i] = new DirtyHandle<Vector4>();
                _scissors[i] = new DirtyHandle<Vector4>();
            }
            for (int i = 0; i < 16; i++)
            {
                _vertexBuffer[i] = new DirtyHandle<VertexBufferViewData>();
                _srv[i] = new DirtyHandleClass<BindablePipelineResource>();
                _uav[i] = new DirtyHandleClass<BindablePipelineResource>();
                _cbv[i] = new DirtyHandleClass<BindablePipelineResource>();
                _constants[i] = new DirtyHandleSpan<uint>();
            }
        }

        public void Dispose()
        {
            if (_isOpen)
            {
                Close();
            }

            _commandList.Dispose();
            _commandAllocator.Dispose();

            _constantAllocator.Dispose();

            _device.ReturnRingBuffer(_ringBuffer);
            _device.GPUDescriptors_CBV_SRV_UAV.DestroySuballocator(_descriptorSuballocator);
        }

        private void ResetAllBindings()
        {
            for (int i = 0; i < _rtv.Length; i++)
            {
                _rtv[i].Value = null;
                _viewports[i].Value = null;
                _scissors[i].Value = null;
            }

            for (int i = 0; i < 16; i++)
            {
                _vertexBuffer[i].Value = null;
                _srv[i].Value = null;
                _uav[i].Value = null;
                _cbv[i].Value = null;
                unsafe { _constants[i].Value = null; }
            }

            _dsv.Value = null;
            _indexBuffer.Value = null;
            _pipelineState.Value = null;
            _primitiveTopology.Value = null;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Begin()
        {
            if (_isOpen)
            {
                //throw new InvalidOperationException("Cannot begin open command list!");
                return;
            }

            _isOpen = true;

            _commandAllocator.Reset();
            _commandList.Reset(_commandAllocator);

            _commandList.ClearState(null);
            _commandList.SetDescriptorHeaps(_device.GPUDescriptors_CBV_SRV_UAV.D3D12DescriptorHeap);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Close()
        {
            if (!_isOpen)
            {
                //throw new InvalidOperationException("Cannot close non-open command list!");
                return;
            }

            for (int i = 0; i < _transtionedSwapChains.Count; i++)
                if (_transtionedSwapChains[i].LastInteractedWithCommandBuffer == this)
                    _transtionedSwapChains[i].Transition(this, false);
            _transtionedSwapChains.Clear();

            for (int i = 0; i < _resources.Count; i++)
                _resources[i].ResetIndiceAtIndex(_id);
            _resources.Clear();

            if (_mapped.Count > 0)
            {
                GfxDevice.Logger?.Warning("Leaked active map allocations!!");
            }

            _cachedAlloc?.Free();
            _cachedAlloc = null;

            _unresolvedResources.Clear();

            ResetAllBindings();

            try
            {
                _commandList.Close();
            }
            catch (Exception ex)
            {
                GfxDevice.Logger?.Error(ex, "Failed to close commandlist!");
            }

            _isOpen = false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void BeginDebugGroup(string name, in Color4 color)
        {
            if (_enablePIX)
            {
                PIXNATIVE.PIXBeginEventOnCommandList(_commandList.NativePointer, color.ToRgba(), name);
            }
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void EndDebugGroup()
        {
            if (_enablePIX)
            {
                PIXNATIVE.PIXEndEventOnCommandList(_commandList.NativePointer);
            }
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetDebugMarker(string name, in Color4 color)
        {
            if (_enablePIX)
            {
                PIXNATIVE.PIXSetMarkerOnCommandList(_commandList.NativePointer, color.ToRgba(), name);
            }
        }

        public void ClearRenderTarget(IGfxTextureView rtv, in Color4 rgba)
        {
            if (rtv is GfxSwapChain.CustomTextureView)
            {
                GfxSwapChain.CustomTextureView view = (GfxSwapChain.CustomTextureView)rtv;
                view.SwapChain.Transition(this, true);

                if (!_transtionedSwapChains.Contains(view.SwapChain))
                {
                    _transtionedSwapChains.Add(view.SwapChain);
                }

                _commandList.ClearRenderTargetView(view.CPU, rgba);
            }
            else
            {
                GfxTextureView view = (GfxTextureView)rtv;
                _commandList.ClearRenderTargetView(view.Descriptor.GetCPUHandle(), rgba);
            }
        }
        public void ClearDepthStencil(IGfxTextureView depthStencil, float depth = 1.0f, byte stencil = 0xff)
        {
            GfxTextureView view = (GfxTextureView)depthStencil;

            ClearFlags flags = 0;
            if (depth != 1.0f)
                flags |= ClearFlags.Depth;
            if (stencil != 0xff)
                flags |= ClearFlags.Stencil;

            if (flags > 0)
            {
                _commandList.ClearDepthStencilView(view.Descriptor.GetCPUHandle(), flags, depth, stencil);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetRenderTarget(IGfxTextureView? rtv, uint slot = 0, IGfxTextureView? depthStencil = null)
        {
            if (rtv is GfxSwapChain.CustomTextureView)
            {
                var span = _rtv.AsSpan();
                GfxSwapChain.CustomTextureView view = (GfxSwapChain.CustomTextureView)rtv;
                view.SwapChain.Transition(this, true);

                if (!_transtionedSwapChains.Contains(view.SwapChain))
                {
                    _transtionedSwapChains.Add(view.SwapChain);
                }
                span[(int)slot].Value = new DescriptorHeapAllocation(view.CPU);
                _dsv.Value = ((GfxTextureView?)depthStencil)?.Descriptor;
                _rtv_dsv_modified = true;
            }
            else
            {
                var span = _rtv.AsSpan();
                span[(int)slot].Value = ((GfxTextureView?)rtv)?.Descriptor;
                _dsv.Value = ((GfxTextureView?)depthStencil)?.Descriptor;
                _rtv_dsv_modified = true;
            }
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetRenderTargets(ReadOnlySpan<IGfxTextureView?> rtv, uint slot = 0, IGfxTextureView? depthStencil = null)
        {
            var span = _rtv.AsSpan();

            for (int i = (int)slot; i < rtv.Length; i++)
            {
                if (rtv[i] is GfxSwapChain.CustomTextureView)
                {
                    GfxSwapChain.CustomTextureView view = (GfxSwapChain.CustomTextureView)rtv[i];
                    view.SwapChain.Transition(this, true);

                    if (!_transtionedSwapChains.Contains(view.SwapChain))
                    {
                        _transtionedSwapChains.Add(view.SwapChain);
                    }

                    span[i].Value = new DescriptorHeapAllocation(view.CPU);
                }
                else
                {
                    span[i].Value = ((GfxTextureView?)rtv[i])?.Descriptor;
                }
            }

            _dsv.Value = ((GfxTextureView?)depthStencil)?.Descriptor;

            _rtv_dsv_modified = true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetViewport(Vector4 viewport, uint slot = 0)
        {
            var span = _viewports.AsSpan();
            span[(int)slot].Value = viewport;
            _rtv_dsv_modified = true;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetViewports(ReadOnlySpan<Vector4> viewports, uint slot = 0)
        {
            var span = _viewports.AsSpan((int)slot);
            for (int i = 0; i < viewports.Length; i++)
            {
                span[i].Value = viewports[i];
            }
            _rtv_dsv_modified = true;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetScissor(Vector4 scissor, uint slot = 0)
        {
            var span = _scissors.AsSpan();
            span[(int)slot].Value = scissor;
            _rtv_dsv_modified = true;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetScissors(ReadOnlySpan<Vector4> scissors, uint slot = 0)
        {
            var span = _scissors.AsSpan((int)slot);
            for (int i = 0; i < scissors.Length; i++)
            {
                span[i].Value = scissors[i];
            }
            _rtv_dsv_modified = true;
        }


        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetVertexBuffer(IGfxBufferView? buffer, uint slot = 0, uint stride = 0, ulong offset = 0)
        {
            if (buffer == null)
            {
                _vertexBuffer[slot].Value = null;
            }
            else
            {
                GfxBufferView bufferView = (GfxBufferView)buffer;
                _vertexBuffer[slot].Value = new VertexBufferViewData
                {
                    BufferView = bufferView,
                    Stride = (uint)stride
                };
            }

            _input_layout_modified = true;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetVertexBuffers(ReadOnlySpan<IGfxBufferView?> buffers, uint startSlot, uint[] strides, ulong[] offsets)
        {
            for (int i = (int)startSlot; i < buffers.Length + startSlot; i++)
            {
                if (buffers[i] == null)
                {
                    _vertexBuffer[i].Value = null;
                }
                else
                {
                    GfxBufferView bufferView = (GfxBufferView)buffers[i];
                    _vertexBuffer[i].Value = new VertexBufferViewData
                    {
                        BufferView = bufferView,
                        Stride = (uint)strides[i]
                    };
                }
            }

            _input_layout_modified = true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetIndexBuffer(IGfxBufferView? buffer, ulong offset = 0)
        {
            if (buffer == null)
            {
                _indexBuffer.Value = null;
            }
            else
            {
                GfxBufferView bufferView = (GfxBufferView)buffer;
                _indexBuffer.Value = new IndexBufferViewData
                {
                    BufferView = bufferView,
                    Stride = bufferView.Desc.Stride == 2 ? Format.R16_UInt : Format.R32_UInt
                };
            }

            _input_layout_modified = true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetConstantBuffer(IGfxBufferView? buffer, uint slot = 0)
        {
            var span = _cbv.AsSpan();
            span[(int)slot].Value = ((GfxBufferView?)buffer);
            _shader_resources_modified = true;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetConstantBuffers(ReadOnlySpan<IGfxBufferView?> buffer, uint slot = 0)
        {
            var span = _cbv.AsSpan();

            for (int i = (int)slot; i < buffer.Length + slot; i++)
            {
                span[i].Value = ((GfxBufferView?)buffer[i]);
            }

            _shader_resources_modified = true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public unsafe void Set32BitConstants(uint index, ReadOnlySpan<uint> src)
        {
            Span<uint> dst = _constantAllocator.At<uint>(index);
            src.CopyTo(dst);

            _constants[index].Value = dst.GetPointerUnsafe();
            _shader_resources_modified = true;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public unsafe void Set32BitConstants(uint index, uint numBits, nint src)
        {
            Span<uint> dst = _constantAllocator.At<uint>(index);
            NativeMemory.Copy(src.ToPointer(), dst.GetPointerUnsafe(), numBits * sizeof(uint));

            _constants[index].Value = dst.GetPointerUnsafe();
            _shader_resources_modified = true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetShaderResource(GfxShaderType stage, IGfxBufferView? resource, uint slot = 0)
        {
            var span = _srv.AsSpan();
            span[(int)slot].Value = ((GfxBufferView?)resource);
            _shader_resources_modified = true;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetShaderResources(GfxShaderType stage, ReadOnlySpan<IGfxBufferView?> resources, uint slot = 0)
        {
            var span = _srv.AsSpan();

            for (int i = (int)slot; i < resources.Length + slot; i++)
            {
                span[i].Value = ((GfxBufferView?)resources[i]);
            }

            _shader_resources_modified = true;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetShaderResource(GfxShaderType stage, IGfxTextureView? resource, uint slot = 0)
        {
            var span = _srv.AsSpan();
            span[(int)slot].Value = ((GfxTextureView?)resource);
            _shader_resources_modified = true;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetShaderResources(GfxShaderType stage, ReadOnlySpan<IGfxTextureView?> resources, uint slot = 0)
        {
            var span = _srv.AsSpan();

            for (int i = (int)slot; i < resources.Length + slot; i++)
            {
                span[i].Value = ((GfxTextureView?)resources[i]);
            }

            _shader_resources_modified = true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetPipelineState(IGfxGraphicsPipeline? pipeline)
        {
            _pipelineState.Value = (GfxGraphicsPipeline?)pipeline;
            _pipeline_modified = true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetPrimitiveToplogy(GfxPrimitiveTopology topology)
        {
            _primitiveTopology.Value = topology;
            _topology_modified = true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public unsafe void DrawIndexed(in IGfxGraphicsCommandBuffer.DrawIndexedAttribs attribs)
        {
            GfxGraphicsPipeline? pipeline = _pipelineState.Value;
            if (pipeline == null)
            {
                return;
            }

            if (_rtv_dsv_modified)
                BindRTVsAndDSV();
            if (_input_layout_modified)
                BindVertexBuffersAndIndexBuffer();
            if (_shader_resources_modified || _pipeline_modified || _topology_modified)
                BindShaderResources();

            _commandList.DrawIndexedInstanced(attribs.IndexCount, 1, attribs.IndexOffset, attribs.BaseVertex, 0);
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void DrawInstancedIndexed(in IGfxGraphicsCommandBuffer.DrawIndexedInstancedAttribs attribs)
        {
            throw new NotImplementedException();
        }

        public unsafe Span<T> Map<T>(IGfxBuffer buffer, GfxMapType type, GfxMapFlags flags) where T : unmanaged
        {
            if (_hasValidation)
            {
                for (int i = 0; i < _mapped.Count; i++)
                {
                    if (_mapped[i].Key == buffer)
                    {
                        throw new InvalidOperationException("Buffer already mapped!");
                    }
                }
            }

            DynamicAllocation allocation = _ringBuffer.Allocate(((GfxBuffer)buffer).Desc.Size);
            if (type.HasFlag(GfxMapType.Read))
                throw new NotImplementedException();

            _mapped.Add(new KeyValuePair<GfxBuffer, DynamicAllocation>((GfxBuffer)buffer, allocation));

            return new Span<T>(allocation.CPUAddress.ToPointer(), (int)(allocation.Size / (ulong)sizeof(T)));
        }
        public nint Map(IGfxBuffer buffer, GfxMapType type, GfxMapFlags flags)
        {
            if (_hasValidation)
            {
                for (int i = 0; i < _mapped.Count; i++)
                {
                    if (_mapped[i].Key == buffer)
                    {
                        throw new InvalidOperationException("Buffer already mapped!");
                    }
                }
            }

            DynamicAllocation allocation = _ringBuffer.Allocate(((GfxBuffer)buffer).Desc.Size);
            if (type.HasFlag(GfxMapType.Read))
                throw new NotImplementedException();

            _mapped.Add(new KeyValuePair<GfxBuffer, DynamicAllocation>((GfxBuffer)buffer, allocation));

            return allocation.CPUAddress;
        }
        public void Unmap(IGfxBuffer buffer)
        {
            Span<KeyValuePair<GfxBuffer, DynamicAllocation>> mapped = CollectionsMarshal.AsSpan(_mapped);
            for (int i = 0; i < mapped.Length; i++)
            {
                ref KeyValuePair<GfxBuffer, DynamicAllocation> kvp = ref mapped[i];
                if (mapped[i].Key == buffer)
                {
                    DynamicAllocation alloc = kvp.Value;
                    if (alloc.Buffer != null)
                    {
                        GfxBuffer buf = (GfxBuffer)buffer;

                        buf.TransitionIfRequired(_commandList, ResourceStates.CopyDest);
                        _commandList.CopyBufferRegion(buf.D3D12Resource, 0, alloc.Buffer, alloc.Offset, alloc.Size);
                    }

                    _mapped.RemoveAt(i);
                    break;
                }
            }
        }

        public unsafe void CopyTextureResource(IGfxBuffer buffer, ulong offset, ulong size, uint pitch, IGfxTexture texture, uint subresource)
        {
            GfxBuffer buf = (GfxBuffer)buffer;
            GfxTexture tex = (GfxTexture)texture;

            buf.TransitionIfRequired(_commandList, ResourceStates.CopySource);
            tex.TransitionIfRequired(_commandList, ResourceStates.CopyDest);

            TextureCopyLocation src = new TextureCopyLocation(buf.D3D12Resource, new PlacedSubresourceFootPrint()
            {
                Offset = offset,
                Footprint = new SubresourceFootPrint
                {
                    Format = FormatConverter.Translate(tex.Desc.Format),
                    Width = tex.Desc.Width,
                    Height = tex.Desc.Height,
                    Depth = tex.Desc.Depth,
                    RowPitch = pitch,
                }
            });

            TextureCopyLocation dst = new TextureCopyLocation(tex.D3D12Resource, subresource/*MipSlice*/ + (0/*ArraySlice*/ * tex.Desc.MipLevels));

            _commandList.CopyTextureRegion(dst, new Int3(0), src);
        }

        private void BindRTVsAndDSV()
        {
            Span<DirtyHandle<DescriptorHeapAllocation>> rtvs = new Span<DirtyHandle<DescriptorHeapAllocation>>(_rtv);

            bool didChangeAnyViews = _dsv.Dirty;
            int rtvCount = 0;

            for (int i = 0; i < 8; i++)
            {
                ref DirtyHandle<DescriptorHeapAllocation> rtv = ref rtvs[i];
                if (rtv.Dirty)
                {
                    _rtvDescriptors[i] = rtv.Value.HasValue ? rtv.Value.Value.GetCPUHandle() : _device.CPUDescriptors_RTV.NullDescriptor;

                    rtv.Reset();

                    rtvCount = i;
                    didChangeAnyViews = true;
                }
                else if (_rtvDescriptors[i].Ptr > 0)
                {
                    rtvCount = i;
                }
            }

            if (didChangeAnyViews)
            {
                _commandList.OMSetRenderTargets(
                    new ReadOnlySpan<CpuDescriptorHandle>(_rtvDescriptors, 0, rtvCount + 1),
                    _dsv.Value.HasValue ? _dsv.Value.Value.GetCPUHandle() : _device.CPUDescriptors_DSV.NullDescriptor);
            }

            Span<DirtyHandle<Vector4>> viewports = _viewports.AsSpan();
            Span<DirtyHandle<Vector4>> scissors = _scissors.AsSpan();

            bool viewportChanged = false;
            bool scissorChanged = false;

            int viewportCount = 0;
            int scissorCount = 0;

            for (int i = 0; i < 8; i++)
            {
                ref DirtyHandle<Vector4> viewport = ref viewports[i];
                ref DirtyHandle<Vector4> scissor = ref scissors[i];

                if (viewport.Dirty)
                {
                    Vector4 v4 = viewport.Value.GetValueOrDefault();
                    _viewportsStatic[i] = new Viewport(v4.X, v4.Y, v4.Z, v4.W);

                    viewport.Reset();

                    viewportChanged = true;
                    viewportCount = i + 1;
                }

                if (scissor.Dirty)
                {
                    Vector4 v4 = scissor.Value.GetValueOrDefault();
                    _scissorsStatic[i] = new RawRect((int)v4.X, (int)v4.Y, (int)v4.Z, (int)v4.W);

                    scissor.Reset();

                    scissorChanged = true;
                    scissorCount = i + 1;
                }
            }

            if (viewportChanged)
                _commandList.RSSetViewports((uint)viewportCount, _viewportsStatic);
            if (scissorChanged)
                _commandList.RSSetScissorRects((uint)scissorCount, _scissorsStatic);

            _rtv_dsv_modified = false;
        }

        private CpuDescriptorHandle[] _rtvDescriptors = new CpuDescriptorHandle[8];
        private Viewport[] _viewportsStatic = new Viewport[8];
        private RawRect[] _scissorsStatic = new RawRect[8];

        private void BindVertexBuffersAndIndexBuffer()
        {
            Span<DirtyHandle<VertexBufferViewData>> vertexBuffers = new Span<DirtyHandle<VertexBufferViewData>>(_vertexBuffer);
            Span<VertexBufferView> vertexBufferViews = new Span<VertexBufferView>(_vertexBufferViews);

            bool didChangeAnyBuffers = false;
            int vertexBufferCount = 0;
            for (int i = 0; i < _vertexBuffer.Length; i++)
            {
                ref DirtyHandle<VertexBufferViewData> vertexBuffer = ref vertexBuffers[i];
                if (vertexBuffer.Dirty)
                {
                    ref VertexBufferView vbv = ref vertexBufferViews[i];
                    if (vertexBuffer.Value.HasValue)
                    {
                        if (vertexBuffer.Value.Value.BufferView.Buffer.Desc.MemoryUsage.HasFlag(GfxMemoryUsage.Staging))
                        {
                            throw new InvalidDataException();
                        }
                        else
                        {
                            vbv.BufferLocation = vertexBuffer.Value.Value.BufferView.Buffer.GPUVirtualAddress;
                            vbv.SizeInBytes = (uint)vertexBuffer.Value.Value.BufferView.Buffer.Desc.Size;
                            vbv.StrideInBytes = vertexBuffer.Value.Value.Stride;

                            vertexBuffer.Value.Value.BufferView.Buffer.TransitionIfRequired(_commandList, ResourceStates.VertexAndConstantBuffer);
                        }
                    }
                    else
                    {
                        vbv.BufferLocation = 0;
                        vbv.SizeInBytes = 0;
                        vbv.StrideInBytes = 0;
                    }

                    vertexBuffer.Reset();

                    didChangeAnyBuffers = true;
                    vertexBufferCount = i;
                }
                //else if (_vertexBufferViews[i].BufferLocation > 0)
                //{
                //    vertexBufferCount = i;
                //}
            }

            if (didChangeAnyBuffers)
            {
                _commandList.IASetVertexBuffers(0, vertexBufferViews.Slice(0, vertexBufferCount + 1));
            }

            if (_indexBuffer.Dirty)
            {
                if (_indexBuffer.Value.HasValue)
                {
                    if (_indexBuffer.Value.Value.BufferView.Buffer.Desc.MemoryUsage.HasFlag(GfxMemoryUsage.Staging))
                    {
                        throw new InvalidDataException();
                    }
                    else
                    {
                        _indexBufferView.BufferLocation = _indexBuffer.Value.Value.BufferView.Buffer.GPUVirtualAddress;
                        _indexBufferView.SizeInBytes = (uint)_indexBuffer.Value.Value.BufferView.Buffer.Desc.Size;
                        _indexBufferView.Format = _indexBuffer.Value.Value.Stride;

                        _indexBuffer.Value.Value.BufferView.Buffer.TransitionIfRequired(_commandList, ResourceStates.IndexBuffer);
                        _commandList.IASetIndexBuffer(_indexBufferView);
                    }
                }
                else
                {
                    _commandList.IASetIndexBuffer((IndexBufferView?)null);
                }

                _indexBuffer.Reset();
            }

            _input_layout_modified = false;
        }

        private VertexBufferView[] _vertexBufferViews = new VertexBufferView[16];
        private IndexBufferView _indexBufferView = new IndexBufferView();

        private unsafe void BindShaderResources()
        {
            //int requiredDescriptors = CountRequiredDescriptors();

            GfxGraphicsPipeline pipeline = _pipelineState.Value ?? throw new NullReferenceException();
            if (_pipeline_modified || _pipelineState.Dirty)
            {
                _commandList.SetPipelineState(pipeline.D3D12PipelineState);
                _commandList.SetGraphicsRootSignature(pipeline.D3D12RootSignature);

                _pipelineState.Reset();
            }

            if (_topology_modified || _primitiveTopology.Dirty)
            {
                _commandList.IASetPrimitiveTopology((PrimitiveTopology)_primitiveTopology.Value.GetValueOrDefault());
                _topology_modified = false;

                _primitiveTopology.Reset();
            }

            if (_pipeline_modified || _shader_resources_modified)
            {
                Span<DirtyHandleClass<BindablePipelineResource>> srv = _srv.AsSpan();
                Span<DirtyHandleClass<BindablePipelineResource>> uav = _uav.AsSpan();
                Span<DirtyHandleClass<BindablePipelineResource>> cbv = _cbv.AsSpan();
                Span<DirtyHandleSpan<uint>> constants = _constants.AsSpan();

                ReadOnlySpan<GfxGraphicsPipeline.BindlessParameter> parameters = pipeline.BindlessParameters;

                for (int i = 0; i < parameters.Length; i++)
                {
                    GfxGraphicsPipeline.BindlessParameter param = parameters[i];
                    switch (param.Type)
                    {
                        case GfxGraphicsPipeline.BindlessParameter.DescriptorType.SRV:
                            {
                                ref DirtyHandleClass<BindablePipelineResource> data = ref _srv[param.Slot];
                                if (data.Dirty)
                                {
                                    if (data.Value != null)
                                    {
                                        if (data.Value.HasIndiceAtIndex(_id))
                                        {
                                            _allocationIndices[param.Offset] = data.Value.GetIndiceAtIndex(_id);
                                        }
                                        else
                                        {
                                            _unresolvedResources.Enqueue(new KeyValuePair<ushort, BindablePipelineResource>(param.Offset, data.Value));
                                        }
                                    }
                                    else if (_hasValidation)
                                    {
                                        GfxDevice.Logger?.Warning("SRV not bound on slot: #{a}!", param.Slot);
                                    }

                                    data.Reset();
                                }
                                else if (_hasValidation && data.Value == null)
                                {
                                    GfxDevice.Logger?.Warning("SRV not bound on slot: #{a}!", param.Slot);
                                }

                                break;
                            }
                        case GfxGraphicsPipeline.BindlessParameter.DescriptorType.CBV:
                            {
                                ref DirtyHandleClass<BindablePipelineResource> data = ref _cbv[param.Slot];
                                if (data.Dirty)
                                {
                                    if (data.Value != null)
                                    {
                                        ((GfxBufferView)data.Value).Buffer.TransitionIfRequired(_commandList, ResourceStates.VertexAndConstantBuffer);
                                        _commandList.SetGraphicsRootConstantBufferView(param.Offset, ((GfxBufferView)data.Value).Buffer.GPUVirtualAddress);
                                    }
                                    else if (_hasValidation)
                                    {
                                        GfxDevice.Logger?.Warning("CBV not bound on slot: #{a}!", param.Slot);
                                    }

                                    data.Reset();
                                }
                                else if (_hasValidation && data.Value == null)
                                {
                                    GfxDevice.Logger?.Warning("CBV not bound on slot: #{a}!", param.Slot);
                                }

                                break;
                            }
                        case GfxGraphicsPipeline.BindlessParameter.DescriptorType.UAV:
                            {
                                ref DirtyHandleClass<BindablePipelineResource> data = ref _uav[param.Slot];
                                if (data.Dirty)
                                {
                                    if (data.Value != null)
                                    {
                                        if (data.Value.HasIndiceAtIndex(_id))
                                        {
                                            _allocationIndices[param.Offset] = data.Value.GetIndiceAtIndex(_id);
                                        }
                                        else
                                        {
                                            _unresolvedResources.Enqueue(new KeyValuePair<ushort, BindablePipelineResource>(param.Offset, data.Value));
                                        }
                                    }
                                    else if (_hasValidation)
                                    {
                                        GfxDevice.Logger?.Warning("UAV not bound on slot: #{a}!", param.Slot);
                                    }

                                    data.Reset();
                                }
                                else if (_hasValidation && data.Value == null)
                                {
                                    GfxDevice.Logger?.Warning("UAV not bound on slot: #{a}!", param.Slot);
                                }

                                break;
                            }
                        case GfxGraphicsPipeline.BindlessParameter.DescriptorType.Constants:
                            {
                                ref DirtyHandleSpan<uint> data = ref constants[param.Slot];
                                if (data.Dirty)
                                {
                                    if (data.Value != null)
                                    {
                                        _commandList.SetGraphicsRoot32BitConstants(param.Offset, param.Size, data.Value, 0);
                                    }
                                    else
                                    {
                                        GfxDevice.Logger?.Warning("Constants not bound on slot: #{a}!", param.Slot);
                                    }

                                    data.Reset();
                                }
                                else if (_hasValidation && data.Value == null)
                                {
                                    GfxDevice.Logger?.Warning("Constants not bound on slot: #{a}!", param.Slot);
                                }

                                break;
                            }
                        default: break;
                    }
                }
            }

            if (_unresolvedResources.Count > 0)
            {
                DescriptorHeapAllocation dynamicHeap = _descriptorSuballocator.Allocate((uint)_unresolvedResources.Count);
                CpuDescriptorHandle cpu = dynamicHeap.GetCPUHandle();

                uint offset = (uint)dynamicHeap.Offset;
                while (_unresolvedResources.TryDequeue(out var kvp))
                {
                    BindablePipelineResource resource = kvp.Value;
                    _device.D3D12Device.CopyDescriptorsSimple(1, cpu, resource.GetHeapAllocation().GetCPUHandle(), DescriptorHeapType.ConstantBufferViewShaderResourceViewUnorderedAccessView);
                    _allocationIndices[kvp.Key] = offset++;
                    resource.SetIndiceAtIndex(_id, _allocationIndices[kvp.Key]);
                    cpu.Offset(dynamicHeap.DescriptorSize);
                }

                _cachedAlloc?.Free();
                _cachedAlloc = dynamicHeap;
            }

            fixed (uint* ptr = _allocationIndices)
            {
                _commandList.SetGraphicsRoot32BitConstants(0, pipeline.BindlessDescriptorValueCount, ptr, 0);
            }

            /*_cachedAlloc?.Free();
            _cachedAlloc = _descriptorSuballocator.Allocate((uint)ranges.Length);

            for (int i = 0; i < ranges.Length; i++)
            {
                GfxGraphicsPipeline.DescriptorRange range = ranges[i];
                switch (range.Type)
                {
                    case DescriptorRangeType.ShaderResourceView:
                        {
                            _commandList.SetGraphicsRootConstantBufferView
                            break;
                        }
                    case DescriptorRangeType.ConstantBufferView:
                        break;
                    default: throw new ArgumentException();
                }
            }

            for (int i = 0; i < binds.Length; i++)
            {
                GfxGraphicsPipeline.DescriptorBinds bind = binds[i];
                switch (bind.Type)
                {
                    case GfxGraphicsPipeline.DescriptorBinds.BindType.Table:
                        {
                            _commandList.SetGraphicsRootDescriptorTable(bind.Index, _cachedAlloc.Value.GetGPUHandle(bind.Offset));
                            break;
                        }
                    default: throw new ArgumentException();
                }
            }*/

            _shader_resources_modified = false;
            _topology_modified = false;
            _pipeline_modified = false;
        }

        /*private int CountRequiredDescriptors()
        {
            int count = 0;

            for (int i = 0; i < _cbv.Length; i++)
                if (_cbv[i].Value.HasValue)
                    count++;

            for (int i = 0; i < _srv.Length; i++)
                if (_srv[i].Value.HasValue)
                    count++;

            return count;
        }*/

        private DescriptorHeapAllocation? _cachedAlloc = null;
        private Queue<KeyValuePair<ushort, BindablePipelineResource>> _unresolvedResources = new Queue<KeyValuePair<ushort, BindablePipelineResource>>();
        private uint[] _allocationIndices = new uint[64/*dunno maybe its a good number?*/];

        public ID3D12GraphicsCommandList10 D3D12GraphicsCommandList => _commandList;
        public ushort Id => _id;

        private struct DirtyHandle<T>
            where T : struct
        {
            private T? _value;
            private bool _dirty;

            public T? Value { get => _value; set { _value = value; _dirty = true; } }
            public bool Dirty => _dirty;

            public DirtyHandle()
            {
                _value = null;
                _dirty = false;
            }

            public void Reset()
            {
                _dirty = false;
            }
        }

        private struct DirtyHandleClass<T>
          where T : class
        {
            private T? _value;
            private bool _dirty;

            public T? Value { get => _value; set { if (_value != value) { _value = value; _dirty = true; } } }
            public bool Dirty => _dirty;
            
            public DirtyHandleClass()
            {
                _value = null;
                _dirty = false;
            }

            public void Reset()
            {
                _dirty = false;
            }
        }

        private unsafe struct DirtyHandleSpan<T>
            where T : unmanaged
        {
            private T* _value;
            private bool _dirty;

            public T* Value { get => _value; set { _value = value; _dirty = true; } }
            public bool Dirty => _dirty;

            public DirtyHandleSpan()
            {
                _value = null;
                _dirty = false;
            }

            public void Reset()
            {
                _dirty = false;
            }

            public Span<T> Span => new Span<T>(_value, 128);
        }

        private struct VertexBufferViewData
        {
            public GfxBufferView BufferView;
            public uint Stride;
        }

        private struct IndexBufferViewData
        {
            public GfxBufferView BufferView;
            public Format Stride;
        }

        private static unsafe class PIXNATIVE
        {
            [DllImport("WinPixEventRuntime.dll", EntryPoint = "PIXBeginEventOnCommandList")]
            public static extern void PIXBeginEventOnCommandList(nint commandList, ulong color, [MarshalAs(UnmanagedType.LPStr)] string formatString);

            [DllImport("WinPixEventRuntime.dll", EntryPoint = "PIXEndEventOnCommandList")]
            public static extern void PIXEndEventOnCommandList(nint commandList);

            [DllImport("WinPixEventRuntime.dll", EntryPoint = "PIXSetMarkerOnCommandList")]
            public static extern void PIXSetMarkerOnCommandList(nint commandList, ulong color, [MarshalAs(UnmanagedType.LPStr)] string formatString);
        }
    }
}
