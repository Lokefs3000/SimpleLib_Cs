using SimpleLib.Render.Components;
using SimpleLib.Render.Data;
using SimpleLib.Resources.Data;
using SimpleRHI;
using System.Numerics;
using System.Runtime.InteropServices;
using TerraFX.Interop.Windows;

namespace SimpleLib.Render.Passes
{
    public class OpaqueRenderPass : IRenderPass
    {
        private IGfxBuffer? _instancedTransformBuffer = null;
        private int _instancedTransformBufferLength = 0;

        private IGfxBuffer? _constantPerModelBuffer = null;
        private IGfxBuffer? _structuredPerModelBuffer = null;
        private int _structuredPerModelBufferLength = 0;

        private IGfxDevice _device;

        public OpaqueRenderPass(RenderEngine engine)
        {
            _device = engine.DeviceManager.RenderDevice;
        }

        public void Dispose()
        {

        }

        public void Pass(RenderEngine engine, RenderPassData data)
        {
            return;

            IGfxSwapChain? swapChain = engine.SwapChainHandler.GetSwapChain(engine.SwapChainHandler.PrimaryWindowId);
            if (swapChain != null)
            {
                IGfxGraphicsCommandBuffer context = engine.CommandBufferPool.GetContext();

                UploadTransformBuffer(context, engine.RenderBuilder);
                EnsureRenderingObjects();

                for (int i = 0; i < engine.RenderBuilder.Batches.Count; i++)
                {
                    RenderBuilder.RenderBatch batch = engine.RenderBuilder.Batches[i];
                    if (batch.Last - batch.First > 1)
                    {
                        DrawBatchInstanced(context, engine.RenderBuilder, ref batch);
                    }
                    else
                    {
                        DrawBatchConstant(context, engine.RenderBuilder, ref batch);
                    }
                }

                engine.CommandBufferPool.ReturnContext(context);
            }
        }

        private void DrawBatchInstanced(IGfxGraphicsCommandBuffer context, RenderBuilder builder, ref RenderBuilder.RenderBatch batch)
        {

        }

        private void DrawBatchConstant(IGfxGraphicsCommandBuffer context, RenderBuilder builder, ref RenderBuilder.RenderBatch batch)
        {
            if (_constantPerModelBuffer == null)
            {
                return;
            }

            RenderBuilder.RenderFlag flag = builder.Flags[batch.First];
            RenderBuilder.PerModelData data = builder.PerModel[batch.First];

            if (flag.Material.Shader == null)
            {
                return;
            }

            unsafe
            {
                nint mapped = context.Map(_constantPerModelBuffer, GfxMapType.Write, GfxMapFlags.Discard);
                NativeMemory.Copy(mapped.ToPointer(), &data, 4u);
                context.Unmap(_constantPerModelBuffer);
            }

            Model model = flag.MeshObject.ParentModel;
            Mesh.RenderMesh renderMesh = flag.MeshObject.GetMeshForLOD(0);

            Material material = flag.Material;
            material.Commit();

            material.DisableKeyword(KeywordInstancing);

            context.SetVertexBuffer(model.VertexBufferView, 0, 44);
            context.SetIndexBuffer(model.IndexBufferView);
            context.SetPipelineState(material.Pipeline);

            context.DrawIndexed(new IGfxGraphicsCommandBuffer.DrawIndexedAttribs
            {
                BaseVertex = (int)renderMesh.VertexOffset,
                IndexOffset = renderMesh.IndexOffset,
                IndexType = model.IndexBufferType,
                IndexCount = renderMesh.IndexCount,
            });
        }

        private void EnsureRenderingObjects()
        {
            if (_constantPerModelBuffer == null)
            {
                IGfxBuffer.CreateInfo desc = new IGfxBuffer.CreateInfo();
                desc.Name = "ConstantPerModelBuffer";
                desc.Size = 4ul;
                desc.Bind = GfxBindFlags.ConstantBuffer;
                desc.MemoryUsage = GfxMemoryUsage.Dynamic;
                desc.CpuAccess = GfxCPUAccessFlags.Write;

                _constantPerModelBuffer = _device.CreateBuffer(desc);
            }
        }

        private void UploadTransformBuffer(IGfxGraphicsCommandBuffer context, RenderBuilder builder)
        {
            if (_instancedTransformBufferLength < builder.Transforms.Count)
            {
                _instancedTransformBufferLength = builder.Transforms.Count;
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
            }

            nint mapped = context.Map(_instancedTransformBuffer, GfxMapType.Write, GfxMapFlags.Discard);
            Span<Matrix4x4> list = builder.Transforms.AsSpan();
            unsafe
            {
                fixed (Matrix4x4* frm = list)
                {
                    NativeMemory.Copy(frm, mapped.ToPointer(), (nuint)(64ul * (ulong)builder.Transforms.Count));
                }
            }
            context.Unmap(_instancedTransformBuffer);
        }

        private static readonly int KeywordInstancing = "INSTANCING".GetHashCode();
    }
}
