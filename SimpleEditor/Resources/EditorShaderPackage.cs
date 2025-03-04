using CommunityToolkit.HighPerformance;
using HelloDirect3D12;
using SharpGen.Runtime;
using SimpleEditor.Files;
using SimpleLib.Files;
using SimpleLib.Resources;
using SimpleLib.Resources.Data;
using SimpleRHI;
using System.Buffers;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Text;
using System.Xml.Linq;
using Vortice.Direct3D;
using Vortice.Direct3D12.Shader;
using Vortice.Dxc;

namespace SimpleEditor.Resources
{
    public class EditorShaderPackage : IShaderPackage
    {
        private Filesystem _filesystem;
        private ProjectFileSystem _projectFs;

        private Dictionary<ulong, ShaderSourceData> _shaders = new Dictionary<ulong, ShaderSourceData>();

        public EditorShaderPackage(Filesystem filesystem, ProjectFileSystem projectFs)
        {
            _filesystem = filesystem;
            _projectFs = projectFs;
        }

        public void Dispose()
        {

        }

        public void InvalidateShader(ulong id)
        {
            throw new NotImplementedException();
        }

        public bool HasShader(IShaderPackage.ShaderType type, ulong id)
        {
            if (_shaders.TryGetValue(id, out ShaderSourceData? sourceData))
            {
                return sourceData.ShaderMask.HasFlag(TranslateShaderIndex((int)type));
            }

            return false;
        }

        public ReadOnlyMemory<byte> GetShaderVariant(IShaderPackage.ShaderType type, ulong id, ulong mask)
        {
            ShaderSourceData sourceData = GetSourceData(id);
            ShaderVariantData variantData = CompileVariant(sourceData, id, mask);
            return variantData.Bytecode[(int)type];
        }

        public IShaderPackage.ReflectionData? LoadReflection(ulong id)
        {
            if (_shaders.TryGetValue(id, out ShaderSourceData? sourceData))
            {
                if (sourceData.CachedReflectionData != null)
                {
                    if (sourceData.CachedReflectionData.Variants.Count != sourceData.Variants.Count ||
                        sourceData.CachedReflectionData.VariantData.Count != sourceData.VariantData.Count)
                    {
                        sourceData.CachedReflectionData.Variants.Clear();
                        sourceData.CachedReflectionData.VariantData.Clear();

                        sourceData.CachedReflectionData.Variants = sourceData.Variants;
                        foreach (var kvp in sourceData.VariantData)
                        {
                            sourceData.CachedReflectionData.VariantData.Add(kvp.Key, new IShaderPackage.ReflectionData.Variant
                            {
                                Parameters = kvp.Value.Parameters,
                                ConstantBuffers = kvp.Value.ConstantBuffers,
                                SamplerStates = kvp.Value.SamplerStates,
                                InputElements = kvp.Value.InputElements,
                            });
                        }

                        sourceData.CachedReflectionData.BlendDescriptions = sourceData.BlendDescriptions;
                    }

                    return sourceData.CachedReflectionData;
                }

                IShaderPackage.ReflectionData data = new IShaderPackage.ReflectionData();
                data.Variants = sourceData.Variants;

                data.BlendDescriptions = sourceData.BlendDescriptions;

                foreach (var kvp in sourceData.VariantData)
                {
                    data.VariantData.Add(kvp.Key, new IShaderPackage.ReflectionData.Variant
                    {
                        Parameters = kvp.Value.Parameters,
                        ConstantBuffers = kvp.Value.ConstantBuffers,
                        SamplerStates = kvp.Value.SamplerStates,
                        InputElements = kvp.Value.InputElements,
                    });
                }

                sourceData.CachedReflectionData = data;
                return data;
            }

            return null;
        }

        private ShaderVariantData CompileVariant(ShaderSourceData sourceData, ulong id, ulong variant)
        {
            if (sourceData.VariantData.TryGetValue(variant, out ShaderVariantData? data))
            {
                return data;
            }

            data = new ShaderVariantData();
            data.Variant = variant;

            using IDxcCompiler3 compiler = Dxc.CreateDxcCompiler<IDxcCompiler3>();
            using IDxcUtils utils = Dxc.CreateDxcUtils();

            //crashes when using the same one twice!
            using IDxcIncludeHandler includeHandler1 = new ShaderIncludeHandler("Engine/", "Engine/Shaders/", _projectFs.RootPath, _projectFs.Exists(id) ? _projectFs.GetFullPath(id) ?? string.Empty : string.Empty);
            using IDxcIncludeHandler includeHandler2 = new ShaderIncludeHandler("Engine/", "Engine/Shaders/", _projectFs.RootPath, _projectFs.Exists(id) ? _projectFs.GetFullPath(id) ?? string.Empty : string.Empty);

            string[] arguments = [
                sourceData.LocalPath,

                "-O3",
                "-Zpr",
                "-HV", "2021",
                "-WX",

#if DEBUG
                "-Zi",
                "-Qembed_debug",
#else
                "-Qstrip_debug",
                "-Qstrip_reflect",
#endif

                "-D", "USE_BINDLESS_DESCRIPTORS"
                ];

            int count = BitOperations.PopCount(variant);
            if (count > 0)
            {
                Queue<string> enabled = new Queue<string>();
                for (int i = 0; i < sourceData.Variants.Count; i++)
                {
                    if ((sourceData.Variants[i].Bit & variant) > 0)
                    {
                        enabled.Enqueue(sourceData.Variants[i].Name + "_ENABLED");
                    }
                }

                string[] args = new string[count * 2];
                for (int i = 0; i < args.Length; i += 2)
                {
                    args[i] = "-D";
                    args[i] = enabled.Dequeue();
                }
            }

            ReadOnlyMemory<byte> vs = CompileShader(arguments.Concat(["-T", "vs_6_6", "-E", "VertexMain"]), sourceData.ProcessedText, sourceData.LocalPath, compiler, includeHandler1, out ReadOnlyMemory<byte> vs_refl);
            ReadOnlyMemory<byte> ps = CompileShader(arguments.Concat(["-T", "ps_6_6", "-E", "PixelMain"]), sourceData.ProcessedText, sourceData.LocalPath, compiler, includeHandler2, out ReadOnlyMemory<byte> ps_relf);

            data.Bytecode = [vs, ps];

            ProcessShaderReflection(utils, sourceData, data, sourceData.LocalPath, vs_refl, ps_relf);

            sourceData.VariantData.Add(variant, data);
            return data;
        }

        private void ProcessShaderReflection(IDxcUtils utils, ShaderSourceData sourceData, ShaderVariantData variantData, string shaderName, params ReadOnlyMemory<byte>[] reflections)
        {
            Dictionary<string, ValueTuple<uint, ShaderBitmask>> constantBufferSizeDict = new Dictionary<string, ValueTuple<uint, ShaderBitmask>>();

            Span<DefinedVariable> variables = CollectionsMarshal.AsSpan(sourceData.Variables);
            Span<DefinedConstantBuffer> constantBuffers = CollectionsMarshal.AsSpan(sourceData.ConstantBuffers);
            Span<DefinedSamplerState> samplerStates = CollectionsMarshal.AsSpan(sourceData.SamplerStates);

            List<string> refCb = new List<string>();
            for (int i = 0; i < reflections.Length; i++)
            {
                ReadOnlyMemory<byte> reflectionData = reflections[i];
                ID3D12ShaderReflection reflection;

                unsafe
                {
                    using MemoryHandle pin = reflectionData.Pin();
                    using IDxcBlob blob = utils.CreateBlob((nint)pin.Pointer, (uint)reflectionData.Length, 0);
                    reflection = utils.CreateReflection<ID3D12ShaderReflection>(blob);
                }

                if ((IShaderPackage.ShaderType)i == IShaderPackage.ShaderType.Vertex)
                {
                    Span<ShaderParameterDescription> inputs = reflection.InputParameters;
                    for (int j = 0; j < inputs.Length; j++)
                    {
                        ref ShaderParameterDescription input = ref inputs[j];
                        if (input.SystemValueType == SystemValueType.Undefined)
                        {
                            variantData.InputElements.Add(new IGfxGraphicsPipeline.CreateInfo.InputElementDesc
                            {
                                Name = input.SemanticName,
                                Index = input.SemanticIndex,
                                Type = ConvertInputType(ref input),
                            });
                        }
                    }
                }
                
                /*Span<InputBindingDescription> bindings = reflection.BoundResources;
                for (int j = 0; j < bindings.Length; j++)
                {
                    ref InputBindingDescription bind = ref bindings[j];
                    if (positions.ContainsKey(bind.Name))
                    {
                        bool valid = false;
                        for (int k = 0; k < variables.Length; k++)
                        {
                            ref DefinedVariable variable = ref variables[k];
                            if (variable.Name == bind.Name)
                            {
                                if (variable.VariantMask == ulong.MaxValue || (variable.VariantMask & variantData.Variant) > 0)
                                {
                                    valid = true;
                                    break;
                                }
                            }
                        }

                        if (valid)
                        {
                            ref BindlessParameters param = ref parameters[positions[bind.Name]];
                            param.StageBits |= TranslateShaderIndex(i);
                        }
                    }
                    else if (constantBuffers.ContainsKey(bind.Name))
                    {
                        ref ConstantBufferParameter param = ref CollectionsMarshal.GetValueRefOrNullRef(constantBuffers, bind.Name);
                        param.StageBits |= TranslateShaderIndex(i);
                    }
                    else
                    {
                        if (bind.Type == ShaderInputType.ConstantBuffer)
                        {
                            constantBuffers.Add(bind.Name, new ConstantBufferParameter(bind.Name, bind.BindPoint, 0, TranslateShaderIndex(i)));
                        }
                        else
                        {
                            bool valid = false;
                            for (int k = 0; k < variables.Length; k++)
                            {
                                ref DefinedVariable variable = ref variables[k];
                                if (variable.Name == bind.Name)
                                {
                                    if (variable.VariantMask == ulong.MaxValue || (variable.VariantMask & variantData.Variant) > 0)
                                    {
                                        valid = true;
                                        break;
                                    }
                                }
                            }

                            if (valid)
                            {
                                variantData.Parameters.Add(new BindlessParameters(bind.Name, bind.BindPoint, TranslateValueType(ref bind), TranslateShaderIndex(i)));
                                parameters = variantData.Parameters.AsSpan();

                                positions.Add(bind.Name, variantData.Parameters.Count - 1);
                            }
                        }
                    }
                }*/

                ReadOnlySpan<ID3D12ShaderReflectionConstantBuffer> constantBufferDatas = reflection.ConstantBuffers;
                for (int j = 0; j < constantBufferDatas.Length; j++)
                {
                    using ID3D12ShaderReflectionConstantBuffer data = constantBufferDatas[j];
                    ConstantBufferDescription description = data.Description;

                    if (constantBufferSizeDict.TryGetValue(description.Name, out ValueTuple<uint, ShaderBitmask> value))
                    {
                        constantBufferSizeDict[description.Name] = new ValueTuple<uint, ShaderBitmask>(description.Size, value.Item2 | TranslateShaderIndex(i));
                    }
                    else
                    {
                        constantBufferSizeDict.Add(description.Name, new ValueTuple<uint, ShaderBitmask>(description.Size, TranslateShaderIndex(i)));
                    }
                }

                reflection.Dispose();
            }

            for (int i = 0; i < variables.Length; i++)
            {
                ref DefinedVariable variable = ref variables[i];
                if (variable.VariantMask == ulong.MaxValue || variable.VariantMask == variantData.Variant || (variable.VariantMask & variantData.Variant) > 0)
                {
                    variantData.Parameters.Add(new BindlessParameters(variable.Name, variable.RegisterSlot, TranslateValueType(variable.Type), ShaderBitmask.All));
                }
            }

            for (int i = 0; i < constantBuffers.Length; i++)
            {
                ref DefinedConstantBuffer constantBuffer = ref constantBuffers[i];
                if (constantBuffer.VariantMask == ulong.MaxValue || constantBuffer.VariantMask == variantData.Variant || (constantBuffer.VariantMask & variantData.Variant) > 0)
                {
                    if (constantBufferSizeDict.TryGetValue(constantBuffer.Name, out var size))
                    {
                        variantData.ConstantBuffers.Add(new ConstantBufferParameter(constantBuffer.Name, constantBuffer.RegisterSlot, size.Item1, constantBuffer.IsConstant, size.Item2));
                    }
                }
            }

            for (int i = 0; i < samplerStates.Length; i++)
            {
                ref DefinedSamplerState samplerState = ref samplerStates[i];
                if (samplerState.VariantMask == ulong.MaxValue || samplerState.VariantMask == variantData.Variant || (samplerState.VariantMask & variantData.Variant) > 0)
                {
                    variantData.SamplerStates.Add(new SamplerStateParameter(samplerState.Name, samplerState.RegisterSlot, samplerState.Filter, samplerState.AddressU, samplerState.AddressV, samplerState.AddressW));
                }
            }
        }

        private GfxValueType ConvertInputType(ref ShaderParameterDescription desc)
        {
            switch (desc.ComponentType)
            {
                case RegisterComponentType.UInt32: return GfxValueType.UInt1 + (byte)Math.Min(BitOperations.PopCount((uint)desc.UsageMask), 4) - 1;
                case RegisterComponentType.Float32: return GfxValueType.Single1 + (byte)Math.Min(BitOperations.PopCount((uint)desc.UsageMask), 4) - 1;
                case RegisterComponentType.UInt16: return GfxValueType.UInt1_Half + (byte)Math.Min(BitOperations.PopCount((uint)desc.UsageMask), 4) - 1;
                case RegisterComponentType.Float16: return GfxValueType.Half1 + (byte)Math.Min(BitOperations.PopCount((uint)desc.UsageMask), 4) - 1;
                case RegisterComponentType.Float64: return GfxValueType.Double1 + (byte)Math.Min(BitOperations.PopCount((uint)desc.UsageMask), 4) - 1;
                default: return GfxValueType.Unkown;
            }
        }

        private ShaderValueType TranslateValueType(ref InputBindingDescription bind)
        {
            switch (bind.Type)
            {
                case ShaderInputType.ConstantBuffer: return ShaderValueType.ConstantBuffer;
                case ShaderInputType.TextureBuffer:
                case ShaderInputType.Texture:
                    {
                        switch (bind.Dimension)
                        {
                            case ShaderResourceViewDimension.Texture1D: return ShaderValueType.Texture1D;
                            case ShaderResourceViewDimension.Texture1DArray: return ShaderValueType.Texture1DArray;
                            case ShaderResourceViewDimension.Texture2D: return ShaderValueType.Texture2D;
                            case ShaderResourceViewDimension.Texture2DArray: return ShaderValueType.Texture2DArray;
                            case ShaderResourceViewDimension.Texture3D: return ShaderValueType.Texture3D;
                            case ShaderResourceViewDimension.TextureCube: return ShaderValueType.TextureCube;
                            default: break;
                        }
                        return ShaderValueType.Unknown;
                    }
                case ShaderInputType.Structured: return ShaderValueType.StructuredBuffer;
                default: return ShaderValueType.Unknown;
            }
        }

        private ShaderValueType TranslateValueType(string bind)
        {
            if (bind.Contains('<'))
            {
                bind = bind.Substring(0, bind.IndexOf('<'));
            }

            return Enum.Parse<ShaderValueType>(bind);
        }

        private ShaderBitmask TranslateShaderIndex(int i)
        {
            switch ((IShaderPackage.ShaderType)i)
            {
                case IShaderPackage.ShaderType.Vertex: return ShaderBitmask.Vertex;
                case IShaderPackage.ShaderType.Pixel: return ShaderBitmask.Pixel;
                default: return ShaderBitmask.None;
            }
        }

        private ReadOnlyMemory<byte> CompileShader(IEnumerable<string> arguments, string sourceText, string sourceName, IDxcCompiler3 compiler, IDxcIncludeHandler includeHandler, out ReadOnlyMemory<byte> reflection)
        {
            IDxcResult? result = null;

            try
            {
                Result r = compiler.Compile(sourceText, arguments.ToArray(), includeHandler, out result);
                {
                    using IDxcBlobEncoding? encoding = result?.GetErrorBuffer();
                    IDxcBlob? blob = (IDxcBlob?)encoding;

                    if (blob != null)
                    {
                        ReadOnlySpan<byte> ptr = blob.AsSpan();
                        
                        if (ptr.Length > 0)
                        {
                            string str = Encoding.UTF8.GetString(ptr);
                            LogTypes.Resources.Error("Compilation failed for shader: \"{b}\"! Errors:\n{a}", sourceName, str);
                            throw new Exception();
                        }
                    }
                }

                r.CheckError();
                if (result == null)
                    throw new NullReferenceException();

                using IDxcBlob refl = result.GetOutput(DxcOutKind.Reflection);
                using IDxcBlob bytecode = result.GetResult();

                reflection = refl.AsMemory();
                return bytecode.AsMemory();
            }
            catch (Exception ex)
            {
                LogTypes.Resources.Error(ex, "Compilation failed for shader: \"{b}\"!", sourceName);
            }
            finally
            {
                result?.Dispose();
            }

            reflection = null;
            return null;
        }

        private ShaderSourceData GetSourceData(ulong id)
        {
            if (_shaders.TryGetValue(id, out ShaderSourceData? sourceData))
            {
                return sourceData;
            }

            sourceData = new ShaderSourceData();
            sourceData.SourceText = _filesystem.ReadText(id) ?? string.Empty;

            if (_projectFs.Exists(id))
            {
                sourceData.LocalPath = _projectFs.GetLocalPath(id) ?? $"Unresolved-{id}";
            }
            else
            {
                sourceData.LocalPath = $"Unscoped-{id}";
            }

            ProcessShaderSource(sourceData, id);

            if (sourceData.ProcessedText.Contains("VertexMain"))
                sourceData.ShaderMask |= ShaderBitmask.Vertex;
            if (sourceData.ProcessedText.Contains("PixelMain"))
                sourceData.ShaderMask |= ShaderBitmask.Pixel;

            _shaders.Add(id, sourceData);
            return sourceData;
        }

        private void ProcessShaderSource(ShaderSourceData data, ulong id)
        {
            /*StringBuilder builder = new StringBuilder();

            string localSource = data.SourceText;

            //resolve includes
            LinkedList<string> includes = new LinkedList<string>();
            includes.AddFirst(data.LocalPath);
            List<string> searchDirs = new List<string>();
            searchDirs.Add(_projectFs.Exists(id) ? Path.GetDirectoryName(_projectFs.GetFullPath(id)) ?? "Engine/" : "Engine/");
            searchDirs.Add("Engine/Shaders/");
            searchDirs.Add(string.Empty);
            ResolveIncludeFor(ref localSource, includes, searchDirs, data);



            builder.ToString();*/

            using ShaderIncludeHandler includeHandler = new ShaderIncludeHandler("Engine/", "Engine/Shaders/", _projectFs.RootPath, _projectFs.Exists(id) ? _projectFs.GetFullPath(id) ?? string.Empty : string.Empty);

            int index = 0;
            while ((index = data.SourceText.IndexOf("#define", index)) > -1)
            {
                int strFirst = index++ + 8;
                int strLast = data.SourceText.IndexOf('\n', strFirst + 1);

                //TODO: check for length so we dont substring a short string and get an empty string

                string name = data.SourceText.Substring(strFirst, strLast - strFirst).Trim();
                if (name.StartsWith("VARIANT_"))
                {
                    //TODO: collision checks
                    //TODO: warn about overflow
                    //TODO: check for spaces? test if thats an issue
                    string variant = name.Substring(8);
                    data.Variants.Add(new VariantBitmask(variant, 1ul << data.Variants.Count));
                }
            }

            string processed = data.SourceText;

            index = 0;
            while ((index = processed.IndexOf("#include", index)) > -1)
            {
                int strFirst = index++ + 8;
                int strLast = processed.IndexOf('\n', strFirst + 1);

                string name = processed.Substring(strFirst, strLast - strFirst).Trim('"', ' ', '\r', '\n');
                Result r = includeHandler.LoadSource(name, out IDxcBlob? include);
                try
                {
                    if (!r.Failure && include != null)
                    {
                        string source = Encoding.UTF8.GetString(include.AsSpan());
                        source = source.Substring(0, source.Length - 1);

                        processed = processed.Replace($"#include \"{name}\"", source);

                        index -= 4;
                    }
                    else
                    {
                        LogTypes.Resources.Error("Failed to resolve include: \"{a}\"! While compiling: \"{b}\"..", name, data.LocalPath);
                    }
                }
                catch (Exception ex)
                {
                    LogTypes.Resources.Error(ex, "Error occured while compiling: \"{a}\"..", data.LocalPath);
                }

                include?.Dispose();
            }

            ulong mask = 0;
            Stack<MaskStackElement> maskStack = new Stack<MaskStackElement>();

            List<DefinedVariable> variables = new List<DefinedVariable>();
            List<DefinedConstantBuffer> constantBuffers = new List<DefinedConstantBuffer>();
            List<DefinedSamplerState> samplerStates = new List<DefinedSamplerState>();

            bool? isConstant = null;

            int current = 0;
            while (current < processed.Length)
            {
                int start = current;
                int real = current;

                if (char.IsLetter(processed[current]))
                {
                    current++;
                    while (current < processed.Length && char.IsLetterOrDigit(processed[current]))
                    {
                        current++;
                    }

                    string slice = processed.Substring(start, current - start);
                    if (slice == "StructuredBuffer" || slice == "Texture1D" || slice == "Texture1DArray" || slice == "Texture2D" || slice == "Texture2DArray" || slice == "Texture3D" || slice == "TextureCube")
                    {
                        if (!char.IsWhiteSpace(processed[++current]))
                        {
                            while (current < processed.Length && !char.IsWhiteSpace(processed[current]) && (char.IsLetterOrDigit(processed[current]) || processed[current] == '<' || processed[current] == '>'))
                            {
                                current++;
                            }
                        }

                        string variableType = processed.Substring(start, current - start);

                        start = ++current;

                        while (current < processed.Length && (char.IsLetterOrDigit(processed[current]) || processed[current] == '_'))
                        {
                            current++;
                        }

                        string variableName = processed.Substring(start, current - start);

                        while (current < processed.Length && processed[current] != '(')
                        {
                            current++;
                        }

                        start = ++current;

                        while (current < processed.Length && char.IsLetterOrDigit(processed[current]))
                        {
                            current++;
                        }

                        string register = processed.Substring(start, current++ - start);

                        variables.Add(new DefinedVariable(variableType, variableName, register, maskStack.Count > 0 ? mask : ulong.MaxValue));

                        string redefintion = $"#define {variableName} (({variableType})ResourceDescriptorHeap[Ref_{variableName}])";

                        processed = processed.Remove(real, current - real);
                        processed = processed.Insert(real, redefintion);

                        current = real;
                    }
                    else if (slice == "cbuffer")
                    {
                        start = ++current;

                        while (current < processed.Length && (char.IsLetterOrDigit(processed[current]) || processed[current] == '_'))
                        {
                            current++;
                        }

                        string variableName = processed.Substring(start, current - start);

                        while (current < processed.Length && processed[current] != '(')
                        {
                            current++;
                        }

                        start = ++current;

                        while (current < processed.Length && char.IsLetterOrDigit(processed[current]))
                        {
                            current++;
                        }

                        string register = processed.Substring(start, current - start);

                        constantBuffers.Add(new DefinedConstantBuffer(variableName, register, maskStack.Count > 0 ? mask : ulong.MaxValue, isConstant.GetValueOrDefault(false)));

                        isConstant = null;
                    }
                    else if (slice == "SamplerState")
                    {
                        start = ++current;

                        while (current < processed.Length && (char.IsLetterOrDigit(processed[current]) || processed[current] == '_'))
                        {
                            current++;
                        }

                        string variableName = processed.Substring(start, current - start);

                        while (current < processed.Length && processed[current] != '(')
                        {
                            current++;
                        }

                        start = ++current;

                        while (current < processed.Length && char.IsLetterOrDigit(processed[current]))
                        {
                            current++;
                        }

                        string register = processed.Substring(start, current - start);

                        start = ++current;

                        while (current < processed.Length && processed[current] != '{')
                        {
                            current++;
                        }

                        start = current++;

                        //yeah not the most GC friendly way, ik
                        Dictionary<string, object> properties = new Dictionary<string, object>();

                        int dict_start = start;
                        while (current < processed.Length && processed[current] != '}')
                        {
                            if (char.IsWhiteSpace(processed[current]))
                            {
                                current++;
                                continue;
                            }

                            start = current;

                            while (current < processed.Length && char.IsLetterOrDigit(processed[current]))
                            {
                                current++;
                            }

                            string propertyName = processed.Substring(start, current - start);

                            while (current < processed.Length && (char.IsWhiteSpace(processed[current]) || processed[current] == '='))
                            {
                                current++;
                            }

                            start = current;

                            while (current < processed.Length && (char.IsLetterOrDigit(processed[current]) || processed[current] == '_'))
                            {
                                current++;
                            }

                            string propertyValue = processed.Substring(start, current - start);

                            current++;

                            switch (propertyName)
                            {
                                case "Filter":
                                    {
                                        if (!Enum.TryParse(propertyValue, false, out GfxFilter filter))
                                        {
                                            LogTypes.Resources.Warning("Unkown sampler state filter value: \"{a}\"!", propertyValue);
                                            break;
                                        }

                                        if (!properties.TryAdd(propertyName, filter))
                                        {
                                            LogTypes.Resources.Warning("Sampler state property already defined: \"{a}\"!", propertyName);
                                            break;
                                        }

                                        break;
                                    }
                                case "AddressU":
                                case "AddressV":
                                case "AddressW":
                                    {
                                        if (!Enum.TryParse(propertyValue, false, out GfxWrap wrap))
                                        {
                                            LogTypes.Resources.Warning("Unkown sampler state address value: \"{a}\"!", propertyValue);
                                            break;
                                        }

                                        if (!properties.TryAdd(propertyName, wrap))
                                        {
                                            LogTypes.Resources.Warning("Sampler state property already defined: \"{a}\"!", propertyName);
                                            break;
                                        }

                                        break;
                                    }
                                default:
                                    {
                                        LogTypes.Resources.Warning("Unknown sampler state property: \"{a}\"!", propertyName);
                                        break;
                                    }
                            }
                        }

                        current++;

                        properties.TryAdd("Filter", GfxFilter.MinMagMipLinear);
                        properties.TryAdd("AddressU", GfxWrap.Wrap);
                        properties.TryAdd("AddressV", GfxWrap.Wrap);
                        properties.TryAdd("AddressW", GfxWrap.Wrap);

                        samplerStates.Add(new DefinedSamplerState(variableName, register, mask, (GfxFilter)properties["Filter"], (GfxWrap)properties["AddressU"], (GfxWrap)properties["AddressV"], (GfxWrap)properties["AddressW"]));

                        processed = processed.Remove(dict_start, current - dict_start);
                        current = dict_start;
                    }
                    /*else if (slice == "PsInput")
                    {
                        start = ++current;

                        while (current < processed.Length && (char.IsLetterOrDigit(processed[current]) || processed[current] == '_'))
                        {
                            current++;
                        }

                        string functionName = processed.Substring(start, current - start);

                        if (functionName == "VertexMain")
                        {
                            while (current < processed.Length && processed[current] != '{')
                            {
                                current++;
                            }

                            current++;


                        }
                    }*/
                }
                else if (processed[current] == '#')
                {
                    current++;

                    start = current;

                    while (current < processed.Length && char.IsLetter(processed[current]))
                    {
                        current++;
                    }

                    string directive = processed.Substring(start, current - start);
                    if (directive == "if")
                    {
                        continue;
                    }

                    string maskName = string.Empty;

                    if (directive != "else" && directive != "endif")
                    {
                        while (current < processed.Length && char.IsWhiteSpace(processed[current]))
                        {
                            current++;
                        }

                        start = current++;

                        while (current < processed.Length && (char.IsLetterOrDigit(processed[current]) || processed[current] == '_'))
                        {
                            current++;
                        }

                        if (current >= processed.Length)
                        {
                            break;
                        }

                        maskName = processed.Substring(start, current - start);
                    }

                    int last = maskName.LastIndexOf('_');
                    if (last == -1)
                        last = maskName.Length;

                    if (directive == "ifndef")
                    {
                        ulong bit = FindVariantBit(data, maskName.Substring(0, last));
                        if (bit == 0)
                        {
                            goto ProcessShaderSourceEnd;
                        }

                        maskStack.Push(new MaskStackElement(bit, true));
                        mask &= ~bit;
                    }
                    else if (directive == "else")
                    {
                        if (maskStack.Count == 0)
                        {
                            goto ProcessShaderSourceEnd;
                        }

                        MaskStackElement element = maskStack.Pop();
                        if (element.Inverted)
                            mask |= element.Mask;
                        else
                            mask &= ~element.Mask;

                        maskStack.Push(new MaskStackElement(element.Mask, !element.Inverted));
                    }
                    else if (directive == "elif")
                    {
                        if (maskStack.Count == 0)
                        {
                            goto ProcessShaderSourceEnd;
                        }

                        ulong bit = FindVariantBit(data, maskName.Substring(0, last));
                        if (bit == 0)
                        {
                            goto ProcessShaderSourceEnd;
                        }

                        MaskStackElement element = maskStack.Pop();
                        if (element.Inverted)
                            mask |= element.Mask;
                        else
                            mask &= ~element.Mask;

                        maskStack.Push(new MaskStackElement(bit, false));
                        mask |= bit;
                    }
                    else if (directive == "ifdef")
                    {
                        ulong bit = FindVariantBit(data, maskName.Substring(0, last));
                        if (bit == 0)
                        {
                            goto ProcessShaderSourceEnd;
                        }

                        maskStack.Push(new MaskStackElement(bit, false));
                        mask |= bit;
                    }
                    else if (directive == "endif")
                    {
                        if (maskStack.Count == 0)
                        {
                            goto ProcessShaderSourceEnd;
                        }

                        MaskStackElement element = maskStack.Pop();
                        if (element.Inverted)
                            mask |= element.Mask;
                        else
                            mask &= ~element.Mask;

                        current--;
                    }
                    else if (directive == "define")
                    {
                        if (maskName.StartsWith("BLEND_RT"))
                        {
                            int blendRTIndex = int.Parse(maskName.Substring(8));
                            if (data.BlendDescriptions.Count <= blendRTIndex)
                            {
                                while (data.BlendDescriptions.Count <= blendRTIndex)
                                {
                                    data.BlendDescriptions.Add(new IGfxGraphicsPipeline.CreateInfo.RenderTargetBlendDesc());
                                }
                            }

                            string value = processed.Substring(current, processed.IndexOf('\n', current) - current).Trim().Trim('"');

                            var rtbd = new IGfxGraphicsPipeline.CreateInfo.RenderTargetBlendDesc();
                            rtbd.BlendEnabled = true;

                            //actual god whoever answered this
                            //https://www.gamedev.net/forums/topic/596801-d3d11-alpha-transparency-trouble/
                            if (value == "Premultiplied")
                            {
                                rtbd.SourceBlend = GfxBlend.One;
                                rtbd.DestinationBlend = GfxBlend.InverseSourceAlpha;
                                rtbd.BlendOperation = GfxBlendOperation.Add;

                                rtbd.SourceBlendAlpha = GfxBlend.One;
                                rtbd.DestinationBlendAlpha = GfxBlend.InverseSourceAlpha;
                                rtbd.BlendOperationAlpha = GfxBlendOperation.Add;
                            }
                            else if (value == "Additive")
                            {
                                rtbd.SourceBlend = GfxBlend.One;
                                rtbd.DestinationBlend = GfxBlend.One;
                                rtbd.BlendOperation = GfxBlendOperation.Add;

                                rtbd.SourceBlendAlpha = GfxBlend.One;
                                rtbd.DestinationBlendAlpha = GfxBlend.One;
                                rtbd.BlendOperationAlpha = GfxBlendOperation.Add;
                            }
                            else if (value == "Subtractive")
                            {
                                rtbd.SourceBlend = GfxBlend.One;
                                rtbd.DestinationBlend = GfxBlend.One;
                                rtbd.BlendOperation = GfxBlendOperation.ReverseSubtract;

                                rtbd.SourceBlendAlpha = GfxBlend.One;
                                rtbd.DestinationBlendAlpha = GfxBlend.One;
                                rtbd.BlendOperationAlpha = GfxBlendOperation.ReverseSubtract;
                            }
                            else if (value == "StraightAlpha")
                            {
                                rtbd.SourceBlend = GfxBlend.SourceAlpha;
                                rtbd.DestinationBlend = GfxBlend.InverseSourceAlpha;
                                rtbd.BlendOperation = GfxBlendOperation.Add;

                                rtbd.SourceBlendAlpha = GfxBlend.SourceAlpha;
                                rtbd.DestinationBlendAlpha = GfxBlend.InverseSourceAlpha;
                                rtbd.BlendOperationAlpha = GfxBlendOperation.Add;
                            }
                            else if (value == "Opaque")
                            {
                                rtbd.BlendEnabled = false;
                            }
                            else
                            {
                                string[] keys = value.Split(',');
                                for (int i = 0; i < keys.Length; i++)
                                {
                                    string[] segments = keys[i].Split('=');
                                    if (segments.Length == 2)
                                    {
                                        switch (segments[0])
                                        {
                                            case "Enabled": rtbd.BlendEnabled = bool.Parse(segments[1]); break;
                                            case "LogicOpEnabled": rtbd.LogicOperationEnabled = bool.Parse(segments[1]); break;
                                            case "SrcBlend": rtbd.SourceBlend = Enum.Parse<GfxBlend>(segments[1]); break;
                                            case "DestBlend": rtbd.DestinationBlend = Enum.Parse<GfxBlend>(segments[1]); break;
                                            case "BlendOp": rtbd.BlendOperation = Enum.Parse<GfxBlendOperation>(segments[1]); break;
                                            case "SrcBlendAlpha": rtbd.SourceBlendAlpha = Enum.Parse<GfxBlend>(segments[1]); break;
                                            case "DestBlendAlpha": rtbd.DestinationBlendAlpha = Enum.Parse<GfxBlend>(segments[1]); break;
                                            case "BlendOpAlpha": rtbd.BlendOperationAlpha = Enum.Parse<GfxBlendOperation>(segments[1]); break;
                                            case "LogicOp": rtbd.LogicOperation = Enum.Parse<GfxLogicOperation>(segments[1]); break;
                                            case "RTWrite": rtbd.RenderTargetWriteMask = Enum.Parse<GfxColorWriteEnable>(segments[1]); break;
                                            default: break;
                                        }
                                    }
                                }
                            }

                            data.BlendDescriptions[blendRTIndex] = rtbd;
                        }
                    }
                }
                else if (processed[current] == '[')
                {
                    start = ++current;

                    while (current < processed.Length && char.IsLetter(processed[current]))
                    {
                        current++;
                    }

                    string attribute = processed.Substring(start, current - start);
                    if (attribute == "ConstantBufferBehavior" && processed[current] == '(')
                    {
                        if (isConstant.HasValue)
                        {
                            int nextFind = processed.IndexOf('\n', current);
                            if (nextFind == -1)
                                nextFind = processed.Length;

                            LogTypes.Resources.Information("Attribute already specified on constant buffer: \"{}\"! While compiling shader: \"{}\"..", processed.Substring(real, nextFind - real), data.LocalPath);
                        }

                        start = ++current;
                        while (current < processed.Length && char.IsLetterOrDigit(processed[current]))
                        {
                            current++;
                        }

                        if (current < processed.Length && processed[current] != ')')
                        {
                            int nextFind = processed.IndexOf('\n', current);
                            if (nextFind == -1)
                                nextFind = processed.Length;

                            LogTypes.Resources.Information("Invalid syntax in constant buffer attribute: \"{}\"! While compiling shader: \"{}\"..", processed.Substring(real, nextFind - real), data.LocalPath);
                            
                            goto ProcessShaderSourceEnd;
                        }

                        if (current < processed.Length && processed[current + 1] != ']')
                        {
                            int nextFind = processed.IndexOf('\n', current);
                            if (nextFind == -1)
                                nextFind = processed.Length;

                            LogTypes.Resources.Information("Invalid syntax in constant buffer attribute: \"{}\"! While compiling shader: \"{}\"..", processed.Substring(real, nextFind - real), data.LocalPath);
                            
                            goto ProcessShaderSourceEnd;
                        }

                        string option = processed.Substring(start, current - start);

                        if (option == "Constants")
                        {
                            isConstant = true;
                        }
                        else if (option == "CBV")
                        {
                            isConstant = false;
                        }

                        processed = processed.Remove(real, current - real + 2);
                        current = real;
                    }
                    else
                    {
                        int nextFind = processed.IndexOf('\n', current);
                        if (nextFind == -1)
                            nextFind = processed.Length;

                        LogTypes.Resources.Information("Unkown or invalid attribute specified in shader: \"{}\"! While compiling shader: \"{}\"..", processed.Substring(real, nextFind - real), data.LocalPath);
                    }
                }

            ProcessShaderSourceEnd:
                current = processed.IndexOf('\n', current);
                if (current == -1)
                {
                    break;
                }

                current++;
            }

            Span<DefinedVariable> variablesSpan = CollectionsMarshal.AsSpan(variables);
            Span<VariantBitmask> bitmasks = CollectionsMarshal.AsSpan(data.Variants);

            StringBuilder sb = new StringBuilder();
            sb.AppendLine("cbuffer GlobalRefCB : register(b8)");
            sb.AppendLine("{");

            for (int i = 0; i < variablesSpan.Length; i++)
            {
                ref DefinedVariable variable = ref variablesSpan[i];

                int count = 0;
                for (int j = 0; j < bitmasks.Length; j++)
                {
                    ref VariantBitmask bitmask = ref bitmasks[j];

                    if (variable.VariantMask != ulong.MaxValue)
                    {
                        sb.Append((variable.VariantMask & bitmask.Bit) > 0 ? "#ifdef " : "#ifndef ");
                        sb.Append(bitmask.Name);
                        sb.AppendLine("_ENABLED");

                        count++;
                    }
                }

                sb.Append("    uint Ref_");
                sb.Append(variable.Name);
                sb.Append(';');
                sb.AppendLine();

                for (int j = 0; j < count; j++)
                {
                    sb.AppendLine("#endif");
                }
            }

            sb.AppendLine("}");
            sb.AppendLine();

            data.ProcessedText = sb.ToString() + processed;

            data.Variables = variables;
            data.ConstantBuffers = constantBuffers;
            data.SamplerStates = samplerStates;
        }

        private ulong FindVariantBit(ShaderSourceData sourceData, string maskName)
        {
            Span<VariantBitmask> bitmasks = CollectionsMarshal.AsSpan(sourceData.Variants);
            for (int i = 0; i < sourceData.Variants.Count; i++)
            {
                ref VariantBitmask bitmask = ref bitmasks[i];
                if (bitmask.Name == maskName)
                {
                    return bitmask.Bit;
                }
            }

            return 0;
        }

        private void ResolveIncludeFor(ref string source, LinkedList<string> includes, List<string> searchDirs, ShaderSourceData data)
        {
            int index = 0;
            while ((index = source.IndexOf("#include", index)) > -1)
            {
                int strFirst = source.IndexOf('"', index);
                int strLast = source.IndexOf('"', ++strFirst);
                if (strFirst > -1 && strLast > -1)
                {
                    string slice = source.Substring(strFirst, strLast - strFirst);
                    if (includes.Contains(slice))
                    {
                        LogTypes.Resources.Error("Infinite include cycle encountered with include: \"{a}\"!", slice);
                        throw new Exception();
                    }

                    string? read = LocateIncludeIntention(slice, searchDirs);
                    if (read != null)
                    {
                        ResolveIncludeFor(ref read, includes, searchDirs, data);

                        int nextLine = Math.Min(source.IndexOf('\n', strLast), source.Length);
                        if (nextLine == -1)
                            nextLine = source.Length;

                        source = source.Remove(index, nextLine - index);
                        source = source.Insert(index, read);
                    }
                    else
                    {
                        LogTypes.Resources.Warning("Failed to locate include: \"{a}\"!", slice);
                        throw new Exception();
                    }
                }
                else
                {
                    LogTypes.Resources.Error("Failed to resolve include for: \"{a}\"!", includes.First?.Value);
                    throw new Exception();
                }
            }
        }

        private string? LocateIncludeIntention(string slice, List<string> searchDirs)
        {
            for (int i = 0; i < searchDirs.Count; i++)
            {
                string full = Path.Combine(searchDirs[i], slice);
                if (File.Exists(full))
                {
                    string? parent = Path.GetDirectoryName(full);
                    if (parent != null && parent != searchDirs[i])
                        searchDirs.Add(parent);

                    return File.ReadAllText(full);
                }
            }

            return null;
        }

        private class ShaderSourceData
        {
            public string LocalPath = string.Empty;

            public string SourceText = string.Empty;
            public string ProcessedText = string.Empty;

            public ShaderBitmask ShaderMask = ShaderBitmask.None;

            public IShaderPackage.ReflectionData? CachedReflectionData = null;

            public Dictionary<ulong, ShaderVariantData> VariantData = new Dictionary<ulong, ShaderVariantData>();
            public List<VariantBitmask> Variants = new List<VariantBitmask>();

            public List<DefinedVariable> Variables = new List<DefinedVariable>();
            public List<DefinedConstantBuffer> ConstantBuffers = new List<DefinedConstantBuffer>();
            public List<DefinedSamplerState> SamplerStates = new List<DefinedSamplerState>();

            public List<IGfxGraphicsPipeline.CreateInfo.RenderTargetBlendDesc> BlendDescriptions = new List<IGfxGraphicsPipeline.CreateInfo.RenderTargetBlendDesc>();
        }

        private class ShaderVariantData
        {
            public ulong Variant;

            public List<BindlessParameters> Parameters = new List<BindlessParameters>();
            public List<ConstantBufferParameter> ConstantBuffers = new List<ConstantBufferParameter>();
            public List<SamplerStateParameter> SamplerStates = new List<SamplerStateParameter>();
            public List<IGfxGraphicsPipeline.CreateInfo.InputElementDesc> InputElements = new List<IGfxGraphicsPipeline.CreateInfo.InputElementDesc>();

            public ReadOnlyMemory<byte>[] Bytecode = [];
        }

        private struct DefinedVariable
        {
            public string Type;
            public string Name;
            public byte RegisterType;
            public byte RegisterSlot;

            public ulong VariantMask;

            public DefinedVariable(string type, string name, string register, ulong mask)
            {
                Type = type;
                Name = name;
                RegisterType = 0;
                RegisterSlot = byte.Parse(register.Substring(1));
                VariantMask = mask;
            }
        }

        private struct DefinedConstantBuffer
        {
            public string Name;
            public byte RegisterSlot;

            public ulong VariantMask;

            public bool IsConstant;

            public DefinedConstantBuffer(string name, string register, ulong mask, bool isConstant)
            {
                Name = name;
                RegisterSlot = byte.Parse(register.Substring(1));
                VariantMask = mask;
                IsConstant = isConstant;
            }
        }

        private struct DefinedSamplerState
        {
            public string Name;
            public byte RegisterSlot;

            public ulong VariantMask;

            public GfxFilter Filter;
            public GfxWrap AddressU;
            public GfxWrap AddressV;
            public GfxWrap AddressW;

            public DefinedSamplerState(string name, string register, ulong mask, GfxFilter filter, GfxWrap u, GfxWrap v, GfxWrap w)
            {
                Name = name;
                RegisterSlot = byte.Parse(register.Substring(1));
                VariantMask = mask;
                Filter = filter;
                AddressU = u;
                AddressV = v;
                AddressW = w;
            }
        }

        private struct MaskStackElement
        {
            public ulong Mask;
            public bool Inverted;

            public MaskStackElement(ulong mask, bool inverted)
            {
                Mask = mask;
                Inverted = inverted;
            }
        }
    }
}
