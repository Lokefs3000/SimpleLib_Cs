using SimpleLib.Components;
using SimpleLib.Files;
using SimpleLib.Render.Data.Structures;
using SimpleLib.Resources.Data;
using SimpleLib.Timing;
using SimpleLib.Utility;
using SimpleRHI;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace SimpleLib.Render.Components
{
    public class RenderBuilder : IDisposable
    {
        public List<RenderPoints> Viewpoints = new List<RenderPoints>();

        public List<RenderBatch> Batches = new List<RenderBatch>();
        public List<RenderFlag> Flags = new List<RenderFlag>();

        public UnsafeList<Matrix4x4> Transforms = new UnsafeList<Matrix4x4>(64);
        public UnsafeList<PerModelData> PerModel = new UnsafeList<PerModelData>(64);

        public uint LargestBatch = 0u;

        public void Dispose()
        {
            Transforms.Dispose();
            PerModel.Dispose();

            GC.SuppressFinalize(this);
        }

        //Use object pools?
        public void Compile()
        {
            DebugTimers.StartTimer("RenderBuilder.Compile");

            //Flags.Sort(MaterialComparer.Comparer);

            ulong last = FileRegistry.Invalid;
            int lastIndex = 0;

            for (int i = 1; i < Flags.Count; i++)
            {
                ulong curr = Flags[i].Material.Id;
                if (curr != last || true/*hack to avoid batching for debugging*/)
                {
                    RenderBatch batch = new RenderBatch();
                    batch.Material = Flags[i].Material;
                    batch.First = lastIndex;
                    batch.Last = i;

                    Batches.Add(batch);

                    //Flags.Sort(batch.First, batch.Last - batch.First, MeshComparer.Comparer);

                    last = curr;
                    lastIndex = i;

                    LargestBatch = (uint)Math.Max(LargestBatch, batch.Last - batch.First);
                }
            }

            if (Batches.Count > 0)
            {
                RenderBatch batch = Batches[Batches.Count - 1];
                batch.Last = Flags.Count;
                Batches[Batches.Count - 1] = batch;
                LargestBatch = (uint)Math.Max(LargestBatch, batch.Last - batch.First);
            }

            PerModel.Ensure((uint)Flags.Count);
            for (int i = 0; i < Flags.Count; i++)
            {
                PerModel.AddNoResize(new PerModelData
                {
                    TransformIndex = (uint)Flags[i].TransformIndex,
                });
            }

            DebugTimers.StopTimer();
        }

        public void Reset()
        {
            Viewpoints.Clear();
            Batches.Clear();
            Flags.Clear();
            Transforms.Clear();
            PerModel.Clear();
            LargestBatch = 0;
        }

        private class MaterialComparer : IComparer<RenderFlag>
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public int Compare(RenderFlag x, RenderFlag y)
            {
                return (int)(x.Material.Id - y.Material.Id);
            }

            public static MaterialComparer Comparer = new MaterialComparer();
        }

        private class MeshComparer : IComparer<RenderFlag>
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public int Compare(RenderFlag x, RenderFlag y)
            {
                return (int)(x.MeshObject.OwningModel.Id - y.MeshObject.OwningModel.Id);
            }

            public static MeshComparer Comparer = new MeshComparer();
        }

        public struct RenderFlag
        {
            public Mesh MeshObject;
            public Material Material;

            public int TransformIndex;
        }

        public struct RenderBatch
        {
            public Material Material;

            public int First;
            public int Last;
        }

        public struct RenderPoints
        {
            public Transform Transform;
            public Camera Viewpoint;
            public IGfxTextureView RenderTarget;
        }
    }
}
