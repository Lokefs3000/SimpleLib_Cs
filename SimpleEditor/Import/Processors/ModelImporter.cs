using Arch.LowLevel;
using Schedulers;
using Silk.NET.Assimp;
using SimpleEditor.Runtime;
using SimpleLib.Utility;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Text;
using Tomlyn;
using Tomlyn.Model;

namespace SimpleEditor.Import.Processors
{
    public class ModelImporter : IJob
    {
        public static Queue<Arguments> Pending = new Queue<Arguments>();

        public unsafe void Execute()
        {
            Arguments args;
            lock (Pending)
            {
                if (Pending.Count == 0)
                {
                    LogTypes.Import.Warning("Image import scheduled but no arguments are available!");
                    return;
                }

                args = Pending.Dequeue();
            }

            TomlTable table = Toml.ToModel(args.EDR.ProjectFileSystem.ReadAssociate(args.Id));

            TomlTable general = (TomlTable)table["General"];

            using Assimp Assimp = Assimp.GetApi();

            Span<byte> model = args.EDR.ProjectFileSystem.ReadRealBytes(args.Id);

            Scene* scene;
            fixed (byte* ptr = model)
            {
                scene = Assimp.ImportFileFromMemory(ptr, (uint)model.Length, (uint)(PostProcessPreset.TargetRealTimeMaximumQuality | PostProcessSteps.FlipUVs | PostProcessSteps.CalculateTangentSpace), args.Hint);
            }

            if (scene == null)
            {
                LogTypes.Import.Error("Failed to load model: \"{a}\", because: \"{b}\"!", args.Id, Assimp.GetErrorStringS());
                return;
            }

            Node* rootNode = scene->MRootNode;

            MeshHeader header = new MeshHeader();
            header.Header = MeshHeader.HeaderReal;
            header.Version = MeshHeader.VersionReal;
            header.UsesLODs = (bool)general["EnableLODs"] && CheckForLODSupport(rootNode);
            header.VertexStride = (byte)((bool)general["HalfPrecision"] ? sizeof(VertexHalf) : sizeof(Vertex));
            CalcModelMetrics(scene, ref header.VertexCount, ref header.IndexCount, ref header.IndexStride);

            Stream stream;
            try
            {
                stream = System.IO.File.Open(args.Output, FileMode.Create);
            }
            catch (Exception ex)
            {
                LogTypes.Import.Error(ex, "Failed to open output stream: \"{a}\"!", args.Output);
                Assimp.FreeScene(scene);
                return;
            }

            StreamUtility.Serialize(stream, ref header);

            using UnsafeArray<byte> vertices = new UnsafeArray<byte>((int)(header.VertexCount * (int)header.VertexStride));
            using UnsafeArray<byte> indices = new UnsafeArray<byte>((int)(header.IndexCount * (int)header.IndexStride));

            ulong vertexOffset = 0;
            ulong indexOffset = 0;

            bool result = false;
            if (header.UsesLODs)
                result = WriteNodeLOD(scene, rootNode, stream, ref vertexOffset, ref indexOffset, header.IndexStride, header.VertexStride, vertices, indices);
            else
            {
                for (int i = 0; i < rootNode->MNumChildren; i++)
                {
                    result = WriteNode(scene, rootNode->MChildren[i], stream, ref vertexOffset, ref indexOffset, header.IndexStride, header.VertexStride, vertices, indices);
                }
            }

            fixed (byte* ptr00 = &vertices[0])
            {
                stream.Write(new ReadOnlySpan<byte>(ptr00, vertices.Count));
            }

            fixed (byte* ptr00 = &indices[0])
            {
                stream.Write(new ReadOnlySpan<byte>(ptr00, indices.Count));
            }

            stream.Flush();
            stream.Dispose();

            Assimp.FreeScene(scene);

            if (!result)
            {
                System.IO.File.Delete(args.Output);
                LogTypes.Import.Error("Failed to import mesh: \"{a}\"!", args.Id);
            }
        }

        private unsafe bool CheckForLODSupport(Node* root)
        {
            byte found = 0;
            for (int i = 0; i < root->MNumChildren; i++)
            {
                Node* node = root->MChildren[i];
                string name = node->MName.AsString;

                if (name.Length > 3 && name.StartsWith("LOD") && byte.TryParse(name.Substring(3), out byte v))
                {
                    found++;
                }
            }

            return (found == root->MNumChildren);
        }

        private unsafe void CalcModelMetrics(Scene* scene, ref uint vertexCount, ref uint indexCount, ref byte indexStride)
        {
            for (int i = 0; i < scene->MNumMeshes; i++)
            {
                Mesh* mesh = scene->MMeshes[i];

                vertexCount += mesh->MNumVertices;

                for (int j = 0; j < mesh->MNumFaces; j++)
                {
                    indexCount += mesh->MFaces[j].MNumIndices;
                }
            }

            if (indexCount > ushort.MaxValue)
                indexStride = sizeof(uint);
            else
                indexStride = sizeof(ushort);
        }

        private unsafe bool WriteNode(Scene* scene, Node* node, Stream stream, ref ulong vertexOffset, ref ulong indexOffset, byte indexStride, byte vertexStride, UnsafeArray<byte> vertices, UnsafeArray<byte> indices)
        {
            bool success = true;

            string name = node->MName.AsString;
            byte len = (byte)Math.Min(name.Length, byte.MaxValue - 1);
            if (len != name.Length)
            {
                LogTypes.Import.Warning("Mesh name: \"{a}\", is longer then 255 characters and will be trunacated!", name);
            }

            byte meshCount = (byte)Math.Min(node->MNumMeshes, byte.MaxValue);
            if (len != name.Length)
            {
                LogTypes.Import.Warning("Single node contains more then 256 meshes! Not all will be included.", name);
            }

            byte childCount = (byte)Math.Min(node->MNumChildren, byte.MaxValue);
            if (len != name.Length)
            {
                LogTypes.Import.Warning("Single node contains more then 256 children! Not all will be included.", name);
            }

            NodeInstanceHeader header = new NodeInstanceHeader();
            header.NameLength = len;
            header.MeshCount = meshCount;
            header.ChildCount = childCount;

            StreamUtility.Serialize(stream, ref header);
            if (len != name.Length) name = name = name.Substring(0, len);
            stream.Write(Encoding.UTF8.GetBytes(name));

            for (int i = 0; i < meshCount; i++)
            {
                Mesh* mesh = scene->MMeshes[node->MMeshes[i]];

                uint vertexCount = mesh->MNumVertices;
                uint indexCount = 0;

                for (int j = 0; j < mesh->MNumFaces; j++)
                    indexCount += mesh->MFaces[j].MNumIndices;

                bool hasUVs = mesh->MTextureCoords[0] != null;
                bool hasTangents = mesh->MTangents != null;

                StreamUtility.Serialize(stream, vertexOffset);
                StreamUtility.Serialize(stream, indexOffset);
                StreamUtility.Serialize(stream, vertexCount);
                StreamUtility.Serialize(stream, indexCount, indexStride);

                if (vertexStride == sizeof(VertexHalf))
                {
                    for (int j = 0; j < mesh->MNumVertices; j++)
                    {
                        fixed (byte* ptr00 = &vertices[(int)(vertexOffset++ * vertexStride)])
                        {
                            VertexHalf* v = (VertexHalf*)ptr00;
                            v->Position = new UShort3(mesh->MVertices[j].X, mesh->MVertices[j].Y, mesh->MVertices[j].Z);
                            if (hasUVs)
                                v->UV = new UShort2(mesh->MTextureCoords[0][j].X, mesh->MTextureCoords[0][j].Y);
                            else
                                v->UV = new UShort2(0, 0);
                            if (hasTangents)
                                v->Tangent = new UShort3(mesh->MTangents[j].X, mesh->MTangents[j].Y, mesh->MTangents[j].Z);
                            else
                                v->Tangent = new UShort3(0, 0, 0);
                        }
                    }
                }
                else
                {
                    for (int j = 0; j < mesh->MNumVertices; j++)
                    {
                        fixed (byte* ptr00 = &vertices[(int)(vertexOffset++ * vertexStride)])
                        {
                            Vertex* v = (Vertex*)ptr00;
                            v->Position = new Vector3(mesh->MVertices[j].X, mesh->MVertices[j].Y, mesh->MVertices[j].Z);
                            if (hasUVs)
                                v->UV = new Vector2(mesh->MTextureCoords[0][j].X, mesh->MTextureCoords[0][j].Y);
                            else
                                v->UV = new Vector2(0, 0);
                            if (hasTangents)
                                v->Tangent = new Vector3(mesh->MTangents[j].X, mesh->MTangents[j].Y, mesh->MTangents[j].Z);
                            else
                                v->Tangent = new Vector3(0, 0, 0);
                        }
                    }
                }

                if (indexStride == sizeof(ushort))
                {
                    for (int j = 0; j < mesh->MNumFaces; j++)
                    {
                        Face face = mesh->MFaces[j];
                        if (face.MNumIndices > 3)
                            LogTypes.Import.Warning("Face has more then 3 vertices! This is very unintended and could lead to problems with serilization and unserialization!");
                        for (int k = 0; k < Math.Max(face.MNumIndices, 0U); k++)
                        {
                            ((ushort*)indices)[indexOffset++] = (ushort)face.MIndices[k];
                        }
                    }
                }
                else
                {
                    for (int j = 0; j < mesh->MNumFaces; j++)
                    {
                        Face face = mesh->MFaces[j];
                        if (face.MNumIndices > 3)
                            LogTypes.Import.Warning("Face has more then 3 vertices! This is very unintended and could lead to problems with serilization and unserialization!");
                        for (int k = 0; k < Math.Max(face.MNumIndices, 0U); k++)
                        {
                            ((uint*)indices)[indexOffset++] = face.MIndices[k];
                        }
                    }
                }
            }

            for (int i = 0; i < childCount; i++)
            {
                success = success && WriteNode(scene, node->MChildren[i], stream, ref vertexOffset, ref indexOffset, indexStride, vertexStride, vertices, indices);
            }

            return success;
        }

        private unsafe bool WriteNodeLOD(Scene* scene, Node* root, Stream stream, ref ulong vertexOffset, ref ulong indexOffset, byte indexStride, byte vertexStride, UnsafeArray<byte> vertices, UnsafeArray<byte> indices)
        {
            Dictionary<byte, nint> lodMap = new Dictionary<byte, nint>();
            for (int i = 0; i < root->MNumChildren; i++)
            {
                Node* node = root->MChildren[i];
                string name = node->MName.AsString;

                if (name.Length > 3 && name.StartsWith("LOD") && byte.TryParse(name.Substring(3), out byte v))
                {
                    lodMap.Add(v, (nint)node);
                }
                else
                {
                    return false;
                }
            }

            byte count = (byte)lodMap.Count;
            stream.WriteByte(count);

            for (byte lod = 0; lod < lodMap.Count; lod++)
            {
                if (!lodMap.ContainsKey(lod))
                {
                    LogTypes.Import.Error("Failed to find mesh LOD level: #{a}!", lod);
                }

                Node* lodNode = (Node*)lodMap[lod];

                ushort childCount = (ushort)lodNode->MNumChildren;
                StreamUtility.Serialize(stream, childCount);

                for (int i = 0; i < lodNode->MNumChildren; i++)
                {
                    Node* childNode = lodNode->MChildren[i];

                    string name = childNode->MName.AsString;
                    if (name.Contains('.'))
                    {
                        int last = name.IndexOf('.');

                        if (byte.TryParse(name.Substring(last), out byte v))
                        {
                            name = name.Substring(0, name.Length - 4);
                        }
                    }

                    byte len = (byte)Math.Min(name.Length, byte.MaxValue - 1);
                    if (len != name.Length)
                    {
                        LogTypes.Import.Warning("Mesh name: \"{a}\", is longer then 255 characters and will be trunacated!", name);
                    }

                    if (childNode->MNumMeshes > 1)
                    {
                        LogTypes.Import.Warning("Single node cannot contain more then 1 mesh in LOD mode.");
                    }

                    if (childNode->MNumChildren > 0)
                    {
                        LogTypes.Import.Warning("Single node cannot contain children in LOD mode.");
                    }

                    NodeLODHeader header = new NodeLODHeader();
                    header.NameLength = len;

                    StreamUtility.Serialize(stream, ref header);
                    if (len != name.Length) name = name = name.Substring(0, len);
                    stream.Write(Encoding.UTF8.GetBytes(name));

                    if (childNode->MNumMeshes > 0)
                    {
                        Mesh* mesh = scene->MMeshes[childNode->MMeshes[0]];

                        uint vertexCount = mesh->MNumVertices;
                        uint indexCount = 0;

                        for (int j = 0; j < mesh->MNumFaces; j++)
                            indexCount += mesh->MFaces[j].MNumIndices;

                        bool hasUVs = mesh->MTextureCoords[0] != null;
                        bool hasTangents = mesh->MTangents != null;

                        StreamUtility.Serialize(stream, vertexOffset);
                        StreamUtility.Serialize(stream, indexOffset);
                        StreamUtility.Serialize(stream, vertexCount);
                        StreamUtility.Serialize(stream, indexCount, indexStride);

                        if (vertexStride == sizeof(VertexHalf))
                        {
                            for (int j = 0; j < mesh->MNumVertices; j++)
                            {
                                fixed (byte* ptr00 = &vertices[(int)(vertexOffset++ * vertexStride)])
                                {
                                    VertexHalf* v = (VertexHalf*)ptr00;
                                    v->Position = new UShort3(mesh->MVertices[j].X, mesh->MVertices[j].Y, mesh->MVertices[j].Z);
                                    if (hasUVs)
                                        v->UV = new UShort2(mesh->MTextureCoords[0][j].X, mesh->MTextureCoords[0][j].Y);
                                    else
                                        v->UV = new UShort2(0, 0);
                                    if (hasTangents)
                                        v->Tangent = new UShort3(mesh->MTangents[j].X, mesh->MTangents[j].Y, mesh->MTangents[j].Z);
                                    else
                                        v->Tangent = new UShort3(0, 0, 0);
                                }
                            }
                        }
                        else
                        {
                            for (int j = 0; j < mesh->MNumVertices; j++)
                            {
                                fixed (byte* ptr00 = &vertices[(int)(vertexOffset++ * vertexStride)])
                                {
                                    Vertex* v = (Vertex*)ptr00;
                                    v->Position = new Vector3(mesh->MVertices[j].X, mesh->MVertices[j].Y, mesh->MVertices[j].Z);
                                    if (hasUVs)
                                        v->UV = new Vector2(mesh->MTextureCoords[0][j].X, mesh->MTextureCoords[0][j].Y);
                                    else
                                        v->UV = new Vector2(0, 0);
                                    if (hasTangents)
                                        v->Tangent = new Vector3(mesh->MTangents[j].X, mesh->MTangents[j].Y, mesh->MTangents[j].Z);
                                    else
                                        v->Tangent = new Vector3(0, 0, 0);
                                }
                            }
                        }

                        if (indexStride == sizeof(ushort))
                        {
                            for (int j = 0; j < mesh->MNumFaces; j++)
                            {
                                Face face = mesh->MFaces[j];
                                if (face.MNumIndices > 3)
                                    LogTypes.Import.Warning("Face has more then 3 vertices! This is very unintended and could lead to problems with serilization and unserialization!");
                                for (int k = 0; k < Math.Max(face.MNumIndices, 0U); k++)
                                {
                                    ((ushort*)indices)[indexOffset++] = (ushort)face.MIndices[k];
                                }
                            }
                        }
                        else
                        {
                            for (int j = 0; j < mesh->MNumFaces; j++)
                            {
                                Face face = mesh->MFaces[j];
                                if (face.MNumIndices > 3)
                                    LogTypes.Import.Warning("Face has more then 3 vertices! This is very unintended and could lead to problems with serilization and unserialization!");
                                for (int k = 0; k < Math.Max(face.MNumIndices, 0U); k++)
                                {
                                    ((uint*)indices)[indexOffset++] = face.MIndices[k];
                                }
                            }
                        }
                    }
                    else
                    {
                        StreamUtility.Serialize(stream, 0ul);
                        StreamUtility.Serialize(stream, 0ul);
                        StreamUtility.Serialize(stream, 0u);
                        StreamUtility.Serialize(stream, 0u, indexStride);
                    }
                }
            }

            return true;
        }

        public class Arguments
        {
            public EditorRuntime EDR;
            public string Output;
            public ulong Id;
            public string Hint;
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

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        private struct Vertex
        {
            public Vector3 Position;
            public Vector2 UV;
            public Vector3 Normal;

            public Vector3 Tangent;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        private struct VertexHalf
        {
            public UShort3 Position;
            public UShort2 UV;
            public UShort3 Normal;

            public UShort3 Tangent;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        private struct UShort2
        {
            public ushort X;
            public ushort Y;

            public UShort2(ushort x = 0, ushort y = 0)
            {
                X = x;
                Y = y;
            }

            public unsafe UShort2(float x = 0.0f, float y = 0.0f)
            {
                X = *(ushort*)&x;
                Y = *(ushort*)&y;
            }
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        private struct UShort3
        {
            public ushort X;
            public ushort Y;
            public ushort Z;

            public UShort3(ushort x = 0, ushort y = 0, ushort z = 0)
            {
                X = x;
                Y = y;
                Z = z;
            }

            public unsafe UShort3(float x = 0.0f, float y = 0.0f, float z = 0.0f)
            {
                X = *(ushort*)&x;
                Y = *(ushort*)&y;
                Z = *(ushort*)&z;
            }
        }
    }
}
