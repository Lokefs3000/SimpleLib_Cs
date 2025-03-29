using SimpleRHI;
using System.Runtime.InteropServices;

namespace SimpleLib.Resources.Data
{
    public class Shader : Resource
    {
        private readonly Storage _storage;

        public IShaderPackage.ReflectionData? Reflection => _storage.Reflection;

        public IDisposable Data => (IDisposable)_storage;

        internal Shader(ulong id, IDisposable? data = null) : base(id)
        {
            _storage = (data != null && data is Storage) ? (Storage)data : new Storage();
        }

        internal void BindResources(IGfxPipelineStateCache cache, IGfxDevice device, IShaderPackage package)
        {
            package.GetShaderVariant(IShaderPackage.ShaderType.Vertex, Id, 0);
            package.GetShaderVariant(IShaderPackage.ShaderType.Pixel, Id, 0);

            _storage.Reflection = package.LoadReflection(Id);
            _storage.Cache = cache;
            _storage.Device = device;
            _storage.Package = package;

            HasLoaded = true;
        }

        internal IGfxGraphicsPipeline? GetPipelineForVariant(ulong variant)
        {
            for (int i = 0; i < _storage.PipelineState.Count; i++)
            {
                if (_storage.PipelineState[i].Key == variant)
                {
                    return _storage.PipelineState[i].Value;
                }
            }

            return CreateNewVariant(variant);
        }

        private IGfxGraphicsPipeline? CreateNewVariant(ulong variant)
        {
            IShaderPackage.ReflectionData? reflection = _storage.Package.LoadReflection(Id);
            if (reflection == null)
            {
                _storage.PipelineState.Add(new KeyValuePair<ulong, IGfxGraphicsPipeline?>(variant, null));
                return null;
            }

            IGfxGraphicsPipeline.CreateInfo desc = new IGfxGraphicsPipeline.CreateInfo();

            //eww
            unsafe
            {
                fixed (IGfxGraphicsPipeline.CreateInfo* ci = &reflection.CreateInfo)
                {
                    NativeMemory.Copy(ci, &desc, (nuint)sizeof(IGfxGraphicsPipeline.CreateInfo));
                }
            }

            desc.VertexShaderBytecode = _storage.Package.GetShaderVariant(IShaderPackage.ShaderType.Vertex, Id, variant);
            desc.PixelShaderBytecode = _storage.Package.GetShaderVariant(IShaderPackage.ShaderType.Pixel, Id, variant);

            InitializeCreateInfoResources(ref desc, reflection.VariantData[variant]);

            desc.Blend = (reflection.BlendDescriptions.Count == 0) ? [new IGfxGraphicsPipeline.CreateInfo.RenderTargetBlendDesc()] : reflection.BlendDescriptions.ToArray();
            desc.InputLayout = reflection.VariantData[variant].InputElements.ToArray();
            desc.RTVFormats = [GfxFormat.R8G8B8A8_UNORM]; //will replace hardcoded values layer
            desc.DSVFormat = GfxFormat.D24_UNORM_S8_UINT; //same here!
            desc.Name = $"{Id}_{variant}";
            desc.PipelineStateCache = _storage.Cache;

            IGfxGraphicsPipeline? pipeline = null;

            try
            {
                pipeline = _storage.Device?.CreateGraphicsPipeline(desc);
            }
            catch (Exception ex)
            {
                LogTypes.Resources.Error(ex, "Failed to create graphics pipeline variant: {a}!", variant);
            }

            _storage.PipelineState.Add(new KeyValuePair<ulong, IGfxGraphicsPipeline?>(variant, pipeline));

            return pipeline;
        }

        private void InitializeCreateInfoResources(ref IGfxGraphicsPipeline.CreateInfo ci, IShaderPackage.ReflectionData.Variant variant)
        {
            ci.Resources = new IGfxGraphicsPipeline.CreateInfo.ResourceDescriptor[variant.Parameters.Count + variant.ConstantBuffers.Count];
            ci.Samplers = new IGfxGraphicsPipeline.CreateInfo.ImmutableSamplerDesc[variant.SamplerStates.Count];

            int i = 0;
            for (; i < variant.Parameters.Count; i++)
            {
                BindlessParameters param = variant.Parameters[i];

                ci.Resources[i] = new IGfxGraphicsPipeline.CreateInfo.ResourceDescriptor
                {
                    Name = param.ReferenceName,
                    Type = TranslateValueType(param.ValueType),
                    Slot = param.BindPoint,
                    Visibility = TranslateShaderType(param.StageBits)
                };
            }

            for (int j = 0; j < variant.ConstantBuffers.Count; j++, i++)
            {
                ConstantBufferParameter param = variant.ConstantBuffers[j];

                ci.Resources[i] = new IGfxGraphicsPipeline.CreateInfo.ResourceDescriptor
                {
                    Name = param.Name,
                    Type = param.IsConstant ? GfxDescriptorType.Constant : GfxDescriptorType.CBV,
                    Slot = param.BindPoint,
                    Count = param.Size / sizeof(uint),
                    Visibility = TranslateShaderType(param.StageBits)
                };
            }


            for (i = 0; i < variant.SamplerStates.Count; i++)
            {
                SamplerStateParameter param = variant.SamplerStates[i];

                ci.Samplers[i] = new IGfxGraphicsPipeline.CreateInfo.ImmutableSamplerDesc
                {
                    Slot = (byte)param.BindPoint,
                    Filter = param.Filter,
                    U = param.AddressU,
                    V = param.AddressV,
                    W = param.AddressW,
                };
            }
        }

        private GfxDescriptorType TranslateValueType(ShaderValueType vt)
        {
            switch (vt)
            {
                case ShaderValueType.Unknown: return GfxDescriptorType.SRV;
                case ShaderValueType.Texture1D: return GfxDescriptorType.SRV;
                case ShaderValueType.Texture1DArray: return GfxDescriptorType.SRV;
                case ShaderValueType.Texture2D: return GfxDescriptorType.SRV;
                case ShaderValueType.Texture2DArray: return GfxDescriptorType.SRV;
                case ShaderValueType.Texture3D: return GfxDescriptorType.SRV;
                case ShaderValueType.TextureCube: return GfxDescriptorType.SRV;
                case ShaderValueType.StructuredBuffer: return GfxDescriptorType.SRV;
                case ShaderValueType.ConstantBuffer: return GfxDescriptorType.CBV;
                case ShaderValueType.Constants: return GfxDescriptorType.Constant;
                default: return GfxDescriptorType.Unknown;
            }
        }

        private GfxShaderType TranslateShaderType(ShaderBitmask mask)
        {
            GfxShaderType type = 0;

            if (mask.HasFlag(ShaderBitmask.Vertex))
                type |= GfxShaderType.Vertex;
            if (mask.HasFlag(ShaderBitmask.Pixel))
                type |= GfxShaderType.Pixel;

            return type;
        }

        private class Storage : IDisposable
        {
            public List<KeyValuePair<ulong, IGfxGraphicsPipeline?>> PipelineState = new List<KeyValuePair<ulong, IGfxGraphicsPipeline?>>();
            public IShaderPackage.ReflectionData? Reflection = null;
            public IGfxPipelineStateCache? Cache = null;
            public IGfxDevice? Device = null;
            public IShaderPackage? Package = null;

            public void Dispose()
            {
                for (var i = 0; i < PipelineState.Count; i++)
                {
                    PipelineState[i].Value?.Dispose();
                }

                PipelineState.Clear();
            }
        }

        public struct ReflectionElement
        {
            public ShaderType Stages;
            public ElementType Type;
            public string Name;

            public enum ElementType : byte
            {
                Texture2D,
                Texture2DArray,
                Texture3D,
                TextureCube,
                ConstantBuffer,
                StructuredBuffer,
                UnorderedAccessBuffer,
                SamplerState
            }
        }

        public enum ShaderType
        {
            Vertex = 0b00000001,
            Pixel = 0b00000010
        }
    }
}
