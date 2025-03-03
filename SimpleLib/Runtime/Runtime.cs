using Arch.Core;
using Schedulers;
using SDL3;
using SimpleLib.Debugging;
using SimpleLib.Files;
using SimpleLib.GUI.sIMGUI;
using SimpleLib.Inputs;
using SimpleLib.Objects;
using SimpleLib.Render;
using SimpleLib.Resources;
using SimpleLib.Systems;
using SimpleLib.Timing;
using static SDL3.SDL3;

namespace SimpleLib.Runtime
{
    public class Runtime : IDisposable
    {
        public static Runtime? GlobalRuntimeInstance { get; private set; }

        public readonly Filesystem Filesystem;
        public readonly JobScheduler JobScheduler;
        public readonly WindowRegistry WindowRegistry;
        public readonly ECSSystemHandler ECSSystemHandler;
        public readonly FrameManager FrameManager;
        public readonly SceneManager SceneManager;
        public readonly InputHandler InputHandler;
        public readonly RenderEngine RenderEngine;
        public readonly ResourceHandler ResourceManager;

        public Runtime(ref CreateInfo ci)
        {
            GlobalRuntimeInstance = this;

            CommandArguments.Parse(ref ci.CommandArguments);

            Filesystem = new Filesystem(ci.RegistryFilePath);
            JobScheduler = new JobScheduler(new JobScheduler.Config());
            WindowRegistry = new WindowRegistry();
            ECSSystemHandler = new ECSSystemHandler();
            FrameManager = new FrameManager();
            SceneManager = new SceneManager(ECSSystemHandler.World);
            InputHandler = new InputHandler();
            RenderEngine = new RenderEngine(ECSSystemHandler.World);
            ResourceManager = new ResourceHandler(JobScheduler, Filesystem, RenderEngine.DeviceManager.RenderDevice);

            World.SharedJobScheduler = JobScheduler;

            sIMGUI.CreateContext();
        }

        public virtual void Dispose()
        {
            sIMGUI.DestroyContext();

            Filesystem.Dispose();
            JobScheduler.Dispose();
            WindowRegistry.Dispose();
            ECSSystemHandler.Dispose();
            SceneManager.Dispose();
            RenderEngine.Dispose();
            ResourceManager.Dispose();

            GlobalRuntimeInstance = null;
        }

        public void Run()
        {
            RenderEngine.LoadDefaultResources();

            BeforeMainLoop();
            while (true)
            {
                while (SDL_PollEvent(out SDL_Event @event))
                {
                    InputHandler.Update(@event);
                }

                InputHandler.FrameUpdate();

                sIMGUI.NewFrame();
                DoGUI();
                RuntimeConsole.DrawToScreenViaIMGUI();
                sIMGUI.Render();

                JobScheduler.Flush();
                ECSSystemHandler.Update();
                RenderEngine.Render();
                FrameManager.MeasureAndWait();
                DebugTimers.ClearValues();
            }
        }

        protected virtual void BeforeMainLoop()
        {

        }

        protected virtual void DoGUI()
        {

        }

        public struct CreateInfo
        {
            public string RegistryFilePath = string.Empty;

            public string[] CommandArguments = [];

            public CreateInfo()
            {
            }
        }
    }
}
