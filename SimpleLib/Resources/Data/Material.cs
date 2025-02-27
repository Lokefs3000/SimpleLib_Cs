using SimpleRHI;
using System.Runtime.CompilerServices;

namespace SimpleLib.Resources.Data
{
    public class Material : Resource
    {
        private readonly Storage _storage;

        public Shader? Shader { get => _storage.Shader; set => SwitchShader(value); }

        //cache?
        public IGfxGraphicsPipeline? Pipeline => Shader?.GetPipelineForVariant(_storage.ActiveVariant);

        public IDisposable Data => (IDisposable)_storage;

        internal Material(ulong id, IDisposable? data = null) : base(id)
        {
            _storage = (data != null && data is Storage) ? (Storage)data : new Storage();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void EnableKeyword(string keyword) => EnableKeyword(GetKeywordId(keyword));

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void EnableKeyword(int keyword)
        {
            if (_storage.Variants == null)
            {
                return;
            }

            _storage.ActiveVariant |= _storage.Variants[keyword].Bit;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void DisableKeyword(string keyword) => DisableKeyword(GetKeywordId(keyword));

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void DisableKeyword(int keyword)
        {
            if (_storage.Variants == null)
            {
                return;
            }

            _storage.ActiveVariant &= ~_storage.Variants[keyword].Bit;
        }

        public int GetKeywordId(string keyword)
        {
            if (_storage.Variants == null)
            {
                return -1;
            }

            Span<Storage.VariantData> variants = _storage.Variants.AsSpan();
            for (int i = 0; i < variants.Length; i++)
            {
                ref Storage.VariantData data = ref variants[i];
                if (data.Name == keyword)
                {
                    return i;
                }
            }

            LogTypes.Resources.Error("Failed to find keyword: \"{}\"!", keyword);
            return -1;
        }

        internal void BindResources(Shader shader, IGfxDevice device)
        {
            _storage.Dispose();

            _storage.RenderDevice = device;

            SwitchShader(shader);

            HasLoaded = true;
        }

        internal void Commit()
        {

        }

        private void SwitchShader(Shader? shader)
        {
            if (_storage.RenderDevice == null)
            {
                return;
            }

            _storage.Shader = null;

            IShaderPackage.ReflectionData? reflection = shader?.Reflection;
            _storage.Reflection = reflection;

            if (true)
            {
                _storage.Dispose();
                _storage.Shader = shader;

                if (_storage.Shader == null)
                {
                    return;
                }

                _storage.ActiveVariant = 0;
            }
        }

        private class Storage : IDisposable
        {
            public IGfxDevice? RenderDevice;
            public Shader? Shader;
            public ulong ActiveVariant = 0;
            public IShaderPackage.ReflectionData? Reflection;
            public VariantData[]? Variants = null;

            public void Dispose()
            {
                RenderDevice = null;
                Shader = null;
                Reflection = null;
                Variants = null;
            }

            public struct VariantData
            {
                public string Name;
                public ulong Bit;
            }
        }
    }
}
