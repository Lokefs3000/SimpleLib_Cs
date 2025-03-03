using SimpleLib.Files;
using SimpleLib.GUI.sIMGUI;
using SimpleLib.Render.Components;
using SimpleLib.Render.Data;
using SimpleLib.Resources;
using SimpleLib.Resources.Data;
using SimpleLib.Runtime;
using SimpleRHI;
using System.Numerics;
using Vortice.Mathematics;

namespace SimpleLib.Render.Passes
{
    public class sIMGUIRenderer : IRenderPass
    {
        private IGfxBuffer? _vertexBuffer;
        private IGfxBuffer? _indexBuffer;

        private IGfxBufferView? _vertexBufferView;
        private IGfxBufferView? _indexBufferView;

        private Material _material;

        private int _prevVertexBufferSize = 0;
        private int _prevIndexBufferSize = 0;

        public sIMGUIRenderer()
        {
            _material = ResourceHandler.LoadMaterial(AutoFileRegisterer.EngineMaterialsSIMGUIMaterial);
        }

        public void Dispose()
        {
            _vertexBufferView?.Dispose();
            _indexBufferView?.Dispose();

            _vertexBuffer?.Dispose();
            _indexBuffer?.Dispose();
        }

        public void Pass(RenderEngine engine, RenderPassData data)
        {
            DrawList drawList = sIMGUI.DrawList;
            if (drawList.Vertices.IsEmpty || drawList.Indices.IsEmpty)
                return;

            IGfxGraphicsCommandBuffer commandBuffer = engine.CommandBufferPool.GetContext();
            ViewportRenderData viewport = data.Get<ViewportRenderData>() ?? throw new NullReferenceException();

            UpdateAndPrepareBuffers(drawList, engine.DeviceManager.RenderDevice, commandBuffer);
            DrawBuffersToScreen(drawList, commandBuffer, viewport);

            engine.CommandBufferPool.ReturnContext(commandBuffer);
        }

        private unsafe void DrawBuffersToScreen(DrawList drawList, IGfxGraphicsCommandBuffer commandBuffer, ViewportRenderData viewport)
        {
            Span<sIMGUIDrawCmd> cmds = drawList.DrawCommands;

            Vector4 prevClipRect = Vector4.Zero;
            Texture? prevTexture = null;

            Matrix4x4 orthographic =
                Matrix4x4.CreateOrthographic(viewport.RenderResolution.X, viewport.RenderResolution.Y, -1.0f, 1.0f) *
                Matrix4x4.CreateTranslation(new Vector3(-1.0f, 1.0f, 0.0f));

            commandBuffer.SetViewport(new Vector4(0.0f, 0.0f, viewport.RenderResolution.X, viewport.RenderResolution.Y));
            //commandBuffer.ClearRenderTarget(viewport.BackbufferTextureView, new Color4(0.1f, 0.4f, 0.2f));
            commandBuffer.SetRenderTarget(viewport.BackbufferTextureView);

            commandBuffer.SetVertexBuffer(_vertexBufferView, stride: (uint)sizeof(sIMGUIVertex));
            commandBuffer.SetIndexBuffer(_indexBufferView);

            commandBuffer.Set32BitConstants(0, 16u, (nint)(&orthographic));

            commandBuffer.SetPipelineState(_material.Pipeline);
            commandBuffer.SetPrimitiveToplogy(GfxPrimitiveTopology.TriangleList);

            for (int i = 0; i < cmds.Length; i++)
            {
                ref sIMGUIDrawCmd cmd = ref cmds[i];
                if (cmd.IndexCount == 0)
                {
                    continue;
                }

                if (prevClipRect != cmd.Clip)
                {
                    prevClipRect = cmd.Clip ?? new Vector4(0.0f, 0.0f, viewport.RenderResolution.X, viewport.RenderResolution.Y);
                    commandBuffer.SetScissor(new Vector4(prevClipRect.X, prevClipRect.Y, prevClipRect.Z, prevClipRect.W));
                }

                if (prevTexture != cmd.Texture)
                {
                    prevTexture = cmd.Texture;
                    commandBuffer.SetShaderResource(GfxShaderType.Pixel, cmd.Texture.Data.View);
                }

                commandBuffer.DrawIndexed(new IGfxGraphicsCommandBuffer.DrawIndexedAttribs
                {
                    BaseVertex = cmd.VertexOffset,
                    IndexOffset = cmd.IndexOffset,
                    IndexCount = cmd.IndexCount,
                    IndexType = GfxValueType.UInt1_Half,
                });
            }
        }

        private unsafe void UpdateAndPrepareBuffers(DrawList drawList, IGfxDevice device, IGfxGraphicsCommandBuffer commandBuffer)
        {
            if (_vertexBuffer == null || _prevVertexBufferSize < drawList.Vertices.Length)
            {
                _vertexBuffer?.Dispose();
                _vertexBufferView?.Dispose();

                _vertexBuffer = device.CreateBuffer(new IGfxBuffer.CreateInfo
                {
                    Name = "sIMGUI-VertexBuffer",
                    Size = (ulong)(drawList.Vertices.Length + 400) * (ulong)sizeof(sIMGUIVertex),
                    Bind = GfxBindFlags.VertexBuffer,
                    MemoryUsage = GfxMemoryUsage.Dynamic,
                    CpuAccess = GfxCPUAccessFlags.Write
                });

                _vertexBufferView = _vertexBuffer.CreateView(new IGfxBufferView.CreateInfo { Stride = (byte)sizeof(sIMGUIVertex) });

                _prevVertexBufferSize = drawList.Vertices.Length;
            }

            if (_indexBuffer == null || _prevIndexBufferSize < drawList.Indices.Length)
            {
                _indexBuffer?.Dispose();
                _indexBufferView?.Dispose();

                _indexBuffer = device.CreateBuffer(new IGfxBuffer.CreateInfo
                {
                    Name = "sIMGUI-IndexBuffer",
                    Size = (ulong)(drawList.Indices.Length + 600) * (ulong)sizeof(ushort),
                    Bind = GfxBindFlags.IndexBuffer,
                    MemoryUsage = GfxMemoryUsage.Dynamic,
                    CpuAccess = GfxCPUAccessFlags.Write
                });

                _indexBufferView = _indexBuffer.CreateView(new IGfxBufferView.CreateInfo { Stride = (byte)sizeof(ushort) });

                _prevIndexBufferSize = drawList.Indices.Length;
            }

            {
                Span<sIMGUIVertex> mapped = commandBuffer.Map<sIMGUIVertex>(_vertexBuffer, GfxMapType.Write, GfxMapFlags.Discard);
                drawList.Vertices.CopyTo(mapped);
                commandBuffer.Unmap(_vertexBuffer);
            }

            {
                Span<ushort> mapped = commandBuffer.Map<ushort>(_indexBuffer, GfxMapType.Write, GfxMapFlags.Discard);
                drawList.Indices.CopyTo(mapped);
                commandBuffer.Unmap(_indexBuffer);
            }
        }
    }
}
