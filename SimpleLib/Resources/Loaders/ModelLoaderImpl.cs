using Schedulers;
using SharpGen.Runtime;
using SimpleLib.Files;
using SimpleLib.Resources.Data;
using SimpleLib.Utility;
using SimpleRHI;
using System.Buffers;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace SimpleLib.Resources.Loaders
{
    internal class ModelLoaderImpl : IJob
    {
        public static readonly Queue<Payload> Payloads = new Queue<Payload>();
        public static readonly ModelLoaderImpl Impl = new ModelLoaderImpl();

        private ModelLoaderImpl()
        {

        }

        public void Execute()
        {
            Payload args;
            lock (Payloads)
            {
                if (Payloads.Count == 0)
                {
                    LogTypes.Resources.Warning("Model load scheduled but no pending data!");
                    return;
                }

                args = Payloads.Dequeue();
            }

            ReadOnlyMemory<byte> data = args.Filesystem.ReadBytes(args.Id);
            CastingUtility.Cast(data, 0u, out MeshHeader header);

            if (header.UsesLODs)
                LoadWithLOD(args, ref header, data);
            else
                LoadNoLOD(args, ref header, data);
        }

        private unsafe void LoadNoLOD(Payload arg, ref MeshHeader header, ReadOnlyMemory<byte> buffer)
        {
            uint bufferOffset = (uint)Unsafe.SizeOf<MeshHeader>();

            for (long childStack = 1; childStack > 0; childStack--)
            {
                CastingUtility.Cast(buffer, bufferOffset, out NodeInstanceHeader node);
                bufferOffset += (uint)Unsafe.SizeOf<NodeInstanceHeader>();

                string nodeName;
                CastingUtility.ReadString(buffer, bufferOffset, node.NameLength, out nodeName);

                for (int i = 0; i < node.MeshCount; i++)
                {
                    string name = nodeName;
                    if (node.MeshCount > 1)
                        name += i.ToString();

                    Mesh.RenderMesh renderMesh = new Mesh.RenderMesh();
                    CastingUtility.Cast(buffer, bufferOffset, out renderMesh.VertexOffset); bufferOffset += sizeof(uint);
                    CastingUtility.Cast(buffer, bufferOffset, out renderMesh.IndexOffset); bufferOffset += sizeof(uint);
                    CastingUtility.Cast(buffer, bufferOffset, out renderMesh.VertexCount); bufferOffset += sizeof(uint);
                    CastingUtility.Cast(buffer, bufferOffset, out renderMesh.IndexCount, header.IndexStride); bufferOffset += header.IndexStride;

                    Mesh mesh = new Mesh(arg.Object, name);
                    mesh.AddMeshForLOD(0, renderMesh);

                    arg.Object.AddMesh(mesh);
                }

                childStack += node.ChildCount;
            }

            Span<byte> vertices;
            Span<byte> indices;

            using MemoryHandle handle = buffer.Pin();

            vertices = new Span<byte>(((byte*)handle.Pointer) + bufferOffset, (int)(header.VertexCount * header.VertexStride));
            bufferOffset += (uint)vertices.Length;

            indices = new Span<byte>(((byte*)handle.Pointer) + bufferOffset, (int)(header.IndexCount * header.IndexStride));
            bufferOffset += (uint)indices.Length;

            if (header.VertexStride == sizeof(VertexHalf))
            {
                Span<VertexHalf> half;
                fixed (byte* ptr00 = vertices)
                {
                    half = new Span<VertexHalf>(ptr00, (int)header.VertexCount);
                }

                foreach (KeyValuePair<int, Mesh> kvp in arg.Object.Meshes)
                    kvp.Value.CalcBoundsForLOD(half);
            }
            else
            {
                Span<Vertex> full;
                fixed (byte* ptr00 = vertices)
                {
                    full = new Span<Vertex>(ptr00, (int)header.VertexCount);
                }

                foreach (KeyValuePair<int, Mesh> kvp in arg.Object.Meshes)
                    kvp.Value.CalcBoundsForLOD(full);
            }

            arg.Object.RecalculateBounds();

            IGfxBuffer? vertexBuffer = null;
            {
                IGfxBuffer.CreateInfo desc = new IGfxBuffer.CreateInfo()
                {
                    Size = (ulong)vertices.Length * header.VertexStride,
                    Bind = GfxBindFlags.VertexBuffer,
                    MemoryUsage = GfxMemoryUsage.Immutable,
                    Data = (nint)vertices.GetPointerUnsafe()
                };

                vertexBuffer = arg.RenderDevice.CreateBuffer(desc);
                if (vertexBuffer == null)
                {
                    LogTypes.Resources.Error("Failed to create vertex buffer for model: \"{a}\"!", arg.Id);
                    return;
                }
            }

            IGfxBuffer? indexBuffer = null;
            {
                IGfxBuffer.CreateInfo desc = new IGfxBuffer.CreateInfo()
                {
                    Size = (ulong)indices.Length * header.IndexStride,
                    Bind = GfxBindFlags.IndexBuffer,
                    MemoryUsage = GfxMemoryUsage.Immutable,
                    Data = (nint)indices.GetPointerUnsafe()
                };

                indexBuffer = arg.RenderDevice.CreateBuffer(desc);
                if (indexBuffer == null)
                {
                    LogTypes.Resources.Error("Failed to create vertex buffer for model: \"{a}\"!", arg.Id);
                    vertexBuffer?.Dispose();
                    return;
                }
            }

            arg.Object.BindResources(vertexBuffer, indexBuffer, header.IndexStride == sizeof(ushort) ? GfxValueType.UInt1_Half : GfxValueType.UInt1);
        }

        private unsafe void LoadWithLOD(Payload arg, ref MeshHeader header, ReadOnlyMemory<byte> buffer)
        {
            uint bufferOffset = (uint)sizeof(MeshHeader);

            byte lodCount;
            CastingUtility.Cast(buffer, bufferOffset, out lodCount); bufferOffset++;

            Dictionary<string, Mesh> meshes = new Dictionary<string, Mesh>();
            for (byte lod = 0; lod < lodCount; lod++)
            {
                ushort childCount = 0;
                CastingUtility.Cast(buffer, bufferOffset, out childCount);
                childCount += sizeof(ushort);

                for (int i = 0; i < childCount; i++)
                {
                    NodeLODHeader node = new NodeLODHeader();
                    CastingUtility.Cast(buffer, bufferOffset, out node);
                    bufferOffset += (uint)sizeof(NodeLODHeader);

                    string nodeName = string.Empty;
                    CastingUtility.ReadString(buffer, bufferOffset, node.NameLength, out nodeName);
                    bufferOffset += node.NameLength;

                    Mesh.RenderMesh renderMesh = new Mesh.RenderMesh();
                    CastingUtility.Cast(buffer, bufferOffset, out renderMesh.VertexOffset); bufferOffset += sizeof(uint);
                    CastingUtility.Cast(buffer, bufferOffset, out renderMesh.IndexOffset); bufferOffset += sizeof(uint);
                    CastingUtility.Cast(buffer, bufferOffset, out renderMesh.VertexCount); bufferOffset += sizeof(uint);
                    CastingUtility.Cast(buffer, bufferOffset, out renderMesh.IndexCount, header.IndexStride); bufferOffset += header.IndexStride;

                    if (!meshes.ContainsKey(nodeName))
                    {
                        Mesh mesh = new Mesh(arg.Object, nodeName);
                        arg.Object.AddMesh(mesh);

                        meshes.Add(nodeName, mesh);
                    }

                    meshes[nodeName].AddMeshForLOD(lod, renderMesh);
                }
            }

            Span<byte> vertices;
            Span<byte> indices;

            using MemoryHandle handle = buffer.Pin();

            vertices = new Span<byte>(((byte*)handle.Pointer) + bufferOffset, (int)(header.VertexCount * header.VertexStride));
            bufferOffset += (uint)vertices.Length;

            indices = new Span<byte>(((byte*)handle.Pointer) + bufferOffset, (int)(header.IndexCount * header.IndexStride));
            bufferOffset += (uint)indices.Length;

            if (header.VertexStride == sizeof(VertexHalf))
            {
                Span<VertexHalf> half;
                fixed (byte* ptr00 = vertices)
                {
                    half = new Span<VertexHalf>(ptr00, (int)header.VertexCount);
                }

                foreach (KeyValuePair<int, Mesh> kvp in arg.Object.Meshes)
                    kvp.Value.CalcBoundsForLOD(half);
            }
            else
            {
                Span<Vertex> full;
                fixed (byte* ptr00 = vertices)
                {
                    full = new Span<Vertex>(ptr00, (int)header.VertexCount);
                }

                foreach (KeyValuePair<int, Mesh> kvp in arg.Object.Meshes)
                    kvp.Value.CalcBoundsForLOD(full);
            }

            arg.Object.RecalculateBounds();

            IGfxBuffer? vertexBuffer = null;
            {
                IGfxBuffer.CreateInfo desc = new IGfxBuffer.CreateInfo()
                {
                    Size = (ulong)vertices.Length * header.VertexStride,
                    Bind = GfxBindFlags.VertexBuffer,
                    MemoryUsage = GfxMemoryUsage.Immutable,
                    Data = (nint)vertices.GetPointerUnsafe()
                };

                vertexBuffer = arg.RenderDevice.CreateBuffer(desc);
                if (vertexBuffer == null)
                {
                    LogTypes.Resources.Error("Failed to create vertex buffer for model: \"{a}\"!", arg.Id);
                    return;
                }
            }

            IGfxBuffer? indexBuffer = null;
            {
                IGfxBuffer.CreateInfo desc = new IGfxBuffer.CreateInfo()
                {
                    Size = (ulong)indices.Length * header.IndexStride,
                    Bind = GfxBindFlags.IndexBuffer,
                    MemoryUsage = GfxMemoryUsage.Immutable,
                    Data = (nint)indices.GetPointerUnsafe()
                };

                indexBuffer = arg.RenderDevice.CreateBuffer(desc);
                if (indexBuffer == null)
                {
                    LogTypes.Resources.Error("Failed to create vertex buffer for model: \"{a}\"!", arg.Id);
                    vertexBuffer?.Dispose();
                    return;
                }
            }

            arg.Object.BindResources(vertexBuffer, indexBuffer, header.IndexStride == sizeof(ushort) ? GfxValueType.UInt1_Half : GfxValueType.UInt1);
        }

        public struct Payload
        {
            public IGfxDevice RenderDevice;
            public Model Object;
            public Filesystem Filesystem;
            public ulong Id;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        private struct MeshHeader
        {
            public uint Header;
            public ushort Version;

            public uint VertexCount;
            public byte VertexStride;
            public uint IndexCount;
            public byte IndexStride;

            public bool UsesLODs;

            public const uint HeaderReal = 0x4c444d53U;
            public const ushort VersionReal = 1;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        private struct NodeInstanceHeader
        {
            public byte NameLength;
            public byte MeshCount;
            public byte ChildCount;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        private struct NodeLODHeader
        {
            public byte NameLength;
        }
    }
}
