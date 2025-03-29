using SharpGen.Runtime;
using Vortice.Direct3D;
using Vortice.Direct3D12;
using Vortice.DXGI;

namespace SimpleRHI.D3D12
{
    internal class GfxGraphicsPipeline : IGfxGraphicsPipeline
    {
        public IGfxGraphicsPipeline.CreateInfo Desc => throw new NotImplementedException();
        private IGfxGraphicsPipeline.CreateInfo _desc;

        private ID3D12RootSignature _rootSignature;
        private ID3D12PipelineState _pipelineState;

        private BindlessParameter[] _params;
        private ushort _bindlessDescriptorCount;

        public GfxGraphicsPipeline(in IGfxGraphicsPipeline.CreateInfo ci, GfxDevice device)
        {
            _desc = ci;

            {
                RootSignatureDescription2 rootDescriptor = new RootSignatureDescription2(RootSignatureFlags.AllowInputAssemblerInputLayout | RootSignatureFlags.ConstantBufferViewShaderResourceViewUnorderedAccessViewHeapDirectlyIndexed);
                rootDescriptor.StaticSamplers = new StaticSamplerDescription1[ci.Samplers.Length];

                List<BindlessParameter> parameters = new List<BindlessParameter>();
                List<RootParameter1> rootDescriptors = new List<RootParameter1>();

                _bindlessDescriptorCount = 0;

                ushort rootTableOffset = 0;

                for (int i = 0; i < ci.Resources.Length; i++)
                {
                    IGfxGraphicsPipeline.CreateInfo.ResourceDescriptor descriptor = ci.Resources[i];
                    if (descriptor.Type == GfxDescriptorType.SRV || descriptor.Type == GfxDescriptorType.UAV)
                    {
                        rootTableOffset = 1;
                        break;
                    }
                }

                for (int i = 0; i < ci.Resources.Length; i++)
                {
                    IGfxGraphicsPipeline.CreateInfo.ResourceDescriptor descriptor = ci.Resources[i];
                    if (descriptor.Type == GfxDescriptorType.SRV || descriptor.Type == GfxDescriptorType.UAV)
                    {
                        parameters.Add(new BindlessParameter
                        {
                            Slot = (byte)descriptor.Slot,
                            Offset = _bindlessDescriptorCount++,
                            Type = TranslateDescriptorType(descriptor.Type)
                        });
                    }
                    else
                    {
                        switch (descriptor.Type)
                        {
                            case GfxDescriptorType.CBV:
                                {
                                    parameters.Add(new BindlessParameter
                                    {
                                        Slot = (byte)descriptor.Slot,
                                        Offset = rootTableOffset++,
                                        Type = BindlessParameter.DescriptorType.CBV
                                    });
                                    rootDescriptors.Add(new RootParameter1(RootParameterType.ConstantBufferView, new RootDescriptor1(descriptor.Slot, 0, RootDescriptorFlags.DataVolatile), TranslateShaderVisibility(descriptor.Visibility)));
                                    break;
                                }
                            case GfxDescriptorType.Constant:
                                {
                                    parameters.Add(new BindlessParameter
                                    {
                                        Slot = (byte)descriptor.Slot,
                                        Offset = rootTableOffset++,
                                        Size = (ushort)descriptor.Count,
                                        Type = BindlessParameter.DescriptorType.Constants
                                    });
                                    rootDescriptors.Add(new RootParameter1(new RootConstants(descriptor.Slot, 0, descriptor.Count), TranslateShaderVisibility(descriptor.Visibility)));
                                    break;
                                }
                            default: break;
                        }
                    }
                }

                for (int i = 0; i < rootDescriptor.StaticSamplers.Length; i++)
                {
                    IGfxGraphicsPipeline.CreateInfo.ImmutableSamplerDesc samplerDesc = ci.Samplers[i];
                    rootDescriptor.StaticSamplers[i] = new StaticSamplerDescription1
                    {
                        Filter = FormatConverter.Translate(samplerDesc.Filter),
                        AddressU = FormatConverter.Translate(samplerDesc.U),
                        AddressV = FormatConverter.Translate(samplerDesc.V),
                        AddressW = FormatConverter.Translate(samplerDesc.W),
                        ShaderRegister = samplerDesc.Slot,
                        ShaderVisibility = ShaderVisibility.All
                    };
                }

                if (_bindlessDescriptorCount > 0)
                {
                    rootDescriptors.Insert(0, new RootParameter1(new RootConstants(8, 0, _bindlessDescriptorCount), ShaderVisibility.All));
                }

                rootDescriptor.Parameters = rootDescriptors.ToArray();
                _params = parameters.ToArray();

                VersionedRootSignatureDescription versioned = new VersionedRootSignatureDescription(rootDescriptor);
                string err = Vortice.Direct3D12.D3D12.D3D12SerializeVersionedRootSignature(versioned, out Blob blob);

                if (err.Length > 0)
                {
                    GfxDevice.Logger?.Error("Failed to serialize root signature!");
                    throw new Exception(err);
                }

                if (blob.BufferSize == 0 || blob.BufferPointer == nint.Zero)
                {
                    GfxDevice.Logger?.Error("No data in serialized root signature!");
                    blob.Dispose();
                    throw new Exception();
                }

                try
                {
                    _rootSignature = device.D3D12Device.CreateRootSignature(blob);
                }
                catch (Exception)
                {
                    GfxDevice.Logger?.Error("Failed to create root signature!");
                    throw;
                }

                blob.Dispose();
            }

            BlendDescription blend = new BlendDescription();
            {
                Span<RenderTargetBlendDescription> buffer = blend.RenderTarget.AsSpan();
                for (int i = 0; i < Math.Min(ci.Blend.Length, 8); i++)
                {
                    ref RenderTargetBlendDescription blendDesc = ref buffer[i];
                    IGfxGraphicsPipeline.CreateInfo.RenderTargetBlendDesc desc = ci.Blend[i];

                    blendDesc.BlendEnable = desc.BlendEnabled;
                    blendDesc.LogicOpEnable = desc.LogicOperationEnabled;

                    blendDesc.SourceBlend = FormatConverter.Translate(desc.SourceBlend);
                    blendDesc.DestinationBlend = FormatConverter.Translate(desc.DestinationBlend);
                    blendDesc.BlendOperation = FormatConverter.Translate(desc.BlendOperation);

                    blendDesc.SourceBlendAlpha = FormatConverter.Translate(desc.SourceBlendAlpha);
                    blendDesc.DestinationBlendAlpha = FormatConverter.Translate(desc.DestinationBlendAlpha);
                    blendDesc.BlendOperationAlpha = FormatConverter.Translate(desc.BlendOperationAlpha);

                    blendDesc.LogicOp = FormatConverter.Translate(desc.LogicOperation);

                    blendDesc.RenderTargetWriteMask = FormatConverter.Translate(desc.RenderTargetWriteMask);
                }
            }

            RasterizerDescription rasterizer = new RasterizerDescription();
            {
                IGfxGraphicsPipeline.CreateInfo.RasterizerDesc desc = ci.Rasterizer;
                rasterizer.FillMode = FormatConverter.Translate(desc.FillMode);
                rasterizer.CullMode = FormatConverter.Translate(desc.CullMode);
                rasterizer.FrontCounterClockwise = false;

                rasterizer.DepthBias = 0;
                rasterizer.DepthBiasClamp = 0.0f;
                rasterizer.SlopeScaledDepthBias = 0.0f;
                rasterizer.DepthClipEnable = true;
                rasterizer.MultisampleEnable = false;
                rasterizer.AntialiasedLineEnable = false;
                rasterizer.ForcedSampleCount = 0;
                rasterizer.ConservativeRaster = ConservativeRasterizationMode.Off;
            }

            DepthStencilDescription depthStencil = new DepthStencilDescription();
            {
                IGfxGraphicsPipeline.CreateInfo.DepthStencilDesc desc = ci.DepthStencil;
                depthStencil.DepthEnable = desc.DepthEnable;

                depthStencil.DepthWriteMask = (DepthWriteMask)desc.DepthWriteMask;
                depthStencil.DepthFunc = FormatConverter.Translate(desc.DepthFunction);

                depthStencil.StencilEnable = desc.StencilEnable;

                depthStencil.StencilReadMask = desc.StencilReadMask;
                depthStencil.StencilWriteMask = desc.StencilWriteMask;

                depthStencil.FrontFace = new DepthStencilOperationDescription()
                {
                    StencilFailOp = FormatConverter.Translate(desc.FrontFace.StencilFailOp),
                    StencilDepthFailOp = FormatConverter.Translate(desc.FrontFace.StencilDepthFailOp),
                    StencilPassOp = FormatConverter.Translate(desc.FrontFace.StencilPassOp),
                    StencilFunc = FormatConverter.Translate(desc.FrontFace.StencilFunction),
                };

                depthStencil.BackFace = new DepthStencilOperationDescription()
                {
                    StencilFailOp = FormatConverter.Translate(desc.BackFace.StencilFailOp),
                    StencilDepthFailOp = FormatConverter.Translate(desc.BackFace.StencilDepthFailOp),
                    StencilPassOp = FormatConverter.Translate(desc.BackFace.StencilPassOp),
                    StencilFunc = FormatConverter.Translate(desc.BackFace.StencilFunction),
                };
            }

            InputLayoutDescription inputLayout = new InputLayoutDescription();
            {
                inputLayout.Elements = new InputElementDescription[ci.InputLayout.Length];

                Span<InputElementDescription> elements = new Span<InputElementDescription>(inputLayout.Elements);
                for (int i = 0; i < ci.InputLayout.Length; i++)
                {
                    ref InputElementDescription elem = ref elements[i];
                    IGfxGraphicsPipeline.CreateInfo.InputElementDesc desc = ci.InputLayout[i];

                    elem.SemanticName = desc.Name;
                    elem.SemanticIndex = desc.Index;

                    elem.Format = FormatConverter.Translate(desc.Type);
                    elem.Slot = desc.InputSlot;
                    elem.AlignedByteOffset = desc.ByteOffset;

                    elem.Classification = FormatConverter.Translate(desc.InputClassification);
                    elem.InstanceDataStepRate = desc.InstanceDataStepRate;
                }
            }

            GraphicsPipelineStateDescription pipelineStateDescription = new GraphicsPipelineStateDescription();
            pipelineStateDescription.RootSignature = _rootSignature;
            pipelineStateDescription.VertexShader = ci.VertexShaderBytecode.GetValueOrDefault();
            pipelineStateDescription.PixelShader = ci.PixelShaderBytecode.GetValueOrDefault();
            pipelineStateDescription.BlendState = blend;
            pipelineStateDescription.RasterizerState = rasterizer;
            pipelineStateDescription.DepthStencilState = depthStencil;
            pipelineStateDescription.InputLayout = inputLayout;
            pipelineStateDescription.PrimitiveTopologyType = FormatConverter.Translate(ci.PrimitiveTopology);
            pipelineStateDescription.RenderTargetFormats = new Format[Math.Min(ci.RTVFormats.Length, 8)];
            pipelineStateDescription.DepthStencilFormat = FormatConverter.Translate(ci.DSVFormat);
            for (int i = 0; i < ci.RTVFormats.Length; i++)
            {
                pipelineStateDescription.RenderTargetFormats[i] = FormatConverter.Translate(ci.RTVFormats[i]);
            }

            bool didLoadFromCache = false;
            if (ci.PipelineStateCache != null)
            {
                GfxPipelineStateCache stateCache = (GfxPipelineStateCache)ci.PipelineStateCache;
                _pipelineState = stateCache.LoadGraphics(ci.Name, pipelineStateDescription);

                if (_pipelineState != null)
                    didLoadFromCache = true;
            }

            if (_pipelineState == null)
            {
                Result r = device.D3D12Device.CreateGraphicsPipelineState(pipelineStateDescription, out _pipelineState);
                if (r.Failure || _pipelineState == null)
                {
                    GfxDevice.Logger?.Error("Failed to create graphics pipeline state!");

                    _rootSignature.Dispose();

                    device.DumpInfoLog();
                    throw new Exception(r.Code.ToString());
                }
            }

            if (!didLoadFromCache && ci.PipelineStateCache != null)
            {
                GfxPipelineStateCache stateCache = (GfxPipelineStateCache)ci.PipelineStateCache;
                stateCache.Store(ci.Name, _pipelineState);
            }

            _pipelineState.Name = ci.Name;
        }

        public void Dispose()
        {
            _pipelineState.Dispose();
            _rootSignature.Dispose();
        }

        private BindlessParameter.DescriptorType TranslateDescriptorType(GfxDescriptorType type)
        {
            switch (type)
            {
                case GfxDescriptorType.Unknown: return BindlessParameter.DescriptorType.Unknown;
                case GfxDescriptorType.SRV: return BindlessParameter.DescriptorType.SRV;
                case GfxDescriptorType.UAV: return BindlessParameter.DescriptorType.UAV;
                case GfxDescriptorType.CBV: return BindlessParameter.DescriptorType.CBV;
                case GfxDescriptorType.Constant: return BindlessParameter.DescriptorType.Constants;
                default: return BindlessParameter.DescriptorType.Unknown;
            }
        }

        private ShaderVisibility TranslateShaderVisibility(GfxShaderType visibility)
        {
            ShaderVisibility r = ShaderVisibility.All;

            /*if (visibility.HasFlag(GfxShaderType.Vertex))
                r |= ShaderVisibility.Vertex;
            if (visibility.HasFlag(GfxShaderType.Pixel))
                r |= ShaderVisibility.Pixel;*/

            return r;
        }

        public ID3D12RootSignature D3D12RootSignature => _rootSignature;
        public ID3D12PipelineState D3D12PipelineState => _pipelineState;

        //bindless parameters are always located at index 0 !!!!
        public ReadOnlySpan<BindlessParameter> BindlessParameters => _params.AsSpan();
        public ushort BindlessDescriptorValueCount => _bindlessDescriptorCount;

        private class CompararerSlot : IComparer<IGfxGraphicsPipeline.CreateInfo.ResourceDescriptor>
        {
            public static readonly CompararerSlot Instance = new CompararerSlot();

            public int Compare(IGfxGraphicsPipeline.CreateInfo.ResourceDescriptor x, IGfxGraphicsPipeline.CreateInfo.ResourceDescriptor y)
            {
                return (int)(x.Slot - y.Slot);
            }
        }

        private class CompararerType : IComparer<IGfxGraphicsPipeline.CreateInfo.ResourceDescriptor>
        {
            public static readonly CompararerType Instance = new CompararerType();

            public int Compare(IGfxGraphicsPipeline.CreateInfo.ResourceDescriptor x, IGfxGraphicsPipeline.CreateInfo.ResourceDescriptor y)
            {
                return (int)(x.Type - y.Type);
            }
        }

        public struct BindlessParameter
        {
            public byte Slot;
            public ushort Offset;
            public ushort Size;
            public DescriptorType Type;

            public enum DescriptorType : byte
            {
                Unknown = 0,
                SRV,
                CBV,
                UAV,
                Constants
            }
        }
    }
}
