using Schedulers;
using SimpleLib.Files;
using SimpleLib.Resources.Constructors;
using SimpleLib.Resources.Data;
using SimpleLib.Resources.Loaders;
using SimpleRHI;

namespace SimpleLib.Resources
{
    public class ResourceHandler : IDisposable
    {
        private static ResourceHandler? _instance;

        private Dictionary<ulong, StoredResourceData> _stored = new Dictionary<ulong, StoredResourceData>();

        private JobScheduler _scheduler;
        private Filesystem _filesystem;
        private IGfxDevice _renderDevice;

        private Texture _loadingTexture;

        private IGfxPipelineStateCache _pipelineStateCache;
        private IShaderPackage? _shaderPackage = null;

        public ResourceHandler(JobScheduler scheduler, Filesystem fs, IGfxDevice renderDevice)
        {
            if (_instance != null)
            {
                throw new InvalidOperationException("ResourceHandler instance already exits!");
            }

            _instance = this;

            _scheduler = scheduler;
            _filesystem = fs;
            _renderDevice = renderDevice;

            InitializeFactories();

            _loadingTexture = LoadTexture(AutoFileRegisterer.EngineTexturesLoadingPng, true);

            unsafe
            {
                IGfxPipelineStateCache.CreateInfo desc = new IGfxPipelineStateCache.CreateInfo();
                if (File.Exists("pso.cache"))
                {
                    desc.CacheBinary = File.ReadAllBytes("pso.cache");
                }

                _pipelineStateCache = renderDevice.CreatePipelineStateCache(desc);
            }
        }

        public void Dispose()
        {
            _instance = null;

            try
            {
                ReadOnlySpan<byte> data = _pipelineStateCache.Serialize();
                File.WriteAllBytes("pso.cache", data);
            }
            catch (Exception ex)
            {
                LogTypes.Resources.Warning(ex, "Failed to save PSO cache!");
            }

            _pipelineStateCache.Dispose();
            _shaderPackage?.Dispose();

            foreach (var kvp in _stored)
            {
                kvp.Value.Data.Dispose();
            }

            GC.SuppressFinalize(this);
        }

        private void InitializeFactories()
        {
            TextureFactory.Device = _renderDevice;
            ModelFactory.Device = _renderDevice;
        }

        public void SetShaderPackage(IShaderPackage package)
        {
            _shaderPackage?.Dispose();
            _shaderPackage = package;
        }

        public void UnloadUnusedResources()
        {
            List<ulong> unused = new List<ulong>();
            foreach (var kvp in _stored)
            {
                StoredResourceData data = kvp.Value;
                if (!data.Resource.IsAlive)
                {
                    unused.Add(kvp.Key);
                    kvp.Value.Data.Dispose();
                }
            }

            foreach (ulong id in unused)
            {
                _stored.Remove(id);
            }

            LogTypes.Resources.Information("Unloaded: #{a}! Resources left loaded: {b}.", unused.Count, _stored.Count);
        }

        public static Texture LoadTexture(ulong id, bool wait = false)
        {
            if (_instance == null)
            {
                throw new ArgumentNullException("Static instance is null!");
            }

            lock (_instance._stored)
            {
                if (_instance._stored.TryGetValue(id, out StoredResourceData? data))
                {
                    Texture ret;
                    if (!data.Resource.IsAlive || data.Resource.Target == null)
                    {
                        ret = new Texture(id, data.Data);
                        data.Resource.Target = ret;
                    }
                    else
                    {
                        ret = (Texture)data.Resource.Target;
                    }

                    return ret;
                }
            }

            Texture texture = new Texture(id);

            TextureLoaderImpl.Payload payload = new TextureLoaderImpl.Payload();
            payload.Device = _instance._renderDevice;
            payload.Object = texture;
            payload.Filesystem = _instance._filesystem;

            lock (TextureLoaderImpl.Pending)
            {
                TextureLoaderImpl.Pending.Enqueue(payload);
            }

            lock (_instance._stored)
            {
                _instance._stored.Add(id, new StoredResourceData(texture.Data, texture));
            }

            JobHandle handle = _instance._scheduler.Schedule(TextureLoaderImpl.Impl);
            if (wait)
            {
                _instance._scheduler.Flush();
                handle.Complete();
            }

            return texture;
        }

        public static Model LoadModel(ulong id, bool wait = false)
        {
            if (_instance == null)
            {
                throw new ArgumentNullException("Static instance is null!");
            }

            lock (_instance._stored)
            {
                if (_instance._stored.TryGetValue(id, out StoredResourceData? data))
                {
                    Model ret;
                    if (!data.Resource.IsAlive || data.Resource.Target == null)
                    {
                        ret = new Model(id, data.Data);
                        data.Resource.Target = ret;
                    }
                    else
                    {
                        ret = (Model)data.Resource.Target;
                    }

                    return ret;
                }
            }

            Model model = new Model(id);

            ModelLoaderImpl.Payload payload = new ModelLoaderImpl.Payload();
            payload.RenderDevice = _instance._renderDevice;
            payload.Object = model;
            payload.Filesystem = _instance._filesystem;
            payload.Id = id;

            lock (ModelLoaderImpl.Payloads)
            {
                ModelLoaderImpl.Payloads.Enqueue(payload);
            }

            lock (_instance._stored)
            {
                _instance._stored.Add(id, new StoredResourceData(model.Data, model));
            }

            JobHandle handle = _instance._scheduler.Schedule(ModelLoaderImpl.Impl);
            if (wait)
            {
                _instance._scheduler.Flush();
                handle.Complete();
            }

            return model;
        }

        public static Shader LoadShader(ulong id, bool wait = false)
        {
            if (_instance == null)
            {
                throw new ArgumentNullException("Static instance is null!");
            }

            lock (_instance._stored)
            {
                if (_instance._stored.TryGetValue(id, out StoredResourceData? data))
                {
                    Shader ret;
                    if (!data.Resource.IsAlive || data.Resource.Target == null)
                    {
                        ret = new Shader(id, data.Data);
                        data.Resource.Target = ret;
                    }
                    else
                    {
                        ret = (Shader)data.Resource.Target;
                    }

                    return ret;
                }
            }

            Shader shader = new Shader(id);

            shader.BindResources(_instance._pipelineStateCache, _instance._renderDevice, _instance._shaderPackage ?? throw new ArgumentNullException("No shader pack loaded!"));

            lock (_instance._stored)
            {
                _instance._stored.Add(id, new StoredResourceData(shader.Data, shader));
            }

            return shader;
        }

        public static Material LoadMaterial(ulong id, bool wait = false)
        {
            if (_instance == null)
            {
                throw new ArgumentNullException("Static instance is null!");
            }

            lock (_instance._stored)
            {
                if (_instance._stored.TryGetValue(id, out StoredResourceData? data))
                {
                    Material ret;
                    if (!data.Resource.IsAlive || data.Resource.Target == null)
                    {
                        ret = new Material(id, data.Data);
                        data.Resource.Target = ret;
                    }
                    else
                    {
                        ret = (Material)data.Resource.Target;
                    }

                    return ret;
                }
            }

            Material material = new Material(id);

            MaterialLoaderImpl.Payload payload = new MaterialLoaderImpl.Payload();
            payload.RenderDevice = _instance._renderDevice;
            payload.Object = material;
            payload.Filesystem = _instance._filesystem;

            lock (MaterialLoaderImpl.Pending)
            {
                MaterialLoaderImpl.Pending.Enqueue(payload);
            }

            lock (_instance._stored)
            {
                _instance._stored.Add(id, new StoredResourceData(material.Data, material));
            }

            JobHandle handle = _instance._scheduler.Schedule(MaterialLoaderImpl.Impl);
            if (wait)
            {
                _instance._scheduler.Flush();
                handle.Complete();
            }

            return material;
        }

        private class StoredResourceData
        {
            public IDisposable Data;
            public WeakReference Resource;

            public StoredResourceData(IDisposable data, Resource resource)
            {
                Data = data;
                Resource = new WeakReference(resource);
            }
        }
    }
}
