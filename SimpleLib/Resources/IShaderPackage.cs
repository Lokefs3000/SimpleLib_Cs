using SimpleLib.Resources.Data;
using SimpleRHI;

namespace SimpleLib.Resources
{
    public interface IShaderPackage : IDisposable
    {
        public bool HasShader(ShaderType type, ulong id);
        public ReadOnlyMemory<byte> GetShaderVariant(ShaderType type, ulong id, ulong mask);
        public ReflectionData? LoadReflection(ulong id);

        public enum ShaderType : byte
        {
            Vertex,
            Pixel
        }

        //TODO: move out of IShaderPackge into global namespace
        //  i think VS can do this automaticly?
        public class ReflectionData
        {
            public Dictionary<ulong, Variant> VariantData = new Dictionary<ulong, Variant>();
            public List<VariantBitmask> Variants = new List<VariantBitmask>();

            public List<IGfxGraphicsPipeline.CreateInfo.RenderTargetBlendDesc> BlendDescriptions = new List<IGfxGraphicsPipeline.CreateInfo.RenderTargetBlendDesc>();

            public struct VariantMask
            {
                public ulong Mask;
                public string Define;
            }

            public struct Variant
            {
                public List<BindlessParameters> Parameters = new List<BindlessParameters>();
                public List<ConstantBufferParameter> ConstantBuffers = new List<ConstantBufferParameter>();
                public List<SamplerStateParameter> SamplerStates = new List<SamplerStateParameter>();
                public List<IGfxGraphicsPipeline.CreateInfo.InputElementDesc> InputElements = new List<IGfxGraphicsPipeline.CreateInfo.InputElementDesc>();

                public Variant()
                {
                }
            }
        }
    }

    public struct BindlessParameters
    {
        public string ReferenceName;
        public uint BindPoint;
        public ShaderValueType ValueType;
        public ShaderBitmask StageBits;

        public BindlessParameters(string referenceName, uint slot, ShaderValueType valueType, ShaderBitmask mask)
        {
            ReferenceName = referenceName;
            BindPoint = slot;
            ValueType = valueType;
            StageBits = mask;
        }
    }

    public struct ConstantBufferParameter
    {
        public string Name;
        public uint BindPoint;
        public uint Size;
        public bool IsConstant;
        public ShaderBitmask StageBits;

        public ConstantBufferParameter(string name, uint bindPoint, uint size, bool isConstant, ShaderBitmask mask)
        {
            Name = name;
            BindPoint = bindPoint;
            Size = size;
            IsConstant = isConstant;
            StageBits = mask;
        }
    }

    public readonly struct SamplerStateParameter
    {
        public readonly string Name;
        public readonly uint BindPoint;

        public readonly GfxFilter Filter;
        public readonly GfxWrap AddressU;
        public readonly GfxWrap AddressV;
        public readonly GfxWrap AddressW;

        public SamplerStateParameter(string name, uint bindPoint, GfxFilter filter, GfxWrap u, GfxWrap v, GfxWrap w)
        {
            Name = name;
            BindPoint = bindPoint;
            Filter = filter;
            AddressU = u;
            AddressV = v;
            AddressW = w;
        }
    }

    public readonly struct VariantBitmask
    {
        public readonly string Name;
        public readonly ulong Bit;

        public VariantBitmask(string name, ulong bit)
        {
            Name = name;
            Bit = bit;
        }
    }

    public enum ShaderValueType : byte
    {
        Unknown = 0,
        Texture1D,
        Texture1DArray,
        Texture2D,
        Texture2DArray,
        Texture3D,
        TextureCube,
        StructuredBuffer,
        ConstantBuffer,
        Constants
    }

    public enum ShaderBitmask : byte
    {
        None = 0,
        Vertex = 1 << 0,
        Pixel = 1 << 1,
        All = Vertex | Pixel,
    }
}
