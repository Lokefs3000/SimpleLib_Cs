using Arch.Core;
using Arch.Core.Extensions;
using SimpleEditor.Files;
using SimpleEditor.Import;
using SimpleEditor.Resources;
using SimpleLib.Components;
using SimpleLib.Debugging;
using SimpleLib.GUI.sIMGUI;
using SimpleLib.Objects;
using SimpleLib.Render;
using SimpleLib.Resources;
using SimpleLib.Timing;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Globalization;
using System.Numerics;
using System.Runtime.InteropServices;
using Vortice.Mathematics;

namespace SimpleEditor.Runtime
{
    public class EditorRuntime : SimpleLib.Runtime.Runtime
    {
        public readonly Importer Importer;
        public readonly ProjectFileSystem ProjectFileSystem;
        public readonly EngineRealFilesystem EngineFilesystem;
        public readonly EditorShaderPackage EditorShaderPackage;

        public EditorRuntime(ref CreateInfo ci) : base(ref ci)
        {
            Importer = new Importer(this);
            ProjectFileSystem = new ProjectFileSystem(Filesystem, Importer, "Project");
            EngineFilesystem = new EngineRealFilesystem(Filesystem.Registry);
            EditorShaderPackage = new EditorShaderPackage(Filesystem, ProjectFileSystem, EngineFilesystem);

            Filesystem.AddNewFilesystem(ProjectFileSystem);
            Filesystem.AddNewFilesystem(EngineFilesystem);
            ResourceManager.SetShaderPackage(EditorShaderPackage);

            GC.RefreshMemoryLimit();
        }

        public override void Dispose()
        {
            Importer.Dispose();
            ProjectFileSystem.Dispose();
            EditorShaderPackage.Dispose();

            base.Dispose();
        }

        protected override void BeforeMainLoop()
        {
            {
                Window window = WindowRegistry.CreateNewWindow(new WindowRegistry.CreateInfo { Title = "SimpleLib", Size = new UInt2(1336, 726) });
                RenderEngine.SwapChainHandler.RegisterWindow(window, true);
            }

            {
                Scene scene = SceneManager.LoadScene(null);

                {
                    int gw = 8;
                    int gh = 8;
                    int gd = 8;

                    for (int z = 0; z < gd; z++)
                    {
                        for (int y = 0; y < gh; y++)
                        {
                            for (int x = 0; x < gw; x++)
                            {
                                Entity entity = scene.CreateEntity();

                                entity.Set(new Transform
                                {
                                    IsDirty = true,
                                    Position = new Vector3((x - gw * 0.5f) * 3.0f, y * 3.0f, z * 3.0f),
                                    //Rotation = new Vector3(45.0f),
                                    Scale = Vector3.One * 0.5f
                                });

                                entity.Add(new MeshRenderer
                                {
                                    Mesh = ResourceHandler.LoadModel(Filesystem.Registry.GetIdForFile("Content/Meshes/cube.fbx"), true).GetMesh("Cube"),
                                    Material = null
                                });
                            }
                        }
                    }

                    Entity entity2 = scene.CreateEntity();

                    entity2.Set(new Transform
                    {
                        IsDirty = true,
                        Position = new Vector3(0.0f, 8.0f, -10.0f),
                        Rotation = new Vector3(20.0f, 0.0f, 0.0f),
                        Scale = Vector3.One
                    });

                    entity2.Add(new Camera
                    {
                        FieldOfView = 70.0f,
                        NearClip = 0.01f,
                        FarClip = 1000.0f,
                        Clear = Camera.ClearMode.Solid,
                        ClearColor = new Color4(0.4f, 0.1f, 0.2f),
                        IsDirty = true,
                    });
                }
            }
        }

        protected override void DoGUI()
        {
            try
            {
                sIMGUI.ScreenCursor = sIMGUI.ScreenCursor + new Vector2(0.0f, 12.0f);

                sIMGUI.Text("General:", Vector4.One);
                sIMGUI.ScreenCursor = sIMGUI.ScreenCursor + new Vector2(16.0f, 0.0f);
                sIMGUI.Text($"Framerate: {(int)(1.0f / FrameManager.DeltaTime)} (t:{FrameManager.FramerateTarget} e:{Math.Abs((int)(1.0f / FrameManager.DeltaTime) - FrameManager.FramerateTarget)})", Vector4.One);
                sIMGUI.ScreenCursor = sIMGUI.ScreenCursor + new Vector2(16.0f, 0.0f);
                sIMGUI.Text($"GC: {(GC.GetTotalMemory(false) / 1024.0 / 1024.0).ToString("G5", CultureInfo.InvariantCulture)}mb", Vector4.One);
                sIMGUI.Text($"Memory: {(CurrentProcess.PrivateMemorySize64 / 1024.0 / 1024.0).ToString("G5", CultureInfo.InvariantCulture)}mb ({(int)Math.Round(Math.Clamp(CurrentProcess.PrivateMemorySize64 / (double)MaxSystemMemory, 0.0, 1.0) * 100.0)})", Vector4.One);

                sIMGUI.Text("Memory counters:", Vector4.One);
                foreach (var data in MemoryCounter.Counters)
                {
                    MemoryCounter.MemoryCounterData counter = data.Value;

                    sIMGUI.ScreenCursor = sIMGUI.ScreenCursor + new Vector2(16.0f, 0.0f);
                    sIMGUI.Text($"{counter.Name}: {(counter.TotalAllocated / 1024.0 / 1024.0).ToString("G5", CultureInfo.InvariantCulture)}mb/{counter.IndividualAllocations}", counter.TotalAllocated == 0 ? new Vector4(0.7f, 0.7f, 0.7f, 1.0f) : Vector4.One);
                }

                sIMGUI.Text("Debug timers:", Vector4.One);
                foreach (var data in DebugTimers.Timers)
                {
                    if (data.Value.Parent == null)
                    {
                        sIMGUI.ScreenCursor = sIMGUI.ScreenCursor + new Vector2(16.0f, 0.0f);
                        DrawDebugTimerTree(data.Key, data.Value);
                    }
                }
            }
            catch (Exception)
            {
            }
        }

        private void DrawDebugTimerTree(string name, DebugTimers.TimerObject timer)
        {
            float x = sIMGUI.ScreenCursor.X + 16.0f;
            double dur = DebugTimers.GetTimerDuration(timer.Id);
            sIMGUI.Text($"{name}: {dur.ToString("F7", CultureInfo.InvariantCulture)}s ({Math.Round(Math.Clamp((float)(dur / FrameManager.DeltaTimeDP), 0.0f, 1.0f) * 100.0f)}%)", Vector4.One);

            for (int i = 0; i < timer.Children.Count; i++)
            {
                sIMGUI.ScreenCursor = sIMGUI.ScreenCursor + new Vector2(x, 0.0f);
                DrawDebugTimerTree(timer.Children[i], DebugTimers.Timers[timer.Children[i]]);
            }
        }

        private static Process CurrentProcess = Process.GetCurrentProcess();
        private static long MaxSystemMemory = GC.GetGCMemoryInfo().TotalAvailableMemoryBytes;

    }
}
