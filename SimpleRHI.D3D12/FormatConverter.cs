using Vortice.Direct3D12;
using Vortice.DXGI;

namespace SimpleRHI.D3D12
{
    internal static class FormatConverter
    {
        public static Blend Translate(GfxBlend blend)
        {
            switch (blend)
            {
                case GfxBlend.Zero: return Blend.Zero;
                case GfxBlend.One: return Blend.One;
                case GfxBlend.SourceColor: return Blend.SourceColor;
                case GfxBlend.InverseSourceColor: return Blend.InverseSourceColor;
                case GfxBlend.SourceAlpha: return Blend.SourceAlpha;
                case GfxBlend.InverseSourceAlpha: return Blend.InverseSourceAlpha;
                case GfxBlend.DestinationAlpha: return Blend.DestinationAlpha;
                case GfxBlend.InverseDestinationAlpha: return Blend.InverseDestinationAlpha;
                case GfxBlend.DestinationColor: return Blend.DestinationColor;
                case GfxBlend.InverseDestinationColor: return Blend.InverseDestinationColor;
                default: throw new ArgumentException("Unkown blend value specified for translation!", "blend");
            }
        }

        public static BlendOperation Translate(GfxBlendOperation op)
        {
            switch (op)
            {
                case GfxBlendOperation.Add: return BlendOperation.Add;
                case GfxBlendOperation.Subtract: return BlendOperation.Subtract;
                case GfxBlendOperation.ReverseSubtract: return BlendOperation.RevSubtract;
                case GfxBlendOperation.Min: return BlendOperation.Min;
                case GfxBlendOperation.Max: return BlendOperation.Max;
                default: throw new ArgumentException("Unkown blend operation value specified for translation!", "op");
            }
        }

        public static LogicOp Translate(GfxLogicOperation op)
        {
            switch (op)
            {
                case GfxLogicOperation.Clear: return LogicOp.Clear;
                case GfxLogicOperation.Set: return LogicOp.Set;
                case GfxLogicOperation.Copy: return LogicOp.Copy;
                case GfxLogicOperation.CopyInverted: return LogicOp.CopyInverted;
                case GfxLogicOperation.NoOp: return LogicOp.Noop;
                case GfxLogicOperation.Invert: return LogicOp.Invert;
                case GfxLogicOperation.And: return LogicOp.And;
                case GfxLogicOperation.Nand: return LogicOp.Nand;
                case GfxLogicOperation.Or: return LogicOp.Or;
                case GfxLogicOperation.Nor: return LogicOp.Nor;
                case GfxLogicOperation.Xor: return LogicOp.Xor;
                case GfxLogicOperation.Equivalant: return LogicOp.Equiv;
                case GfxLogicOperation.AndReverse: return LogicOp.AndReverse;
                case GfxLogicOperation.AndInverted: return LogicOp.AndInverted;
                case GfxLogicOperation.OrReverse: return LogicOp.OrReverse;
                case GfxLogicOperation.OrInverted: return LogicOp.OrInverted;
                default: throw new ArgumentException("Unkown logic operation value specified for translation!", "op");
            }
        }

        public static ColorWriteEnable Translate(GfxColorWriteEnable colorWriteEnable)
        {
            if (colorWriteEnable == GfxColorWriteEnable.All)
                return ColorWriteEnable.All;
            return (ColorWriteEnable)colorWriteEnable;
        }

        public static FillMode Translate(GfxFillMode fillMode)
        {
            switch (fillMode)
            {
                case GfxFillMode.Solid: return FillMode.Solid;
                case GfxFillMode.Wireframe: return FillMode.Wireframe;
                default: throw new ArgumentException("Unkown fill mode value specified for translation!", "fillMode");
            }
        }

        public static CullMode Translate(GfxCullMode cullMode)
        {
            switch (cullMode)
            {
                case GfxCullMode.None: return CullMode.None;
                case GfxCullMode.Front: return CullMode.Front;
                case GfxCullMode.Back: return CullMode.Back;
                default: throw new ArgumentException("Unkown cull mode value specified for translation!", "cullMode");
            }
        }

        public static ComparisonFunction Translate(GfxComparisonFunction comparisonFunction)
        {
            switch (comparisonFunction)
            {
                case GfxComparisonFunction.None: return ComparisonFunction.None;
                case GfxComparisonFunction.Never: return ComparisonFunction.Never;
                case GfxComparisonFunction.Less: return ComparisonFunction.Less;
                case GfxComparisonFunction.Equal: return ComparisonFunction.Equal;
                case GfxComparisonFunction.LessEqual: return ComparisonFunction.LessEqual;
                case GfxComparisonFunction.Greater: return ComparisonFunction.Greater;
                case GfxComparisonFunction.NotEqual: return ComparisonFunction.NotEqual;
                case GfxComparisonFunction.GreaterEqual: return ComparisonFunction.GreaterEqual;
                case GfxComparisonFunction.Always: return ComparisonFunction.Always;
                default: throw new ArgumentException("Unkown comparison function value specified for translation!", "comparisonFunction");
            }
        }

        public static StencilOperation Translate(GfxStencilOperation stencilOperation)
        {
            switch (stencilOperation)
            {
                case GfxStencilOperation.Keep: return StencilOperation.Keep;
                case GfxStencilOperation.Zero: return StencilOperation.Zero;
                case GfxStencilOperation.Replace: return StencilOperation.Replace;
                case GfxStencilOperation.IncrementSat: return StencilOperation.IncrementSaturate;
                case GfxStencilOperation.DecrementSat: return StencilOperation.DecrementSaturate;
                case GfxStencilOperation.Invert: return StencilOperation.Invert;
                case GfxStencilOperation.Increment: return StencilOperation.Increment;
                case GfxStencilOperation.Decrement: return StencilOperation.Decrement;
                default: throw new ArgumentException("Unkown stencil operation value specified for translation!", "stencilOperation");
            }
        }

        public static Format Translate(GfxValueType valueType)
        {
            switch (valueType)
            {
                case GfxValueType.Unkown: return Format.Unknown;
                case GfxValueType.Single1: return Format.R32_Float;
                case GfxValueType.Single2: return Format.R32G32_Float;
                case GfxValueType.Single3: return Format.R32G32B32_Float;
                case GfxValueType.Single4: return Format.R32G32B32A32_Float;
                case GfxValueType.Int1: return Format.R32_SInt;
                case GfxValueType.Int2: return Format.R32G32_SInt;
                case GfxValueType.Int3: return Format.R32G32B32_SInt;
                case GfxValueType.Int4: return Format.R32G32B32A32_SInt;
                case GfxValueType.UInt1: return Format.R32_UInt;
                case GfxValueType.UInt2: return Format.R32G32_UInt;
                case GfxValueType.UInt3: return Format.R32G32B32_UInt;
                case GfxValueType.UInt4: return Format.R32G32B32A32_UInt;
                default: throw new ArgumentException("Unkown value type value specified for translation!", "valueType");
            }
        }

        public static InputClassification Translate(GfxInputClassification inputClassification)
        {
            switch (inputClassification)
            {
                case GfxInputClassification.PerVertex: return InputClassification.PerVertexData;
                case GfxInputClassification.PerInstance: return InputClassification.PerInstanceData;
                default: throw new ArgumentException("Unkown input classification value specified for translation!", "inputClassification");
            }
        }

        public static PrimitiveTopologyType Translate(GfxPrimitiveTopologyType primitiveTopologyType)
        {
            switch (primitiveTopologyType)
            {
                case GfxPrimitiveTopologyType.Undefined: return PrimitiveTopologyType.Undefined;
                case GfxPrimitiveTopologyType.Point: return PrimitiveTopologyType.Point;
                case GfxPrimitiveTopologyType.Line: return PrimitiveTopologyType.Line;
                case GfxPrimitiveTopologyType.Triangle: return PrimitiveTopologyType.Triangle;
                case GfxPrimitiveTopologyType.Patch: return PrimitiveTopologyType.Patch;
                default: throw new ArgumentException("Unkown primitive topology type value specified for translation!", "primitiveTopologyType");
            }
        }

        public static Format Translate(GfxFormat format)
        {
            return (Format)format;
        }

        public static ShaderVisibility Translate(GfxShaderType shaderType)
        {
            ShaderVisibility visibility = 0;

            if (shaderType.HasFlag(GfxShaderType.Vertex))
                visibility |= ShaderVisibility.Vertex;
            if (shaderType.HasFlag(GfxShaderType.Pixel))
                visibility |= ShaderVisibility.Pixel;

            return visibility;
        }

        public static DescriptorRangeType Translate(GfxDescriptorType descriptorType)
        {
            switch (descriptorType)
            {
                case GfxDescriptorType.SRV: return DescriptorRangeType.ShaderResourceView;
                case GfxDescriptorType.UAV: return DescriptorRangeType.UnorderedAccessView;
                case GfxDescriptorType.CBV: return DescriptorRangeType.ConstantBufferView;
                default: throw new ArgumentException("Unkown descriptor type value specified for translation!", "descriptorType");
            }
        }

        public static CommandListType Translate(GfxQueueType queueType)
        {
            switch (queueType)
            {
                case GfxQueueType.Graphics: return CommandListType.Direct;
                case GfxQueueType.Copy: return CommandListType.Copy;
                case GfxQueueType.Compute: return CommandListType.Compute;
                default: throw new ArgumentException("Unkown queue type value specified for translation!", "queueType");
            }
        }

        public static Filter Translate(GfxFilter filter)
        {
            switch (filter)
            {
                case GfxFilter.MinMagMipPoint: return Filter.MinMagMipPoint;
                case GfxFilter.MinMagPointMipLinear: return Filter.MinMagPointMipLinear;
                case GfxFilter.MinPointMagLinearMipPoint: return Filter.MinPointMagLinearMipPoint;
                case GfxFilter.MinPointMagMipLinear: return Filter.MinPointMagMipLinear;
                case GfxFilter.MinLinearMagMipPoint: return Filter.MinLinearMagMipPoint;
                case GfxFilter.MinLinearMagPointMipLinear: return Filter.MinLinearMagPointMipLinear;
                case GfxFilter.MinMagLinearMipPoint: return Filter.MinMagLinearMipPoint;
                case GfxFilter.MinMagMipLinear: return Filter.MinMagMipLinear;
                case GfxFilter.MinMagAnisotropicMipPoint: return Filter.MinMagAnisotropicMipPoint;
                case GfxFilter.Anisotropic: return Filter.Anisotropic;
                default: return Filter.MinMagMipPoint;
            }
        }

        public static TextureAddressMode Translate(GfxWrap wrap)
        {
            switch (wrap)
            {
                case GfxWrap.Wrap: return TextureAddressMode.Wrap;
                case GfxWrap.Mirror: return TextureAddressMode.Wrap;
                case GfxWrap.Clamp: return TextureAddressMode.Clamp;
                default: return TextureAddressMode.Wrap;
            }
        }

        public static ResourceStates AsResourceState(GfxBindFlags bind)
        {
            ResourceStates res = ResourceStates.Common;
            if (bind == GfxBindFlags.VertexBuffer || bind == GfxBindFlags.ConstantBuffer)
                res = ResourceStates.VertexAndConstantBuffer;
            else if (bind == GfxBindFlags.IndexBuffer)
                res = ResourceStates.IndexBuffer;
            return res;
        }
    }
}
