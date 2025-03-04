namespace SimpleRHI
{
    public interface IGfxGraphicsPipeline : IDisposable
    {
        public CreateInfo Desc { get; }

        //change some of the contents to readonly structs? maybe some with record?
        public struct CreateInfo
        {
            public string Name;

            public ReadOnlyMemory<byte>? VertexShaderBytecode;
            public ReadOnlyMemory<byte>? PixelShaderBytecode;

            public RenderTargetBlendDesc[] Blend;
            public RasterizerDesc Rasterizer;
            public DepthStencilDesc DepthStencil;
            public InputElementDesc[] InputLayout;

            public GfxPrimitiveTopologyType PrimitiveTopology;

            public GfxFormat[] RTVFormats;
            public GfxFormat DSVFormat;

            public ResourceDescriptor[] Resources;
            public ImmutableSamplerDesc[] Samplers;

            public IGfxPipelineStateCache? PipelineStateCache;

            public CreateInfo()
            {
                Name = string.Empty;

                VertexShaderBytecode = null;
                PixelShaderBytecode = null;

                Blend = [];
                Rasterizer = new RasterizerDesc();
                DepthStencil = new DepthStencilDesc();
                InputLayout = [];

                PrimitiveTopology = GfxPrimitiveTopologyType.Triangle;

                RTVFormats = [];
                DSVFormat = GfxFormat.Unkown;

                PipelineStateCache = null;
            }

            public struct RenderTargetBlendDesc
            {
                public bool BlendEnabled;
                public bool LogicOperationEnabled;

                public GfxBlend SourceBlend;
                public GfxBlend DestinationBlend;
                public GfxBlendOperation BlendOperation;

                public GfxBlend SourceBlendAlpha;
                public GfxBlend DestinationBlendAlpha;
                public GfxBlendOperation BlendOperationAlpha;

                public GfxLogicOperation LogicOperation;

                public GfxColorWriteEnable RenderTargetWriteMask;

                public RenderTargetBlendDesc()
                {
                    BlendEnabled = false;
                    LogicOperationEnabled = false;
                    SourceBlend = GfxBlend.One;
                    DestinationBlend = GfxBlend.Zero;
                    BlendOperation = GfxBlendOperation.Add;
                    SourceBlendAlpha = GfxBlend.One;
                    DestinationBlendAlpha = GfxBlend.Zero;
                    BlendOperationAlpha = GfxBlendOperation.Add;
                    LogicOperation = GfxLogicOperation.Clear;
                    RenderTargetWriteMask = GfxColorWriteEnable.All;
                }
            }

            public struct RasterizerDesc
            {
                public GfxFillMode FillMode;
                public GfxCullMode CullMode;

                public RasterizerDesc()
                {
                    FillMode = GfxFillMode.Solid;
                    CullMode = GfxCullMode.Back;
                }
            }

            public struct DepthStencilDesc
            {
                public bool DepthEnable;

                public GfxDepthWriteMask DepthWriteMask;
                public GfxComparisonFunction DepthFunction;

                public bool StencilEnable;

                public byte StencilReadMask;
                public byte StencilWriteMask;

                public StencilOperationDesc FrontFace;
                public StencilOperationDesc BackFace;

                public DepthStencilDesc()
                {
                    DepthEnable = false;

                    DepthWriteMask = GfxDepthWriteMask.All;
                    DepthFunction = GfxComparisonFunction.Less;

                    StencilEnable = false;

                    StencilReadMask = 0xff;
                    StencilWriteMask = 0xff;

                    FrontFace = new StencilOperationDesc();
                    BackFace = new StencilOperationDesc();
                }

                public struct StencilOperationDesc
                {
                    public GfxStencilOperation StencilFailOp;
                    public GfxStencilOperation StencilDepthFailOp;
                    public GfxStencilOperation StencilPassOp;
                    public GfxComparisonFunction StencilFunction;

                    public StencilOperationDesc()
                    {
                        StencilFailOp = GfxStencilOperation.Keep;
                        StencilDepthFailOp = GfxStencilOperation.Keep;
                        StencilPassOp = GfxStencilOperation.Keep;
                        StencilFunction = GfxComparisonFunction.Always;
                    }
                }
            }

            public struct InputElementDesc
            {
                public string Name;
                public uint Index;

                public GfxValueType Type;

                public uint InputSlot;
                public uint ByteOffset;

                public GfxInputClassification InputClassification;
                public uint InstanceDataStepRate;

                public InputElementDesc()
                {
                    Name = string.Empty;
                    Index = 0;

                    Type = GfxValueType.Unkown;

                    InputSlot = 0;
                    ByteOffset = uint.MaxValue;

                    InputClassification = GfxInputClassification.PerVertex;
                    InstanceDataStepRate = 0;
                }
            }

            public struct ResourceDescriptor
            {
                public string Name; //UNIQUE IDENTIFIER FOR AUTO OPTIMIZE

                public GfxDescriptorType Type;

                public uint Slot;
                public uint Count; //32bit constant only

                public GfxShaderType Visibility;

                public ResourceDescriptor()
                {
                    Name = string.Empty;
                    Type = GfxDescriptorType.Unknown;
                    Slot = 0;
                    Count = 0;
                    Visibility = GfxShaderType.Vertex;
                }
            }

            public struct ImmutableSamplerDesc
            {
                public byte Slot;

                public GfxFilter Filter;
                public GfxWrap U;
                public GfxWrap V;
                public GfxWrap W;

                public ImmutableSamplerDesc()
                {
                    Slot = 0;

                    Filter = GfxFilter.MinMagMipLinear;
                    U = GfxWrap.Wrap;
                    V = GfxWrap.Wrap;
                    W = GfxWrap.Wrap;
                }
            }
        }
    }
}
