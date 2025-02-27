using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Vortice.Mathematics;

namespace SimpleLib.Resources.Data
{
    public class Mesh
    {
        public readonly List<RenderMesh> LODs = new List<RenderMesh>();

        public readonly Model ParentModel;
        public readonly string Name;

        public BoundingBox Bounds { get; private set; }

        public Mesh(Model parent, string name)
        {
            ParentModel = parent;
            Name = name;
            Bounds = BoundingBox.Zero;
        }

        internal void AddMeshForLOD(byte lod, RenderMesh mesh)
        {
            if (lod + 1 >= LODs.Count)
            {
                for (int i = LODs.Count; i <= lod + 1; i++)
                    LODs.Add(new RenderMesh());
            }

            LODs[lod] = mesh;

            Bounds = BoundingBox.CreateMerged(Bounds, mesh.Bounds);
        }

        internal void CalcBoundsForLOD(Span<Vertex> vertices)
        {
            Bounds = BoundingBox.Zero;

            for (int i = 0; i < LODs.Count; i++)
            {
                RenderMesh mesh = LODs[i];
                BoundingBox bb = BoundingBox.Zero;

                for (uint j = mesh.VertexOffset; j < mesh.VertexOffset + mesh.VertexCount; j++)
                {
                    bb.Min = Vector3.Min(bb.Min, vertices[(int)j].Position);
                    bb.Min = Vector3.Max(bb.Max, vertices[(int)j].Position);
                }

                mesh.Bounds = bb;
                LODs[i] = mesh;

                Bounds = BoundingBox.CreateMerged(Bounds, bb);
            }
        }

        internal void CalcBoundsForLOD(Span<VertexHalf> vertices)
        {
            Bounds = BoundingBox.Zero;

            for (int i = 0; i < LODs.Count; i++)
            {
                RenderMesh mesh = LODs[i];
                BoundingBox bb = BoundingBox.Zero;

                for (uint j = mesh.VertexOffset; j < mesh.VertexOffset + mesh.VertexCount; j++)
                {
                    bb.Min = Vector3.Min(bb.Min, vertices[(int)j].Position.ToVector3());
                    bb.Min = Vector3.Max(bb.Max, vertices[(int)j].Position.ToVector3());
                }

                mesh.Bounds = bb;
                LODs[i] = mesh;

                Bounds = BoundingBox.CreateMerged(Bounds, bb);
            }
        }

        public RenderMesh GetMeshForLOD(byte lod)
        {
            return LODs[lod];
        }

        public struct RenderMesh
        {
            public uint VertexCount;
            public uint IndexCount;

            public uint VertexOffset;
            public uint IndexOffset;

            public BoundingBox Bounds;
        }
    }

    public struct Vertex
    {
        public Vector3 Position;
        public Vector2 UV;
        public Vector3 Normal;

        public Vector3 Tangent;
    }

    public struct VertexHalf
    {
        public UShort3 Position;
        public UShort3 UV;
        public UShort3 Normal;

        public UShort3 Tangent;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct UShort2
    {
        public ushort X;
        public ushort Y;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public UShort2(ushort x = 0, ushort y = 0)
        {
            X = x;
            Y = y;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public unsafe UShort2(float x = 0.0f, float y = 0.0f)
        {
            X = *(ushort*)&x;
            Y = *(ushort*)&y;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public unsafe Vector2 ToVector2()
        {
            fixed (ushort* _x = &X)
            {
                fixed (ushort* _y = &Y)
                {
                    return new Vector2(*(float*)_x, *(float*)_y);
                }
            }
        }
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct UShort3
    {
        public ushort X;
        public ushort Y;
        public ushort Z;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public UShort3(ushort x = 0, ushort y = 0, ushort z = 0)
        {
            X = x;
            Y = y;
            Z = z;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public unsafe UShort3(float x = 0.0f, float y = 0.0f, float z = 0.0f)
        {
            X = *(ushort*)&x;
            Y = *(ushort*)&y;
            Z = *(ushort*)&z;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public unsafe Vector3 ToVector3()
        {
            fixed (ushort* _x = &X)
            {
                fixed (ushort* _y = &Y)
                {
                    fixed (ushort* _z = &Z)
                    {
                        return new Vector3(*(float*)_x, *(float*)_y, *(float*)_z);
                    }
                }
            }
        }
    }
}
