using CommunityToolkit.HighPerformance;
using SimpleLib.Debugging;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Vortice.Mathematics;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace SimpleLib.Resources.Data
{
    public class Mesh : IDisposable
    {
        private readonly List<RenderMesh> _lods = new List<RenderMesh>();

        private readonly Model _parent;
        private readonly string _name;

        private BoundingBox _bounds;

        private ValueTuple<nint, int>[]? _vertexBufferData = null;
        private ValueTuple<nint, int>[]? _indexBufferData = null;

        //TODO: expand to both index and vertex buffers?
        private bool _wasModified = false;
        private bool _wasResized = false;

        public Mesh(Model parent, string name)
        {
            _parent = parent;
            _name = name;
            _bounds = BoundingBox.Zero;
        }

        public void Dispose()
        {
            unsafe
            {
                for (int i = 0; i < _vertexBufferData?.Length; i++)
                {
                    if (_vertexBufferData[i].Item1 != nint.Zero)
                    {
                        NativeMemory.Free(_vertexBufferData[i].Item1.ToPointer());
                        MemoryCounter.DecrementCounter("Mesh", (ulong)_vertexBufferData[i].Item2 * (ulong)Unsafe.SizeOf<Vertex>());
                    }
                }

                for (int i = 0; i < _indexBufferData?.Length; i++)
                {
                    if (_indexBufferData[i].Item1 != nint.Zero)
                    {
                        NativeMemory.Free(_indexBufferData[i].Item1.ToPointer());
                        MemoryCounter.DecrementCounter("Mesh", (ulong)_indexBufferData[i].Item2 * (ulong)_parent.Data.IndexStride);
                    }
                }
            }

            _vertexBufferData = null;
            _indexBufferData = null;
        }

        public unsafe void SetVertices(ReadOnlySpan<Vertex> vertices, int offset = 0, int lod = 0)
        {
            if (lod > _lods.Count)
            {
                throw new ArgumentOutOfRangeException("Cant set vertices for lod because none exist at level!");
            }

            if (_parent.Data.IsFinalized)
            {
                throw new InvalidOperationException("Cannot set vertices on a finalized model!");
            }

            if (_vertexBufferData == null)
            {
                throw new Exception("Internal vertex buffer data array is null!");
            }

            FitVertexBuffer(vertices.Length + offset, lod);

            Span<Vertex> storedVertices = new Span<Vertex>((Vertex*)_vertexBufferData[lod].Item1.ToPointer() + offset, _vertexBufferData[lod].Item2);
            vertices.CopyTo(storedVertices);

            _wasModified = true;
        }

        public unsafe void SetIndices(ReadOnlySpan<uint> indices, int offset = 0, int lod = 0)
        {
            if (lod > _lods.Count)
            {
                throw new ArgumentOutOfRangeException("Cant set indices for lod because none exist at level!");
            }

            if (_parent.Data.IsFinalized)
            {
                throw new InvalidOperationException("Cannot set indices on a finalized model!");
            }

            if (_parent.Data.IndexStride != sizeof(uint))
            {
                throw new InvalidOperationException("Cannot set 32bit indices on 16bit model!");
            }

            if (_indexBufferData == null)
            {
                throw new Exception("Internal index buffer data array is null!");
            }

            FitIndexBuffer(indices.Length + offset, lod);

            Span<uint> storedIndices = new Span<uint>((uint*)_indexBufferData[lod].Item1.ToPointer() + offset, _indexBufferData[lod].Item2);
            indices.CopyTo(storedIndices);

            _wasModified = true;
        }

        public unsafe void SetIndices(ReadOnlySpan<ushort> indices, int offset = 0, int lod = 0)
        {
            if (lod > _lods.Count)
            {
                throw new ArgumentOutOfRangeException("Cant set indices for lod because none exist at level!");
            }

            if (_parent.Data.IsFinalized)
            {
                throw new InvalidOperationException("Cannot set indices on a finalized model!");
            }

            if (_parent.Data.IndexStride != sizeof(ushort))
            {
                throw new InvalidOperationException("Cannot set 16bit indices on 32bit model!");
            }

            if (_indexBufferData == null)
            {
                throw new Exception("Internal index buffer data array is null!");
            }

            FitIndexBuffer(indices.Length + offset, lod);

            Span<ushort> storedIndices = new Span<ushort>((ushort*)_indexBufferData[lod].Item1.ToPointer() + offset, _indexBufferData[lod].Item2);
            indices.CopyTo(storedIndices);

            _wasModified = true;
        }

        public unsafe void ClearInternalDataBuffers(bool markAsResized = true, bool markAsModified = true)
        {
            if (_vertexBufferData != null)
            {
                for (int i = 0; i < _vertexBufferData.Length; i++)
                {
                    ValueTuple<nint, int> data = _vertexBufferData[i];
                    if (data.Item1 != nint.Zero)
                    {
                        NativeMemory.Free(data.Item1.ToPointer());
                        MemoryCounter.DecrementCounter("Mesh", (ulong)data.Item2 * (ulong)Unsafe.SizeOf<Vertex>());
                    }
                }

                _vertexBufferData = null;
            }

            if (_indexBufferData != null)
            {
                for (int i = 0; i < _indexBufferData.Length; i++)
                {
                    ValueTuple<nint, int> data = _indexBufferData[i];
                    if (data.Item1 != nint.Zero)
                    {
                        NativeMemory.Free(data.Item1.ToPointer());
                        MemoryCounter.DecrementCounter("Mesh", (ulong)data.Item2 * (ulong)_parent.Data.IndexStride);
                    }
                }

                _indexBufferData = null;
            }

            _wasModified = _wasModified || markAsModified;
            _wasResized = _wasResized || markAsResized;
        }

        private unsafe void FitVertexBuffer(int size, int lod)
        {
            if (_vertexBufferData == null)
            {
                throw new Exception("_vertexBufferData == null");
            }

            ValueTuple<nint, int> vt = _vertexBufferData[lod];
            if (vt.Item1 == nint.Zero || vt.Item2 < size)
            {
                if (vt.Item1 != nint.Zero)
                {
                    NativeMemory.Free(vt.Item1.ToPointer());
                    MemoryCounter.DecrementCounter("Mesh", (ulong)vt.Item2 * (ulong)Unsafe.SizeOf<Vertex>());
                }

                ulong sz = (ulong)size * (ulong)Unsafe.SizeOf<Vertex>();
                nint ptr = (nint)NativeMemory.Alloc((nuint)sz);
                MemoryCounter.IncrementCounter("Mesh", sz);

                _vertexBufferData[lod] = new ValueTuple<nint, int>(ptr, size);
                _wasResized = true;
            }
        }

        private unsafe void FitIndexBuffer(int size, int lod)
        {
            if (_indexBufferData == null)
            {
                throw new Exception("_indexBufferData == null");
            }

            ValueTuple<nint, int> vt = _indexBufferData[lod];
            if (vt.Item1 == nint.Zero || vt.Item2 < size)
            {
                if (vt.Item1 != nint.Zero)
                {
                    NativeMemory.Free(vt.Item1.ToPointer());
                    MemoryCounter.DecrementCounter("Mesh", (ulong)vt.Item2 * (ulong)_parent.Data.IndexStride);
                }

                ulong sz = (ulong)size * (ulong)_parent.Data.IndexStride;
                nint ptr = (nint)NativeMemory.Alloc((nuint)sz);
                MemoryCounter.IncrementCounter("Mesh", sz);

                _indexBufferData[lod] = new ValueTuple<nint, int>(ptr, size);
                _wasResized = true;
            }
        }

        internal void AddMeshForLOD(byte lod, RenderMesh mesh)
        {
            if (lod >= _lods.Count)
            {
                for (int i = _lods.Count; i <= lod; i++)
                    _lods.Add(new RenderMesh());
            }

            if (_vertexBufferData == null || _vertexBufferData.Length != _lods.Count)
            {
                ValueTuple<nint, int>[] newArray = new ValueTuple<nint, int>[_lods.Count];
                Array.Fill(newArray, new ValueTuple<nint, int>(nint.Zero, 0));

                if (_vertexBufferData != null)
                    Array.Copy(_vertexBufferData, newArray, _vertexBufferData.Length);

                _vertexBufferData = newArray;
            }

            if (_indexBufferData == null || _indexBufferData.Length != _lods.Count)
            {
                ValueTuple<nint, int>[] newArray = new ValueTuple<nint, int>[_lods.Count];
                Array.Fill(newArray, new ValueTuple<nint, int>(nint.Zero, 0));

                if (_indexBufferData != null)
                    Array.Copy(_indexBufferData, newArray, _indexBufferData.Length);

                _indexBufferData = newArray;
            }

            _lods[lod] = mesh;

            _bounds = BoundingBox.CreateMerged(_bounds, mesh.Bounds);
        }

        public unsafe void RecalculateBounds()
        {
            _bounds = new BoundingBox(Vector3.PositiveInfinity, Vector3.NegativeInfinity);

            if (_vertexBufferData == null)
            {
                LogTypes.Resources.Warning("Cannot reculculate bounds of mesh: \"{a}\" because vertex data is null!", _name);
                return;
            }

            if (_indexBufferData == null)
            {
                LogTypes.Resources.Warning("Cannot reculculate bounds of mesh: \"{a}\" because index data is null!", _name);
                return;
            }

            bool isFirstBounds = true;
            Span<RenderMesh> rms = _lods.AsSpan();
            for (int i = 0; i < rms.Length; i++)
            {
                ref RenderMesh rm = ref rms[i];
                rm.Bounds = new BoundingBox(Vector3.PositiveInfinity, Vector3.NegativeInfinity);

                ValueTuple<nint, int> vertices = _vertexBufferData[i];

                if (vertices.Item1 == nint.Zero)
                {
                    LogTypes.Resources.Warning("Cannot calculate bounds of LOD: {}# because either vertex data is null for mesh: \"{}\"!", i, _name);
                    continue;
                }

                Span<Vertex> vertexSpan = new Span<Vertex>(vertices.Item1.ToPointer(), vertices.Item2);
                for (int j = 0; j < vertexSpan.Length; j++)
                {
                    ref Vertex v = ref vertexSpan[j];
                    rm.Bounds.Min = Vector3.Min(rm.Bounds.Min, v.Position);
                    rm.Bounds.Max = Vector3.Max(rm.Bounds.Max, v.Position);
                }

                _bounds = isFirstBounds ? rm.Bounds : BoundingBox.CreateMerged(_bounds, rm.Bounds);
                isFirstBounds = false;
            }
        }

        public RenderMesh GetMeshForLOD(byte lod)
        {
            return _lods[lod];
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void ResetModifiedState()
        {
            _wasModified = false;
            _wasResized = false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal (nint RawData, int RawSize) GetVertexBufferData(int lod)
        {
            if (_vertexBufferData == null)
            {
                throw new Exception("_vertexBufferData == null");
            }

            return _vertexBufferData[lod];
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal (nint RawData, int RawSize) GetIndexBufferData(int lod)
        {
            if (_indexBufferData == null)
            {
                throw new Exception("_indexBufferData == null");
            }

            return _indexBufferData[lod];
        }

        internal int CountVertexBufferSize()
        {
            if (_vertexBufferData == null)
            {
                throw new Exception("_vertexBufferData == null");
            }

            int count = 0;
            for (int i = 0; i < _vertexBufferData.Length; i++)
            {
                count += _vertexBufferData[i].Item2;
            }

            return count;
        }

        internal int CountIndexBufferSize()
        {
            if (_indexBufferData == null)
            {
                throw new Exception("_vertexBufferData == null");
            }

            int count = 0;
            for (int i = 0; i < _indexBufferData.Length; i++)
            {
                count += _indexBufferData[i].Item2;
            }

            return count;
        }

        public string Name => _name;
        public BoundingBox Bounds => _bounds;

        public Model OwningModel => _parent;

        internal bool WasModified => _wasModified;
        internal bool WasResized => _wasResized;

        internal Span<RenderMesh> LODs => CollectionsMarshal.AsSpan(_lods);

        public struct RenderMesh
        {
            public uint VertexCount;
            public uint IndexCount;

            public uint VertexOffset;
            public uint IndexOffset;

            public BoundingBox Bounds;
        }
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct Vertex
    {
        public Vector3 Position;
        public Vector2 UV;
        public Vector3 Normal;

        public Vector3 Tangent;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
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
