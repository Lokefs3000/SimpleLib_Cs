using Serilog;
using SharpGen.Runtime;
using SimpleRHI.D3D12.Allocators;
using SimpleRHI.D3D12.Descriptors;
using SimpleRHI.D3D12.Helpers;
using SimpleRHI.D3D12.Memory;
using System.Diagnostics;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using Vortice.Direct3D;
using Vortice.Direct3D12;
using Vortice.Direct3D12.Debug;
using Vortice.DXGI;

namespace SimpleRHI.D3D12
{
    internal unsafe class GfxDevice : IGfxDevice
    {
        public IGfxDevice.CreateInfo Desc => _desc;
        private readonly IGfxDevice.CreateInfo _desc;

        public string Name => _name;
        private readonly string _name;

        public static Lazy<bool> IsSupported = new Lazy<bool>(CheckSupport);

        private IDXGIFactory7 _factory;
        private IDXGIAdapter4 _adapter;
        private ID3D12SDKConfiguration1 _config;
        private ID3D12Debug? _debug;
        private ID3D12InfoQueue? _infoQueue;
        private ID3D12DeviceFactory _deviceFactory;
        private ID3D12Device14 _device;
        private TerraFX.Interop.DirectX.D3D12MA_Allocator* _allocator;

        private List<DynamicUploadHeap> _uploadHeaps = new List<DynamicUploadHeap>();
        private ushort _commandBufferCount = 0;

        private GfxCommandQueue _directCommandQueue;
        private GfxCommandQueue _copyCommandQueue;

        private CPUDescriptorHeap _cpuHeap_CBV_SRV_UAV;
        private CPUDescriptorHeap _cpuHeap_RTV;
        private CPUDescriptorHeap _cpuHeap_DSV;

        private GPUDescriptorHeap _gpuHeap_CBV_SRV_UAV;

        private GfxCommandQueue _resourceTransitionCommandQueue;
        private ID3D12CommandAllocator _resourceTranstionCommandAllocator;
        private ID3D12GraphicsCommandList10 _resourceTransitionCommandList;
        private GfxFence _resourceTransitionFence;
        private Dictionary<ushort, List<(ITransitionableResource, ResourceStates)>> _pendingResourceTransitions = new Dictionary<ushort, List<(ITransitionableResource, ResourceStates)>>();

        private ID3D12Fence _frameFence;
        private ulong _frameIndex;

        private ManualResetEvent _frameWaitHandle = new ManualResetEvent(false);

        public static ILogger? Logger = null;

        public GfxDevice(in IGfxDevice.CreateInfo ci)
        {
            _desc = ci;
            _name = "Direct3D12 - Unkown";

            Logger = ci.MessageLogger;

            {
                Result r = DXGI.CreateDXGIFactory2(ci.ValidationEnabled, out IDXGIFactory7? factory);
                if (factory == null && ci.ValidationEnabled)
                {
                    ci.MessageLogger?.Warning("Validation layers not available!");
                    r = DXGI.CreateDXGIFactory2(ci.ValidationEnabled, out factory);
                }

                if (r.Failure || factory == null)
                {
                    ci.MessageLogger?.Error("Failed to create DXGI factory!");
                    throw new Exception(r.Code.ToString());
                }

                _factory = factory;
            }

            {
                Result r = _factory.EnumAdapters1(0, out IDXGIAdapter1 adapter1);
                if (r.Failure)
                {
                    ci.MessageLogger?.Error("Failed to enumerate DXGI adapters!");
                    throw new Exception(r.Code.ToString());
                }

                IDXGIAdapter4? v = adapter1.QueryInterfaceOrNull<IDXGIAdapter4>();
                adapter1.Dispose();

                if (r.Failure || v == null)
                {
                    ci.MessageLogger?.Error("Failed to query for \"IDXGIAdapter4\"!");
                    throw new Exception();
                }
                else
                {
                    _adapter = v;
                }
            }

            {
                Result r = Vortice.Direct3D12.D3D12.D3D12GetInterface(Vortice.Direct3D12.D3D12.D3D12SDKConfigurationClsId, out ID3D12SDKConfiguration1? sdk);
                if (r.Failure || sdk == null)
                {
                    ci.MessageLogger?.Error("Failed to get D3D12 SDK configuration!");
                    throw new Exception(r.Code.ToString());
                }
                else
                {
                    _config = sdk;
                }

                //_config.SetSDKVersion(615, ".\\D3D12\\D3D12Core.dll");
            }

            /*if (ci.ValidationEnabled)
            {
                Result r = Vortice.Direct3D12.D3D12.D3D12GetDebugInterface(out _debug);
                if (r.Failure || _debug == null)
                {
                    ci.MessageLogger?.Warning("Failed to get D3D12 debug interface!");
                }
                else
                {
                    _debug.EnableDebugLayer();
                }
            }*/

            {
                Result r = _config.CreateDeviceFactory(615, ".\\D3D12\\", out ID3D12DeviceFactory? factory);
                if (r.Failure || factory == null)
                {
                    ci.MessageLogger?.Error("Failed to get D3D12 device factory!");
                    throw new Exception(r.Code.ToString());
                }

                _deviceFactory = factory;
            }

            if (ci.DebuggingEnabled)
            {
                Result r = _deviceFactory.GetConfigurationInterface(Vortice.Direct3D12.D3D12.D3D12DebugClsId, out _debug);
                if (r.Failure || _debug == null)
                {
                    ci.MessageLogger?.Warning("Failed to get D3D12 debug interface!");
                }
                else
                {
                    using ID3D12Debug6? debug = _debug.QueryInterfaceOrNull<ID3D12Debug6>();
                    if (debug == null)
                    {
                        ci.MessageLogger?.Warning("Failed to acquire \"ID3D12Debug6\" object! Using regular debug layer..");
                    }
                    else
                    {
                        debug.SetEnableAutoName(true);
                        debug.SetEnableGPUBasedValidation(true);
                    }

                    _debug.EnableDebugLayer();
                }
            }

            {
                Result r = _deviceFactory.CreateDevice(null, FeatureLevel.Level_11_1, out ID3D12Device14? device);
                if (r.Failure || device == null)
                {
                    ci.MessageLogger?.Error("Failed to create D3D12 device!");
                    throw new Exception(r.Code.ToString());
                }

                _device = device;
            }

            if (ci.DebuggingEnabled && _debug != null)
            {
                _infoQueue = _device.QueryInterfaceOrNull<ID3D12InfoQueue>();
                _infoQueue?.PushStorageFilter(new InfoQueueFilter()
                {
                    AllowList = new InfoQueueFilterDescription
                    {
                        Severities = [MessageSeverity.Message, MessageSeverity.Info, MessageSeverity.Warning, MessageSeverity.Error, MessageSeverity.Corruption]
                    },
                    DenyList = new InfoQueueFilterDescription
                    {
                        Categories = [ MessageCategory.StateCreation ]
                    }
                });
            }

            {
                TerraFX.Interop.DirectX.D3D12MA_ALLOCATOR_DESC desc = new TerraFX.Interop.DirectX.D3D12MA_ALLOCATOR_DESC();
                desc.Flags = TerraFX.Interop.DirectX.D3D12MA_ALLOCATOR_FLAGS.D3D12MA_ALLOCATOR_FLAG_NONE;
                desc.pDevice = (TerraFX.Interop.DirectX.ID3D12Device*)_device.NativePointer;
                desc.pAdapter = (TerraFX.Interop.DirectX.IDXGIAdapter*)_adapter.NativePointer;

                fixed (TerraFX.Interop.DirectX.D3D12MA_Allocator** ptr = &_allocator)
                {
                    Result r = TerraFX.Interop.DirectX.D3D12MemAlloc.D3D12MA_CreateAllocator(&desc, ptr).Value;
                    if (r.Failure || _allocator == null)
                    {
                        ci.MessageLogger?.Error("Failed to create D3D12MemoryAllocator allocator!");
                        throw new Exception(r.Code.ToString());
                    }
                }
            }

            _cpuHeap_CBV_SRV_UAV = new CPUDescriptorHeap(this, DescriptorHeapType.ConstantBufferViewShaderResourceViewUnorderedAccessView, 2048);
            _cpuHeap_RTV = new CPUDescriptorHeap(this, DescriptorHeapType.RenderTargetView, 128);
            _cpuHeap_DSV = new CPUDescriptorHeap(this, DescriptorHeapType.DepthStencilView, 256);
            _gpuHeap_CBV_SRV_UAV = new GPUDescriptorHeap(this, DescriptorHeapType.ConstantBufferViewShaderResourceViewUnorderedAccessView, 512);

            _directCommandQueue = new GfxCommandQueue(new IGfxCommandQueue.CreateInfo { Type = GfxQueueType.Graphics }, _device);
            _copyCommandQueue = new GfxCommandQueue(new IGfxCommandQueue.CreateInfo { Type = GfxQueueType.Copy }, _device);

            _directCommandQueue.OnCommandBufferSubmit = OnCommandBufferSubmitted;
            _copyCommandQueue.OnCommandBufferSubmit = OnCommandBufferSubmitted;

            _frameFence = _device.CreateFence();
            _frameIndex = 0;

            {
                _resourceTransitionCommandQueue = new GfxCommandQueue(new IGfxCommandQueue.CreateInfo { Type = GfxQueueType.Graphics }, _device);

                Result r = _device.CreateCommandAllocator(CommandListType.Direct, out _resourceTranstionCommandAllocator);
                if (r.Failure)
                {
                    Logger?.Error("Failed to create transition command allocator!");
                    throw new Exception(r.Code.ToString());
                }

                r = _device.CreateCommandList(0u, CommandListType.Direct, _resourceTranstionCommandAllocator, null, out _resourceTransitionCommandList);
                if (r.Failure)
                {
                    Logger?.Error("Failed to create transition command list!");
                    throw new Exception(r.Code.ToString());
                }

                _resourceTransitionCommandList.Close();

                _resourceTransitionFence = new GfxFence(new IGfxFence.CreateInfo { Name = "ResourceTransitionFence" }, this);
            }

            {
                AdapterDescription3 desc = _adapter.Description3;
                _name = "Direct3D12 - " + desc.Description;
            }
        }

        public void Dispose()
        {
            WaitForFrames();
            _frameWaitHandle?.Dispose();

            _resourceTransitionCommandQueue?.Dispose();
            _resourceTransitionCommandList?.Dispose();
            _resourceTranstionCommandAllocator?.Dispose();
            _resourceTransitionFence?.Dispose();

            for (int i = 0; i < _uploadHeaps.Count; i++)
            {
                _uploadHeaps[i]?.Dispose();
            }

            _cpuHeap_CBV_SRV_UAV?.Dispose();
            _cpuHeap_DSV?.Dispose();
            _cpuHeap_RTV?.Dispose();
            _gpuHeap_CBV_SRV_UAV?.Dispose();

            _directCommandQueue?.Dispose();
            _copyCommandQueue?.Dispose();

            if (_allocator != null)
                _allocator->Release();
            _device?.Dispose();
            _deviceFactory?.Dispose();
            _infoQueue?.Dispose();
            _debug?.Dispose();
            _config?.Dispose();
            _adapter?.Dispose();
            _factory?.Dispose();

            _frameWaitHandle?.Dispose();

            GC.SuppressFinalize(this);
        }

        public DynamicUploadHeap GetRingBuffer()
        {
            lock (_uploadHeaps)
            {
                DynamicUploadHeap heap = new DynamicUploadHeap(true, _device, 1024 * 1024);
                _uploadHeaps.Add(heap);

                return heap;
            }
        }

        public void ReturnRingBuffer(DynamicUploadHeap uploadHeap)
        {
            lock (_uploadHeaps)
            {
                _uploadHeaps.Remove(uploadHeap);
                uploadHeap.Dispose();
            }
        }

        public void EnqueueTransitionForCopyQueue(GfxCopyCommandBuffer copyCommandBuffer, ITransitionableResource resource, ResourceStates finalState)
        {
            lock (_pendingResourceTransitions)
            {
                if (!_pendingResourceTransitions.TryGetValue(copyCommandBuffer.Id, out List<(ITransitionableResource, ResourceStates)>? list))
                {
                    list = new List<(ITransitionableResource, ResourceStates)>();
                    _pendingResourceTransitions.Add(copyCommandBuffer.Id, list);
                }

                if (list.Count > 0)
                {
                    {
                        Span<(ITransitionableResource, ResourceStates)> span = CollectionsMarshal.AsSpan(list);
                        for (int i = 0; i < list.Count; i++)
                        {
                            ref (ITransitionableResource, ResourceStates) pending = ref span[i];
                            if (pending.Item1 == resource)
                            {
                                pending.Item2 = finalState;
                                return;
                            }
                        }
                    }
                }

                list.Add((resource, finalState));
            }
        }

        private bool _isFirstGraphicsCommandBuffer = false;

        private void OnCommandBufferSubmitted(GfxQueueType queueType, ushort id)
        {
            lock (_resourceTransitionFence)
            {
                bool hasNeededTransitions = false;
                foreach (var kvp in _pendingResourceTransitions)
                {
                    if (kvp.Value.Count > 0)
                    {
                        hasNeededTransitions = true;
                        break;
                    }
                }

                if (hasNeededTransitions)
                {
                    _resourceTranstionCommandAllocator.Reset();
                    _resourceTransitionCommandList.Reset(_resourceTranstionCommandAllocator);

                    foreach (var kvp in _pendingResourceTransitions)
                    {
                        for (int i = 0; i < kvp.Value.Count; i++)
                        {
                            var transition = kvp.Value[i];
                            transition.Item1.TransitionIfRequired(_resourceTransitionCommandList, transition.Item2, true);
                        }

                        kvp.Value.Clear();
                    }

                    _resourceTransitionCommandList.Close();
                    _resourceTransitionCommandQueue.D3D12CommandQueue.ExecuteCommandList(_resourceTransitionCommandList);

                    if (!_isFirstGraphicsCommandBuffer)
                    {
                        if (queueType == GfxQueueType.Graphics)
                            _directCommandQueue.D3D12CommandQueue.Wait(_resourceTransitionCommandQueue.D3D12Fence, _resourceTransitionFence.CompletedValue + 1);
                        else if (queueType == GfxQueueType.Copy)
                            _copyCommandQueue.D3D12CommandQueue.Wait(_resourceTransitionCommandQueue.D3D12Fence, _resourceTransitionFence.CompletedValue + 1);
                    }

                    _isFirstGraphicsCommandBuffer = true;
                }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public IGfxBuffer CreateBuffer(in IGfxBuffer.CreateInfo ci)
        {
            return new GfxBuffer(ci, this, _allocator);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public IGfxCommandQueue CreateCommandQueue(in IGfxCommandQueue.CreateInfo ci)
        {
            switch (ci.Type)
            {
                case GfxQueueType.Graphics: return _directCommandQueue;
                case GfxQueueType.Copy: return _copyCommandQueue;
                default: throw new NotImplementedException();
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public IGfxFence CreateFence(in IGfxFence.CreateInfo ci)
        {
            return new GfxFence(ci, this);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public IGfxGraphicsCommandBuffer CreateGraphicsCommandBuffer(in IGfxGraphicsCommandBuffer.CreateInfo ci)
        {
            return new GfxGraphicsCommandBuffer(ci, this, Desc.DebuggingEnabled, Desc.ValidationEnabled, _commandBufferCount++);
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public IGfxCopyCommandBuffer CreateCopyCommandBuffer(in IGfxCopyCommandBuffer.CreateInfo ci)
        {
            return new GfxCopyCommandBuffer(ci, this, _commandBufferCount++);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public IGfxGraphicsPipeline CreateGraphicsPipeline(in IGfxGraphicsPipeline.CreateInfo ci)
        {
            return new GfxGraphicsPipeline(ci, this);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public IGfxPipelineStateCache CreatePipelineStateCache(in IGfxPipelineStateCache.CreateInfo ci)
        {
            return new GfxPipelineStateCache(ci, _device);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public IGfxTexture CreateTexture(in IGfxTexture.CreateInfo ci)
        {
            return new GfxTexture(ci, this, _allocator);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public IGfxSwapChain CreateSwapChain(in IGfxSwapChain.CreateInfo ci)
        {
            return new GfxSwapChain(ci, this);
        }

        public void WaitForFrames()
        {
            if (_isFirstGraphicsCommandBuffer)
            {
                _resourceTransitionCommandQueue.WaitForCompletion();
            }

            ulong nextFence = _directCommandQueue.WaitForCompletion();

            _cpuHeap_CBV_SRV_UAV.ReleaseStaleAllocations(nextFence);
            _cpuHeap_DSV.ReleaseStaleAllocations(nextFence);
            _cpuHeap_RTV.ReleaseStaleAllocations(nextFence);
            _gpuHeap_CBV_SRV_UAV.ReleaseStaleAllocations(nextFence);

            for (int i = 0; i < _uploadHeaps.Count; i++)
            {
                _uploadHeaps[i].FinishFrame(nextFence, nextFence + 1);
            }

            DumpInfoLog();

            _isFirstGraphicsCommandBuffer = false;
            _frameIndex = nextFence;
        }

        public void DumpInfoLog()
        {
            if (_infoQueue != null)
            {
                ulong num = _infoQueue.NumStoredMessages;
                for (ulong i = 0; i < num; i++)
                {
                    Message msg = _infoQueue.GetMessage(i);
                    switch (msg.Severity)
                    {
                        case MessageSeverity.Corruption:
                            Logger?.Fatal("[{a}/{b}]: {c}", msg.Category, msg.Id, msg.Description);
                            break;
                        case MessageSeverity.Error:
                            Logger?.Error("[{a}/{b}]: {c}", msg.Category, msg.Id, msg.Description);
                            break;
                        case MessageSeverity.Warning:
                            Logger?.Warning("[{a}/{b}]: {c}", msg.Category, msg.Id, msg.Description);
                            break;
                        case MessageSeverity.Info:
                            Logger?.Information("[{a}/{b}]: {c}", msg.Category, msg.Id, msg.Description);
                            break;
                        case MessageSeverity.Message:
                            Logger?.Debug("[{a}/{b}]: {c}", msg.Category, msg.Id, msg.Description);
                            break;
                        default: break;
                    }
                }

                _infoQueue.ClearStoredMessages();
            }
        }


        public IDXGIFactory7 DXGIFactory => _factory;
        public ID3D12Device14 D3D12Device => _device;

        public CPUDescriptorHeap CPUDescriptors_SRV_CBV_UAV => _cpuHeap_CBV_SRV_UAV;
        public CPUDescriptorHeap CPUDescriptors_RTV => _cpuHeap_RTV;
        public CPUDescriptorHeap CPUDescriptors_DSV => _cpuHeap_DSV;

        public GPUDescriptorHeap GPUDescriptors_CBV_SRV_UAV => _gpuHeap_CBV_SRV_UAV;

        public ulong FrameIndex => _frameIndex;

        private static bool CheckSupport()
        {
            {
                Result r = DXGI.CreateDXGIFactory2(false, out IDXGIFactory7? factory);
                factory?.Dispose();

                if (r.Failure)
                    return false;
            }

            {
                Result r = Vortice.Direct3D12.D3D12.D3D12CreateDevice(null, out ID3D12Device? device);
                device?.Dispose();

                if (r.Failure)
                    return false;
            }

            return true;
        }

        private static unsafe class AGILITYSDK
        {
            [DllImport("D3D12/D3D12Core.dll")]
            public static extern int D3D12GetInterface(Guid* rclsid, Guid* riid, void** ppvDebug);

            public static Result D3D12GetInterface<T>(Guid rclsid, out T? v) where T : ComObject
            {
                Guid guid = typeof(T).GUID;

                void* ptr = null;
                Result r = D3D12GetInterface(&rclsid, &guid, &ptr);

                v = MarshallingHelpers.FromPointer<T>((nint)ptr);
                return r;
            }
        }

        private static unsafe class SIMPLERHI_NATIVE
        {
            [DllImport("SimpleRHI.Native.dll")]
            public static extern int D3D12_Impl_CreateDeviceFactory(void* SDKConfiguration1, uint SDKVersion, byte* SDKPath, Guid* riid, void** ppvFactory);

            public static Result CreateDeviceFactory(ID3D12SDKConfiguration1 SDK, uint SDKVersion, string SDKPath, out ID3D12DeviceFactory? deviceFactory)
            {
                byte[] bytes = Encoding.UTF8.GetBytes(SDKPath);
                Guid riid = Vortice.Direct3D12.D3D12.D3D12DeviceFactoryClsId;

                fixed (byte* ptr2 = bytes)
                {
                    void* ptr = null;
                    Result r = D3D12_Impl_CreateDeviceFactory(SDK.NativePointer.ToPointer(), SDKVersion, ptr2, &riid, &ptr);

                    deviceFactory = new ID3D12DeviceFactory((nint)ptr);
                    return r;
                }
            }
        }
    }
}
