using CommunityToolkit.HighPerformance;
using SimpleLib.Debugging;
using SimpleLib.Render.Copy;
using SimpleRHI;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Vortice.Mathematics;

namespace SimpleLib.Resources.Data
{
    public class Model : Resource
    {
        private readonly Storage _storage;

        internal Storage Data => (Storage)_storage;

        public Model(ulong id, IDisposable? data = null) : base(id)
        {
            _storage = (data != null && data is Storage) ? (Storage)data : new Storage();
        }

        internal void SetupBasicResources(IGfxDevice device, byte indexStride, bool frequentUpdate)
        {
            _storage.Device = device;
            _storage.IndexStride = indexStride;
            _storage.FrequentUpdate = frequentUpdate;
        }

        //!!! WARNING: UNSAFE TO ADD IF DATA IS ALREADY WITHIN MODEL !!!
        public void AddMesh(Mesh mesh)
        {
            int id = mesh.Name.GetDjb2HashCode();

            for (int i = 0; i < _storage.Meshes.Count; i++)
            {
                KeyValuePair<int, Mesh> kvp = _storage.Meshes[i];
                if (kvp.Key == id)
                {
                    _storage.Meshes[i] = new KeyValuePair<int, Mesh>(id, mesh);
                    return;
                }
            }

            _storage.Meshes.Add(new KeyValuePair<int, Mesh>(id, mesh));
        }

        public Mesh? GetMesh(string name)
        {
            int id = name.GetDjb2HashCode();

            for (int i = 0; i < _storage.Meshes.Count; i++)
            {
                KeyValuePair<int, Mesh> kvp = _storage.Meshes[i];
                if (kvp.Key == id)
                {
                    return kvp.Value;
                }
            }

            return null;
        }

        internal void RecalculateBounds_OLD()
        {
            BoundingBox bounds = BoundingBox.Zero;
            for (int i = 0; i < _storage.Meshes.Count; i++)
            {
                KeyValuePair<int, Mesh> kvp = _storage.Meshes[i];
                bounds = BoundingBox.CreateMerged(bounds, kvp.Value.Bounds);
            }

            _storage.Bounds = bounds;
        }

        public void UploadMeshes(bool finalize = false)
        {
            if (_storage.IsFinalized)
            {
                throw new InvalidOperationException("Cannot upload meshes as model is already finalized!");
            }

            if (_storage.FrequentUpdate && finalize)
            {
                throw new InvalidOperationException("Cannot finalize a model marked as having frequent updates!");
            }

            if (!DidAnyMeshChange())
            {
                return;
            }

            AssembleInternalBuffers(out bool didModifyVertices, out bool didModifyIndices);

            if (_storage.VertexBuffer == null || didModifyVertices)
            {
                try
                {
                    _storage.VertexBuffer?.Dispose();
                    _storage.VertexBufferView?.Dispose();

                    _storage.VertexBuffer = _storage.Device?.CreateBuffer(new IGfxBuffer.CreateInfo
                    {
                        Name = $"VertexBuffer-{Id}",
                        Size = (ulong)_storage.InternalVertexCount * (ulong)Unsafe.SizeOf<Vertex>(),
                        Bind = GfxBindFlags.VertexBuffer,
                        MemoryUsage = finalize ? GfxMemoryUsage.Immutable : (_storage.FrequentUpdate ? GfxMemoryUsage.Dynamic : GfxMemoryUsage.Default),
                        CpuAccess = GfxCPUAccessFlags.Write,
                        Data = _storage.InternalVertexBuffer
                    });

                    MemoryCounter.IncrementCounter("Model.VRAM", (ulong)_storage.InternalVertexCount * (ulong)Unsafe.SizeOf<Vertex>());

                    _storage.VertexBufferView = _storage.VertexBuffer?.CreateView(new IGfxBufferView.CreateInfo { Stride = (byte)Unsafe.SizeOf<Vertex>() });

                    ResourceUploader.Upload(this, _storage.VertexBuffer, _storage.InternalVertexBuffer);
                }
                catch (Exception ex)
                {
                    LogTypes.Resources.Error(ex, "Failed to create model vertex buffer!");
                }
            }

            if (_storage.IndexBuffer == null || didModifyIndices)
            {
                try
                {
                    _storage.IndexBuffer?.Dispose();
                    _storage.IndexBufferView?.Dispose();

                    _storage.IndexBuffer = _storage.Device?.CreateBuffer(new IGfxBuffer.CreateInfo
                    {
                        Name = $"IndexBuffer-{Id}",
                        Size = (ulong)_storage.InternalIndexCount * (ulong)_storage.IndexStride,
                        Bind = GfxBindFlags.IndexBuffer,
                        MemoryUsage = finalize ? GfxMemoryUsage.Immutable : (_storage.FrequentUpdate ? GfxMemoryUsage.Dynamic : GfxMemoryUsage.Default),
                        CpuAccess = GfxCPUAccessFlags.Write,
                        Data = _storage.InternalIndexBuffer
                    });

                    MemoryCounter.IncrementCounter("Model.VRAM", (ulong)_storage.InternalIndexCount * (ulong)_storage.IndexStride);

                    _storage.IndexBufferView = _storage.IndexBuffer?.CreateView(new IGfxBufferView.CreateInfo { Stride = _storage.IndexStride });

                    ResourceUploader.Upload(this, _storage.IndexBuffer, _storage.InternalIndexBuffer);
                }
                catch (Exception ex)
                {
                    LogTypes.Resources.Error(ex, "Failed to create model vertex buffer!");
                }
            }

            _storage.IsFinalized = finalize;

            if (finalize)
            {
                unsafe
                {
                    if (_storage.InternalVertexBuffer != nint.Zero)
                    {
                        NativeMemory.Free(_storage.InternalVertexBuffer.ToPointer());
                        MemoryCounter.DecrementCounter("Model", (ulong)_storage.InternalVertexCount * (ulong)Unsafe.SizeOf<Vertex>());
                    }

                    if (_storage.InternalIndexBuffer != nint.Zero)
                    {
                        NativeMemory.Free(_storage.InternalIndexBuffer.ToPointer());
                        MemoryCounter.DecrementCounter("Model", (ulong)_storage.InternalIndexCount * (ulong)_storage.IndexStride);
                    }

                    for (int i = 0; i < _storage.Meshes.Count; i++)
                    {
                        _storage.Meshes[i].Value.ClearInternalDataBuffers();
                    }
                }

                _storage.InternalVertexBuffer = nint.Zero;
                _storage.InternalVertexCount = 0;

                _storage.InternalIndexBuffer = nint.Zero;
                _storage.InternalIndexCount = 0;
            }
        }

        public void RecalculateBounds()
        {
            if (_storage.IsFinalized)
            {
                throw new InvalidOperationException("Cannot recalculate bounds on a finalized model!");
            }

            bool isFirstBounds = true;
            _storage.Bounds = new BoundingBox(Vector3.PositiveInfinity, Vector3.NegativeInfinity); ;
            for (int i = 0; i < _storage.Meshes.Count; i++)
            {
                Mesh mesh = _storage.Meshes[i].Value;
                mesh.RecalculateBounds();

                _storage.Bounds = isFirstBounds ? mesh.Bounds : BoundingBox.CreateMerged(_storage.Bounds, mesh.Bounds);
                isFirstBounds = false;
            }
        }

        private bool DidAnyMeshChange()
        {
            for (int i = 0; i < _storage.Meshes.Count; i++)
            {
                if (_storage.Meshes[i].Value.WasModified)
                {
                    return true;
                }
            }

            return false;
        }

        private unsafe void AssembleInternalBuffers(out bool didModifyVertices, out bool didModifyIndices)
        {
            CountMeshDataRequirements(out int vertexCount, out bool verticesResized, out int indexCount, out bool indicesResized);

            didModifyVertices = false;
            didModifyIndices = false;

            if (_storage.InternalVertexBuffer == nint.Zero || verticesResized || _storage.InternalVertexCount != vertexCount)
            {
                if (_storage.InternalVertexBuffer != nint.Zero)
                {
                    NativeMemory.Free(_storage.InternalVertexBuffer.ToPointer());
                    MemoryCounter.DecrementCounter("Model", (ulong)_storage.InternalVertexCount * (ulong)Unsafe.SizeOf<Vertex>());
                }

                ulong sz = (ulong)vertexCount * (ulong)Unsafe.SizeOf<Vertex>();
                _storage.InternalVertexBuffer = (nint)NativeMemory.Alloc((nuint)sz);
                MemoryCounter.IncrementCounter("Model", sz);

                _storage.InternalVertexCount = vertexCount;
                didModifyVertices = true;
            }

            if (_storage.InternalIndexBuffer == nint.Zero || verticesResized || _storage.InternalIndexCount != indexCount)
            {
                if (_storage.InternalIndexBuffer != nint.Zero)
                {
                    NativeMemory.Free(_storage.InternalIndexBuffer.ToPointer());
                    MemoryCounter.DecrementCounter("Model", (ulong)_storage.InternalIndexCount * (ulong)_storage.IndexStride);
                }

                ulong sz = (ulong)indexCount * (ulong)_storage.IndexStride;
                _storage.InternalIndexBuffer = (nint)NativeMemory.Alloc((nuint)sz);
                MemoryCounter.IncrementCounter("Model", sz);

                _storage.InternalIndexCount = indexCount;
                didModifyIndices = true;
            }

            Vertex* baseVertex = (Vertex*)_storage.InternalVertexBuffer.ToPointer();
            byte* baseIndex = (byte*)_storage.InternalIndexBuffer.ToPointer();

            for (int i = 0; i < _storage.Meshes.Count; i++)
            {
                Mesh mesh = _storage.Meshes[i].Value;
                if (!mesh.WasResized && !mesh.WasModified)
                {
                    continue;
                }

                Span<Mesh.RenderMesh> renderMeshes = mesh.LODs;
                for (int lod = 0; lod < renderMeshes.Length; lod++)
                {
                    var vertexBufferData = mesh.GetVertexBufferData(lod);
                    var indexBufferData = mesh.GetIndexBufferData(lod);

                    NativeMemory.Copy(vertexBufferData.RawData.ToPointer(), baseVertex, (nuint)((ulong)vertexBufferData.RawSize * (ulong)Unsafe.SizeOf<Vertex>()));
                    NativeMemory.Copy(indexBufferData.RawData.ToPointer(), baseIndex, (nuint)((ulong)indexBufferData.RawSize * (ulong)_storage.IndexStride));
                    
                    ref Mesh.RenderMesh rm = ref renderMeshes[lod];
                    rm.VertexCount = (uint)vertexBufferData.RawSize;
                    rm.IndexCount = (uint)indexBufferData.RawSize;
                    rm.VertexOffset = (uint)(((nint)baseVertex - _storage.InternalVertexBuffer).ToInt64() / (long)Unsafe.SizeOf<Vertex>());
                    rm.IndexOffset = (uint)(((nint)baseIndex - _storage.InternalIndexBuffer).ToInt64() / (long)_storage.IndexStride);

                    baseVertex += vertexBufferData.RawSize;
                    baseIndex += (ulong)indexBufferData.RawSize * (ulong)_storage.IndexStride;
                }
                
                mesh.ResetModifiedState();
            }
        }

        private void CountMeshDataRequirements(out int vertexCount, out bool verticesResized, out int indicesCount, out bool indicesResized)
        {
            vertexCount = 0;
            verticesResized = false;
            indicesCount = 0;
            indicesResized = false;

            for (int i = 0; i < _storage.Meshes.Count; i++)
            {
                Mesh mesh = _storage.Meshes[i].Value;

                vertexCount += mesh.CountVertexBufferSize();
                indicesCount += mesh.CountIndexBufferSize();
                verticesResized = verticesResized || mesh.WasResized;
                indicesResized = indicesResized || mesh.WasResized;
            }
        }

        public IGfxBufferView? VertexBuffer => _storage.VertexBufferView;
        public IGfxBufferView? IndexBuffer => _storage.IndexBufferView;
        public byte IndexStride => _storage.IndexStride;

        public BoundingBox Bounds => _storage.Bounds;

        public bool FrequentUpdate => _storage.FrequentUpdate;
        public bool IsFinalized => _storage.IsFinalized;

        internal class Storage : IDisposable
        {
            public List<KeyValuePair<int, Mesh>> Meshes = new List<KeyValuePair<int, Mesh>>();

            public IGfxDevice? Device;

            public IGfxBuffer? VertexBuffer;
            public IGfxBufferView? VertexBufferView;

            public IGfxBuffer? IndexBuffer;
            public IGfxBufferView? IndexBufferView;

            public BoundingBox Bounds = BoundingBox.Zero;

            public byte IndexStride = 0;

            public bool FrequentUpdate = false;
            public bool IsFinalized = false;

            public nint InternalVertexBuffer = nint.Zero;
            public nint InternalIndexBuffer = nint.Zero;

            public int InternalVertexCount = 0;
            public int InternalIndexCount = 0;

            public void Dispose()
            {
                unsafe
                {
                    if (InternalVertexBuffer != nint.Zero)
                    {
                        NativeMemory.Free(InternalVertexBuffer.ToPointer());
                        MemoryCounter.DecrementCounter("Model", (ulong)InternalVertexCount * (ulong)Unsafe.SizeOf<Vertex>());
                    }

                    if (InternalIndexBuffer != nint.Zero)
                    {
                        NativeMemory.Free(InternalIndexBuffer.ToPointer());
                        MemoryCounter.DecrementCounter("Model", (ulong)InternalIndexCount * (ulong)IndexStride);
                    }

                    for (int i = 0; i < Meshes.Count; i++)
                    {
                        Meshes[i].Value.ClearInternalDataBuffers();
                    }
                }

                InternalVertexBuffer = nint.Zero;
                InternalVertexCount = 0;

                InternalIndexBuffer = nint.Zero;
                InternalIndexCount = 0;
            }
        }
    }
}
