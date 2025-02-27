using SimpleRHI;
using Vortice.Mathematics;

namespace SimpleLib.Resources.Data
{
    public class Model : Resource
    {
        private readonly Storage _storage;

        public List<KeyValuePair<int, Mesh>> Meshes => _storage.Meshes;
        public IGfxBuffer? VertexBuffer => _storage.VertexBuffer;
        public IGfxBuffer? IndexBuffer => _storage.IndexBuffer;
        public IGfxBufferView? VertexBufferView => _storage.VertexBufferView;
        public IGfxBufferView? IndexBufferView => _storage.IndexBufferView;
        public GfxValueType IndexBufferType => _storage.IndexBufferType;
        public BoundingBox Bounds => _storage.Bounds;

        public IDisposable Data => (IDisposable)_storage;

        public Model(ulong id, IDisposable? data = null) : base(id)
        {
            _storage = (data != null && data is Storage) ? (Storage)data : new Storage();
        }

        internal void BindResources(IGfxBuffer vertexBuffer, IGfxBuffer indexBuffer, GfxValueType indexBufferType)
        {
            _storage.Dispose();

            _storage.VertexBuffer = vertexBuffer;
            _storage.IndexBuffer = indexBuffer;
            _storage.VertexBufferView = vertexBuffer.CreateView(new IGfxBufferView.CreateInfo { });
            _storage.IndexBufferView = indexBuffer.CreateView(new IGfxBufferView.CreateInfo { });
            _storage.IndexBufferType = indexBufferType;

            HasLoaded = true;
        }

        public void AddMesh(Mesh mesh)
        {
            int id = mesh.Name.GetHashCode();

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
            int id = name.GetHashCode();

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

        public void RecalculateBounds()
        {
            BoundingBox bounds = BoundingBox.Zero;
            for (int i = 0; i < _storage.Meshes.Count; i++)
            {
                KeyValuePair<int, Mesh> kvp = _storage.Meshes[i];
                bounds = BoundingBox.CreateMerged(bounds, kvp.Value.Bounds);
            }

            _storage.Bounds = bounds;
        }

        private class Storage : IDisposable
        {
            public List<KeyValuePair<int, Mesh>> Meshes = new List<KeyValuePair<int, Mesh>>();
            public IGfxBuffer? VertexBuffer = null;
            public IGfxBuffer? IndexBuffer = null;
            public IGfxBufferView? VertexBufferView = null;
            public IGfxBufferView? IndexBufferView = null;
            public GfxValueType IndexBufferType = GfxValueType.Unkown;
            public BoundingBox Bounds = BoundingBox.Zero;

            public void Dispose()
            {
                VertexBuffer?.Dispose();
                IndexBuffer?.Dispose();
            }
        }
    }
}
