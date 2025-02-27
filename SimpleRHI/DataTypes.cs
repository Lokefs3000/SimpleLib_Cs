namespace SimpleRHI
{
    public enum GfxBindFlags : byte
    {
        Unkown         = 0,
        VertexBuffer   = 1 << 0,
        IndexBuffer    = 1 << 1,
        ConstantBuffer = 1 << 2,
        ShaderResource = 1 << 3,
        DepthStencil   = 1 << 4,
        RenderTarget   = 1 << 5,
    }

    public enum GfxMemoryUsage : byte
    {
        Default   = 0,
        Dynamic   = 1 << 0,
        Staging   = 1 << 1,
        Immutable = 1 << 2,
    }

    public enum GfxCPUAccessFlags : byte
    {
        None = 0,
        Read,
        Write,
        ReadWrite = Read | Write
    }

    public enum GfxBufferMode : byte
    {
        None = 0,
        Structured,
        UnorderedAccess
    }

    //DXGI_FORMAT lol
    public enum GfxFormat : byte
    {
        Unkown = 0,
        R32G32B32A32_TYPELESS = 1,
        R32G32B32A32_FLOAT = 2,
        R32G32B32A32_UINT = 3,
        R32G32B32A32_SINT = 4,
        R32G32B32_TYPELESS = 5,
        R32G32B32_FLOAT = 6,
        R32G32B32_UINT = 7,
        R32G32B32_SINT = 8,
        R16G16B16A16_TYPELESS = 9,
        R16G16B16A16_FLOAT = 10,
        R16G16B16A16_UNORM = 11,
        R16G16B16A16_UINT = 12,
        R16G16B16A16_SNORM = 13,
        R16G16B16A16_SINT = 14,
        R32G32_TYPELESS = 15,
        R32G32_FLOAT = 16,
        R32G32_UINT = 17,
        R32G32_SINT = 18,
        R32G8X24_TYPELESS = 19,
        D32_FLOAT_S8X24_UINT = 20,
        R32_FLOAT_X8X24_TYPELESS = 21,
        X32_TYPELESS_G8X24_UINT = 22,
        R10G10B10A2_TYPELESS = 23,
        R10G10B10A2_UNORM = 24,
        R10G10B10A2_UINT = 25,
        R11G11B10_FLOAT = 26,
        R8G8B8A8_TYPELESS = 27,
        R8G8B8A8_UNORM = 28,
        R8G8B8A8_UNORM_SRGB = 29,
        R8G8B8A8_UINT = 30,
        R8G8B8A8_SNORM = 31,
        R8G8B8A8_SINT = 32,
        R16G16_TYPELESS = 33,
        R16G16_FLOAT = 34,
        R16G16_UNORM = 35,
        R16G16_UINT = 36,
        R16G16_SNORM = 37,
        R16G16_SINT = 38,
        R32_TYPELESS = 39,
        D32_FLOAT = 40,
        R32_FLOAT = 41,
        R32_UINT = 42,
        R32_SINT = 43,
        R24G8_TYPELESS = 44,
        D24_UNORM_S8_UINT = 45,
        R24_UNORM_X8_TYPELESS = 46,
        X24_TYPELESS_G8_UINT = 47,
        R8G8_TYPELESS = 48,
        R8G8_UNORM = 49,
        R8G8_UINT = 50,
        R8G8_SNORM = 51,
        R8G8_SINT = 52,
        R16_TYPELESS = 53,
        R16_FLOAT = 54,
        D16_UNORM = 55,
        R16_UNORM = 56,
        R16_UINT = 57,
        R16_SNORM = 58,
        R16_SINT = 59,
        R8_TYPELESS = 60,
        R8_UNORM = 61,
        R8_UINT = 62,
        R8_SNORM = 63,
        R8_SINT = 64,
        A8_UNORM = 65,
        R1_UNORM = 66,
        R9G9B9E5_SHAREDEXP = 67,
        R8G8_B8G8_UNORM = 68,
        G8R8_G8B8_UNORM = 69,
        BC1_TYPELESS = 70,
        BC1_UNORM = 71,
        BC1_UNORM_SRGB = 72,
        BC2_TYPELESS = 73,
        BC2_UNORM = 74,
        BC2_UNORM_SRGB = 75,
        BC3_TYPELESS = 76,
        BC3_UNORM = 77,
        BC3_UNORM_SRGB = 78,
        BC4_TYPELESS = 79,
        BC4_UNORM = 80,
        BC4_SNORM = 81,
        BC5_TYPELESS = 82,
        BC5_UNORM = 83,
        BC5_SNORM = 84,
        B5G6R5_UNORM = 85,
        B5G5R5A1_UNORM = 86,
        B8G8R8A8_UNORM = 87,
        B8G8R8X8_UNORM = 88,
        R10G10B10_XR_BIAS_A2_UNORM = 89,
        B8G8R8A8_TYPELESS = 90,
        B8G8R8A8_UNORM_SRGB = 91,
        B8G8R8X8_TYPELESS = 92,
        B8G8R8X8_UNORM_SRGB = 93,
        BC6H_TYPELESS = 94,
        BC6H_UF16 = 95,
        BC6H_SF16 = 96,
        BC7_TYPELESS = 97,
        BC7_UNORM = 98,
        BC7_UNORM_SRGB = 99,
    }

    public enum GfxBlend : byte
    {
        Zero = 0,
        One,
        SourceColor,
        InverseSourceColor,
        SourceAlpha,
        InverseSourceAlpha,
        DestinationAlpha,
        InverseDestinationAlpha,
        DestinationColor,
        InverseDestinationColor
    }

    public enum GfxBlendOperation : byte
    {
        Add = 0,
        Subtract,
        ReverseSubtract,
        Min,
        Max
    }

    public enum GfxLogicOperation : byte
    {
        Clear = 0,
        Set,
        Copy,
        CopyInverted,
        NoOp,
        Invert,
        And,
        Nand,
        Or,
        Nor,
        Xor,
        Equivalant,
        AndReverse,
        AndInverted,
        OrReverse,
        OrInverted
    }

    public enum GfxColorWriteEnable : byte
    {
        None = 0,
        Red = 1,
        Green = 2,
        Blue = 4,
        Alpha = 8,
        All = Red | Green | Blue | Alpha,
    }

    public enum GfxFillMode : byte
    {
        Solid = 0,
        Wireframe
    }

    public enum GfxCullMode : byte
    {
        None = 0,
        Back,
        Front,
    }

    public enum GfxDepthWriteMask : byte
    {
        Zero = 0,
        All
    }

    public enum GfxComparisonFunction : byte
    {
        None = 0,
        Never,
        Less,
        Equal,
        LessEqual,
        Greater,
        NotEqual,
        GreaterEqual,
        Always
    }

    public enum GfxStencilOperation : byte
    {
        Keep = 0,
        Zero,
        Replace,
        IncrementSat,
        DecrementSat,
        Invert,
        Increment,
        Decrement
    }

    public enum GfxPrimitiveTopologyType : byte
    {
        Undefined = 0,
        Point,
        Line,
        Triangle,
        Patch
    }

    public enum GfxValueType : byte
    {
        Unkown = 0,

        Single1,
        Single2,
        Single3,
        Single4,

        Double1,
        Double2,
        Double3,
        Double4,

        Int1,
        Int2,
        Int3,
        Int4,

        UInt1,
        UInt2,
        UInt3,
        UInt4,

        UInt1_Half,
        UInt2_Half,
        UInt3_Half,
        UInt4_Half,

        Half1,
        Half2,
        Half3,
        Half4
    }

    public enum GfxInputClassification : byte
    {
        PerVertex = 0,
        PerInstance
    }

    public enum GfxResult : byte
    {
        Success = 0,
        InvalidArgument,
        DriverVersionMismatch,
        AdapterNotFound,
        Unsupported
    }

    public enum GfxAPI : byte
    {
        Unkown = 0,
        Direct3D12
    }

    public enum GfxQueueType : byte
    {
        Graphics,
        Copy,
        Compute
    }

    public enum GfxShaderType : byte
    {
        None = 0,
        Vertex = 0b00000001,
        Pixel = 0b00000010,

        All = Vertex | Pixel,
    }

    public enum GfxTextureDimension : byte
    {
        Texture1D = 0,
        Texture2D,
        Texture3D,
        CubeMap,
    }

    public enum GfxMapType : byte
    {
        Read = 1,
        Write,
        ReadWrite = Read | Write,
    }

    public enum GfxMapFlags : byte
    {
        None = 0,
        Discard
    }

    public enum GfxDescriptorType : byte
    {
        Unknown = 0,
        SRV,
        UAV,
        CBV,
        Constant,
        Bindless
    }

    public enum GfxTextureViewType : byte
    {
        Unkown,
        ShaderResource,
        RenderTarget,
        DepthStencil
    }

    public enum GfxFilter : byte
    {
        MinMagMipPoint = 0,
        MinMagPointMipLinear,
        MinPointMagLinearMipPoint,
        MinPointMagMipLinear,
        MinLinearMagMipPoint,
        MinLinearMagPointMipLinear,
        MinMagLinearMipPoint,
        MinMagMipLinear,
        MinMagAnisotropicMipPoint,
        Anisotropic
    }

    public enum GfxWrap : byte
    {
        Wrap,
        Mirror,
        Clamp
    }

    public enum GfxPrimitiveTopology : byte
    {
        Undefined = 0,
        PointList = 1,
        LineList = 2,
        LineStrip = 3,
        TriangleList = 4,
        TriangleStrip = 5,
        TriangleFan = 6,
        LineListAdjacency = 10,
        LineStripAdjacency = 11,
        TriangleListAdjacency = 12,
        TriangleStripAdjacency = 13,
        PatchListWith1ControlPoints = 33,
        PatchListWith2ControlPoints = 34,
        PatchListWith3ControlPoints = 35,
        PatchListWith4ControlPoints = 36,
        PatchListWith5ControlPoints = 37,
        PatchListWith6ControlPoints = 38,
        PatchListWith7ControlPoints = 39,
        PatchListWith8ControlPoints = 40,
        PatchListWith9ControlPoints = 41,
        PatchListWith10ControlPoints = 42,
        PatchListWith11ControlPoints = 43,
        PatchListWith12ControlPoints = 44,
        PatchListWith13ControlPoints = 45,
        PatchListWith14ControlPoints = 46,
        PatchListWith15ControlPoints = 47,
        PatchListWith16ControlPoints = 48,
        PatchListWith17ControlPoints = 49,
        PatchListWith18ControlPoints = 50,
        PatchListWith19ControlPoints = 51,
        PatchListWith20ControlPoints = 52,
        PatchListWith21ControlPoints = 53,
        PatchListWith22ControlPoints = 54,
        PatchListWith23ControlPoints = 55,
        PatchListWith24ControlPoints = 56,
        PatchListWith25ControlPoints = 57,
        PatchListWith26ControlPoints = 58,
        PatchListWith27ControlPoints = 59,
        PatchListWith28ControlPoints = 60,
        PatchListWith29ControlPoints = 61,
        PatchListWith30ControlPoints = 62,
        PatchListWith31ControlPoints = 63,
        PatchListWith32ControlPoints = 64
    }
}
