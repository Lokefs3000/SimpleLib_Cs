using Arch.Core;
using SimpleLib.Components;
using SimpleLib.Timing;

namespace SimpleLib.Systems
{
    public class ECSSystemHandler : IDisposable
    {
        public readonly World World;

        public ECSSystemHandler()
        {
            World = World.Create();
        }

        public void Dispose()
        {
            World.Destroy(World);
        }

        public void Update()
        {
            DebugTimers.StartTimer("ECSSystemHandler.Update");

            //World.InlineParallelQuery(new QueryDescription().WithAll<Transform>(), TransformSystem.Job);
            //World.InlineParallelQuery(new QueryDescription().WithAll<Transform, Camera>(), CameraSystem.Job);

            DebugTimers.StopTimer();
        }
    }
}
