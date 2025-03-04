using Arch.Core;
using Arch.Core.Extensions;
using SimpleLib.Components;
using SimpleLib.Files;
using SimpleLib.Render.Components;
using SimpleLib.Render.Copy;
using SimpleLib.Render.Data;
using SimpleLib.Render.Passes;
using SimpleLib.Resources;
using SimpleLib.Resources.Data;
using SimpleLib.Systems;
using SimpleLib.Timing;
using SimpleRHI;
using System.Numerics;

namespace SimpleLib.Render
{
    public class RenderEngine : IDisposable
    {
        public readonly GraphicsDeviceManager DeviceManager;
        public readonly RenderBuilder RenderBuilder;
        public readonly RenderPassContainer CameraRenderPassContainer;
        public readonly RenderPassContainer ScreenRenderPassContainer;
        public readonly SwapChainHandler SwapChainHandler;
        public readonly CommandBufferPool CommandBufferPool;
        public readonly ResourceUploader ResourceUploader;

        private RenderPassData _cameraPassData;
        private RenderPassData _screenPassData;

        private ViewportRenderData _viewportRenderData;
        private CameraRenderData _cameraRenderData;

        private World _world;

        private Material _missingMaterial;

        private RenderBuilderForEach _renderBuilder;
        private RenderBuilderCameraForEach _renderBuilderCamera;

        public RenderEngine(World world)
        {
            DeviceManager = new GraphicsDeviceManager(GraphicsDeviceManager.API.Direct3D12);
            RenderBuilder = new RenderBuilder();
            CameraRenderPassContainer = new RenderPassContainer();
            ScreenRenderPassContainer = new RenderPassContainer();
            SwapChainHandler = new SwapChainHandler(DeviceManager.RenderDevice, DeviceManager.ImmediateContext);
            CommandBufferPool = new CommandBufferPool(DeviceManager, CameraRenderPassContainer);
            ResourceUploader =  new ResourceUploader(DeviceManager.RenderDevice, DeviceManager.ImmediateContext);

            _cameraPassData = new RenderPassData();
            _screenPassData = new RenderPassData();

            _viewportRenderData = new ViewportRenderData();
            _cameraRenderData = new CameraRenderData();

            _cameraPassData.Set(_viewportRenderData);
            _cameraPassData.Set(_cameraRenderData);
            _screenPassData.Set(_viewportRenderData);

            _world = world;
        }

        public void Dispose()
        {
            ResourceUploader.Dispose();
            RenderBuilder.Dispose();
            CameraRenderPassContainer.Dispose();
            SwapChainHandler.Dispose();
            CommandBufferPool.Dispose();
            DeviceManager.Dispose();

            GC.SuppressFinalize(this);
        }

        public void LoadDefaultResources()
        {
            _missingMaterial = ResourceHandler.LoadMaterial(AutoFileRegisterer.EngineMaterialsMissingMaterial, true);

            _renderBuilder = new RenderBuilderForEach(RenderBuilder, _missingMaterial);
            _renderBuilderCamera = new RenderBuilderCameraForEach(RenderBuilder, SwapChainHandler);

            string opaqueName = CameraRenderPassContainer.AddRenderPass(new OpaqueRenderPass(this));
            string transparentName = CameraRenderPassContainer.AddRenderPass(new TransparentRenderPass());
            string compositeName = CameraRenderPassContainer.AddRenderPass(new CompositorRenderPass(), opaqueName, transparentName);
            ScreenRenderPassContainer.AddRenderPass(new sIMGUIRenderer());
        }

        public void Render()
        {
            ResourceUploader.UploadPendingResources();

            DebugTimers.StartTimer("RenderEngine.Render");

            DebugTimers.StartTimer("RenderBuilder.Build");
            _world.InlineQuery(new QueryDescription().WithAll<Transform, Camera>(), ref _renderBuilderCamera);
            _world.InlineQuery(new QueryDescription().WithAll<Transform, MeshRenderer>(), ref _renderBuilder);
            DebugTimers.StopTimer();

            RenderBuilder.Compile();

            SetupCameraPassData();

            CameraRenderPassContainer.BuildGraph();
            for (int i = 0; i < RenderBuilder.Viewpoints.Count; i++)
            {
                _cameraRenderData.RenderCamera = RenderBuilder.Viewpoints[i].Viewpoint;
                _cameraRenderData.RenderTransform = RenderBuilder.Viewpoints[i].Transform;

                CameraRenderPassContainer.ExecuteGraph(this, _cameraPassData);
            }

            RenderBuilder.Reset();

            SetupScreenPassData();

            ScreenRenderPassContainer.BuildGraph();
            ScreenRenderPassContainer.ExecuteGraph(this, _screenPassData);

            CommandBufferPool.EndFrame(DeviceManager.ImmediateContext);
            SwapChainHandler.PresentAll();

            DebugTimers.StopTimer();

            DebugTimers.StartTimer("IGfxDevice.WaitForFrames");
            DeviceManager.RenderDevice.WaitForFrames();
            DebugTimers.StopTimer();
        }

        private void SetupCameraPassData()
        {
            IGfxSwapChain swapChain = SwapChainHandler.GetSwapChain(SwapChainHandler.PrimaryWindowId)
                ?? throw new Exception();

            _viewportRenderData.RenderResolution = new Vector2(swapChain.Desc.Width, swapChain.Desc.Height);
            _viewportRenderData.BackbufferTextureView = swapChain.RenderTargetView;
        }

        private void SetupScreenPassData()
        {
            IGfxSwapChain swapChain = SwapChainHandler.GetSwapChain(SwapChainHandler.PrimaryWindowId)
                ?? throw new Exception();

            _viewportRenderData.RenderResolution = new Vector2(swapChain.Desc.Width, swapChain.Desc.Height);
            _viewportRenderData.BackbufferTextureView = swapChain.RenderTargetView;
        }

        private struct RenderBuilderForEach : IForEach
        {
            private readonly RenderBuilder _builder;
            private readonly Material _missing;

            public RenderBuilderForEach(RenderBuilder builder, Material missing)
            {
                _builder = builder;
                _missing = missing;
            }

            public void Update(Entity entity)
            {
                ref Transform transform = ref entity.TryGetRef<Transform>(out bool t);
                ref MeshRenderer meshRenderer = ref entity.TryGetRef<MeshRenderer>(out bool mr);

                if (t && mr && meshRenderer.Mesh != null)
                {
                    lock (_builder.Flags)
                    {
                        _builder.Flags.Add(new RenderBuilder.RenderFlag
                        {
                            MeshObject = meshRenderer.Mesh,
                            Material = meshRenderer.Material ?? _missing,
                            TransformIndex = (int)_builder.Transforms.Count
                        });

                        _builder.Transforms.Add(transform.WorldMatrix);
                    }
                }
            }
        }

        private struct RenderBuilderCameraForEach : IForEach
        {
            private readonly RenderBuilder _builder;
            private readonly SwapChainHandler _swapChainHandler;

            public RenderBuilderCameraForEach(RenderBuilder builder, SwapChainHandler swapChainHandler)
            {
                _builder = builder;
                _swapChainHandler = swapChainHandler;
            }

            public void Update(Entity entity)
            {
                ref Transform transform = ref entity.TryGetRef<Transform>(out bool t);
                ref Camera camera = ref entity.TryGetRef<Camera>(out bool c);

                if (t && c)
                {
                    IGfxSwapChain swapChain = _swapChainHandler.GetSwapChain(_swapChainHandler.PrimaryWindowId);
                    IGfxTextureView view = swapChain.RenderTargetView;

                    _builder.Viewpoints.Add(new RenderBuilder.RenderPoints
                    {
                        Transform = transform,
                        Viewpoint = camera,
                        RenderTarget = view,
                    });
                }
            }
        }
    }
}
