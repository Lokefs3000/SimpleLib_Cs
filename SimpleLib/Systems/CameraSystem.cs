using Arch.Core;
using Arch.Core.Extensions;
using SimpleLib.Components;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Runtime.Intrinsics;
using System.Text;
using System.Threading.Tasks;

namespace SimpleLib.Systems
{
    internal struct CameraSystem : IForEach
    {
        public void Update(Entity e)
        {
            ref Transform transform = ref e.TryGetRef<Transform>(out bool exists);
            ref Camera camera = ref e.TryGetRef<Camera>(out bool exists2);
            if (exists && exists2)
            {
                if (transform.WasPrevDirty || camera.IsDirty)
                {
                    camera.ViewMatrix = Matrix4x4.CreateLookAt(transform.WorldPosition, transform.WorldPosition + transform.Forward, transform.Up);

                    camera.IsDirty = false;
                }
            }

        }

        public static readonly IForEachJob<CameraSystem> Job = new IForEachJob<CameraSystem>() { ForEach = new CameraSystem() };
    }
}
