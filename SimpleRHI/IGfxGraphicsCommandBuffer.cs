using System.Numerics;
using Vortice.Mathematics;

namespace SimpleRHI
{
    public interface IGfxGraphicsCommandBuffer : IDisposable
    {
        public CreateInfo Desc { get; }

        public void Begin();
        public void Close();

        public void BeginDebugGroup(string name, in Color4 color);
        public void EndDebugGroup();
        public void SetDebugMarker(string name, in Color4 color);

        public void ClearRenderTarget(IGfxTextureView rtv, in Color4 rgba);
        public void ClearDepthStencil(IGfxTextureView depthStencil, float depth = 1.0f, byte stencil = 0xff);
        public void SetRenderTarget(IGfxTextureView? rtv, uint slot = 0, IGfxTextureView? depthStencil = null);
        public void SetRenderTargets(ReadOnlySpan<IGfxTextureView?> rtv, uint slot = 0, IGfxTextureView? depthStencil = null);

        public void SetViewport(Vector4 viewport, uint slot = 0);
        public void SetViewports(ReadOnlySpan<Vector4> viewports, uint slot = 0);
        public void SetScissor(Vector4 scissor, uint slot = 0);
        public void SetScissors(ReadOnlySpan<Vector4> scissors, uint slot = 0);

        public void SetVertexBuffer(IGfxBufferView? buffer, uint slot = 0, uint stride = 0, ulong offset = 0);
        public void SetVertexBuffers(ReadOnlySpan<IGfxBufferView?> buffers, uint startSlot, uint[] strides, ulong[] offsets);
        public void SetIndexBuffer(IGfxBufferView? buffer, ulong offset = 0);

        public void SetConstantBuffer(IGfxBufferView? buffer, uint slot = 0);
        public void SetConstantBuffers(ReadOnlySpan<IGfxBufferView?> buffer, uint slot = 0);
        public void Set32BitConstants(uint index, ReadOnlySpan<uint> src);
        public void Set32BitConstants(uint index, uint numBits, nint src);
        public void SetShaderResource(GfxShaderType stage, IGfxBufferView? resource, uint slot = 0);
        public void SetShaderResources(GfxShaderType stage, ReadOnlySpan<IGfxBufferView?> resources, uint slot = 0);
        public void SetShaderResource(GfxShaderType stage, IGfxTextureView? resource, uint slot = 0);
        public void SetShaderResources(GfxShaderType stage, ReadOnlySpan<IGfxTextureView?> resources, uint slot = 0);

        public void SetPipelineState(IGfxGraphicsPipeline? pipeline);
        public void SetPrimitiveToplogy(GfxPrimitiveTopology topology);

        public void DrawIndexed(in DrawIndexedAttribs attribs);
        public void DrawInstancedIndexed(in DrawIndexedInstancedAttribs attribs);

        public Span<T> Map<T>(IGfxBuffer buffer, GfxMapType type, GfxMapFlags flags) where T : unmanaged;
        public nint Map(IGfxBuffer buffer, GfxMapType type, GfxMapFlags flags);
        public void Unmap(IGfxBuffer buffer);

        public void CopyTextureResource(IGfxBuffer buffer, ulong offset, ulong size, uint pitch, IGfxTexture texture, uint subresource);

        public struct CreateInfo
        {
            public string Name;

            public GfxQueueType Type;

            public CreateInfo()
            {
                Name = string.Empty;

                Type = GfxQueueType.Graphics;
            }
        }

        public struct DrawIndexedAttribs
        {
            public int BaseVertex;
            public uint IndexOffset;

            public uint IndexCount;
            public GfxValueType IndexType;
        }

        public struct DrawIndexedInstancedAttribs
        {
            public int BaseVertex;
            public uint IndexOffset;
            public uint InstanceOffset;

            public uint InstanceCount;
            public uint IndexCount;
            public GfxValueType IndexType;
        }
    }
}
