﻿using Arch.Buffer;
using SimpleLib.Components;
using SimpleLib.Render.Components;
using SimpleLib.Render.Data;
using SimpleLib.Render.Data.Structures;
using SimpleLib.Resources.Data;
using SimpleLib.Systems;
using SimpleRHI;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using TerraFX.Interop.Windows;
using Vortice.Mathematics;

namespace SimpleLib.Render.Passes
{
    public class OpaqueRenderPass : IRenderPass
    {
        private IGfxBuffer? _instancedTransformBuffer = null;
        private IGfxBufferView? _instancedTransformBufferView = null;
        private int _instancedTransformBufferLength = 0;

        private IGfxBuffer? _constantPerModelBuffer = null;
        private IGfxBufferView? _constantPerModelBufferView = null;
        private IGfxBuffer? _structuredPerModelBuffer = null;
        private IGfxBufferView? _structuredPerModelBufferView = null;
        private int _structuredPerModelBufferLength = 0;

        private IGfxBuffer _cameraDataBuffer;
        private IGfxBufferView _cameraDataBufferView;

        private IGfxDevice _device;

        public OpaqueRenderPass(RenderEngine engine)
        {
            _device = engine.DeviceManager.RenderDevice;

            _cameraDataBuffer = _device.CreateBuffer(new IGfxBuffer.CreateInfo
            {
                Bind = GfxBindFlags.ConstantBuffer,
                CpuAccess = GfxCPUAccessFlags.Write,
                MemoryUsage = GfxMemoryUsage.Dynamic,
                Name = "OpaqueCameraBuffer",
                Size = (ulong)Unsafe.SizeOf<CameraBufferData>()
            });
            _cameraDataBufferView = _cameraDataBuffer.CreateView(new IGfxBufferView.CreateInfo { Stride = (byte)Unsafe.SizeOf<CameraBufferData>() });
        }

        public void Dispose()
        {
            _cameraDataBuffer.Dispose();
            _cameraDataBufferView.Dispose();

            _instancedTransformBufferView?.Dispose();
            _constantPerModelBufferView?.Dispose();
            _structuredPerModelBufferView?.Dispose();

            _instancedTransformBuffer?.Dispose();
            _constantPerModelBuffer?.Dispose();
            _structuredPerModelBuffer?.Dispose();
        }

        public void Pass(RenderEngine engine, RenderPassData data)
        {
            IGfxSwapChain? swapChain = engine.SwapChainHandler.GetSwapChain(engine.SwapChainHandler.PrimaryWindowId);
            if (swapChain != null)
            {
                IGfxGraphicsCommandBuffer context = engine.CommandBufferPool.GetContext();

                EnsureRenderingObjects(data, context);
                UploadTransformBuffer(context, engine.RenderBuilder);
                UploadMiscBuffers(data, context);

                context.SetConstantBuffer(_cameraDataBufferView, 6);
                context.SetShaderResource(GfxShaderType.Vertex, _instancedTransformBufferView, 7);
                context.SetConstantBuffer(_constantPerModelBufferView, 5);

                for (int i = 0; i < engine.RenderBuilder.Batches.Count; i++)
                {
                    RenderBuilder.RenderBatch batch = engine.RenderBuilder.Batches[i];
                    if (batch.Last - batch.First > 1)
                    {
                        DrawBatchInstanced(context, engine.RenderBuilder, ref batch);
                    }
                    else
                    {
                        DrawBatchConstant(context, engine.RenderBuilder, ref batch, i);
                    }
                }

                engine.CommandBufferPool.ReturnContext(context);
            }
        }

        private void DrawBatchInstanced(IGfxGraphicsCommandBuffer context, RenderBuilder builder, ref RenderBuilder.RenderBatch batch)
        {

        }

        private void DrawBatchConstant(IGfxGraphicsCommandBuffer context, RenderBuilder builder, ref RenderBuilder.RenderBatch batch, int i = 0)
        {
            if (_constantPerModelBuffer == null)
            {
                return;
            }

            RenderBuilder.RenderFlag flag = builder.Flags[batch.First];
            RenderBuilder.PerModelData data = builder.PerModel[(uint)batch.First];

            if (flag.Material.Shader == null)
            {
                return;
            }

            unsafe
            {
                Span<PerModelData> mapped = context.Map<PerModelData>(_constantPerModelBuffer, GfxMapType.Write, GfxMapFlags.Discard);
                mapped[0] = new PerModelData { TransformIndex = (uint)flag.TransformIndex };
                context.Unmap(_constantPerModelBuffer);
            }

            Model model = flag.MeshObject.OwningModel;
            Mesh.RenderMesh renderMesh = flag.MeshObject.GetMeshForLOD(0);

            Material material = flag.Material;
            material.Commit();

            material.DisableKeyword(KeywordInstancing);

            context.SetVertexBuffer(model.Data.VertexBufferView, 0, (uint)Unsafe.SizeOf<Vertex>());
            context.SetIndexBuffer(model.Data.IndexBufferView);
            context.SetPipelineState(material.Pipeline);
            context.SetPrimitiveToplogy(GfxPrimitiveTopology.TriangleList);

            context.DrawIndexed(new IGfxGraphicsCommandBuffer.DrawIndexedAttribs
            {
                BaseVertex = (int)renderMesh.VertexOffset,
                IndexOffset = renderMesh.IndexOffset,
                IndexType = model.Data.IndexStride == 2 ? GfxValueType.UInt1_Half : GfxValueType.UInt1,
                IndexCount = renderMesh.IndexCount,
            });
        }

        private void EnsureRenderingObjects(RenderPassData data, IGfxGraphicsCommandBuffer commandBuffer)
        {
            if (_constantPerModelBuffer == null)
            {
                IGfxBuffer.CreateInfo desc = new IGfxBuffer.CreateInfo();
                desc.Name = "ConstantPerModelBuffer";
                desc.Size = (ulong)Unsafe.SizeOf<PerModelData>();
                desc.Bind = GfxBindFlags.ConstantBuffer;
                desc.MemoryUsage = GfxMemoryUsage.Dynamic;
                desc.CpuAccess = GfxCPUAccessFlags.Write;

                _constantPerModelBuffer = _device.CreateBuffer(desc);
                _constantPerModelBufferView = _constantPerModelBuffer.CreateView(new IGfxBufferView.CreateInfo { Stride = (byte)desc.Size });
            }

            ViewportRenderData? viewport = data.Get<ViewportRenderData>();
            CameraRenderData? camera = data.Get<CameraRenderData>();

            if (viewport != null && camera != null)
            {
                commandBuffer.SetViewport(new Vector4(0.0f, 0.0f, viewport.RenderResolution.X, viewport.RenderResolution.Y));
                commandBuffer.SetScissor(new Vector4(0.0f, 0.0f, viewport.RenderResolution.X, viewport.RenderResolution.Y));
                //commandBuffer.ClearRenderTarget(viewport.BackbufferTextureView, new Color4(0.1f, 0.4f, 0.2f));
                commandBuffer.SetRenderTarget(viewport.BackbufferTextureView);
            }
        }

        private void UploadTransformBuffer(IGfxGraphicsCommandBuffer context, RenderBuilder builder)
        {
            if (_instancedTransformBufferLength < builder.Transforms.Count)
            {
                _instancedTransformBufferLength = (int)builder.Transforms.Count * 2;

                _instancedTransformBufferView?.Dispose();
                _instancedTransformBuffer?.Dispose();

                IGfxBuffer.CreateInfo desc = new IGfxBuffer.CreateInfo();
                desc.Name = "TransformBuffer";
                desc.Size = 64UL * (ulong)_instancedTransformBufferLength;
                desc.Bind = GfxBindFlags.ShaderResource;
                desc.Mode = GfxBufferMode.Structured;
                desc.MemoryUsage = GfxMemoryUsage.Dynamic;
                desc.CpuAccess = GfxCPUAccessFlags.Write;
                desc.ElementByteStride = 64u;

                _instancedTransformBuffer = _device.CreateBuffer(desc);
                _instancedTransformBufferView = _instancedTransformBuffer?.CreateView(new IGfxBufferView.CreateInfo { Stride = (byte)desc.ElementByteStride });
            }

            Span<Matrix4x4> mapped = context.Map<Matrix4x4>(_instancedTransformBuffer, GfxMapType.Write, GfxMapFlags.Discard);
            Span<Matrix4x4> list = builder.Transforms.AsSpan();
            list.CopyTo(mapped);
            context.Unmap(_instancedTransformBuffer);
        }

        private void UploadMiscBuffers(RenderPassData data, IGfxGraphicsCommandBuffer commandBuffer)
        {
            ViewportRenderData? viewport = data.Get<ViewportRenderData>();
            CameraRenderData? camera = data.Get<CameraRenderData>();

            if (viewport != null && camera != null)
            {
                Matrix4x4 view = Matrix4x4.CreateLookAt(camera.RenderTransform.WorldPosition, camera.RenderTransform.WorldPosition + camera.RenderTransform.Forward, camera.RenderTransform.Up);
                Matrix4x4 proj = Matrix4x4.CreatePerspectiveFieldOfView(TransformSystem.DegToRad * camera.RenderCamera.FieldOfView, viewport.RenderResolution.X / viewport.RenderResolution.Y, camera.RenderCamera.NearClip, camera.RenderCamera.FarClip);
                CameraBufferData cameraData = new CameraBufferData()
                {
                    ViewPosition = camera.RenderTransform.WorldPosition,
                    ViewProjection = Matrix4x4.Multiply(view, proj)
                };

                Span<CameraBufferData> bufferData = commandBuffer.Map<CameraBufferData>(_cameraDataBuffer, GfxMapType.Write, GfxMapFlags.Discard);
                bufferData[0] = cameraData;
                commandBuffer.Unmap(_cameraDataBuffer);
            }
        }

        private static readonly int KeywordInstancing = "INSTANCING".GetHashCode();
    }
}
