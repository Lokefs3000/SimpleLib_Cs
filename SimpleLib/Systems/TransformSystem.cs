using Arch.Core;
using Arch.Core.Extensions;
using SimpleLib.Components;
using System.Numerics;
using System.Runtime.Intrinsics;
using Vortice.Mathematics;

namespace SimpleLib.Systems
{
    internal struct TransformSystem : IForEach
    {
        public void Update(Entity e)
        {
            ref Transform transform = ref e.TryGetRef<Transform>(out bool exists);
            transform.WasPrevDirty = transform.IsDirty;

            if (exists)
            {
                if (transform.IsDirty)
                {
                    Vector128<float> radians = Vector128.DegreesToRadians(transform.Rotation.AsVector128Unsafe());

                    Matrix4x4 model = Matrix4x4.Identity;

                    if (transform.Rotation != Vector3.Zero)
                        model = Matrix4x4.Multiply(model, Matrix4x4.CreateFromYawPitchRoll(radians.GetY(), radians.GetX(), radians.GetZ()));
                    if (transform.Position != Vector3.Zero)
                        model = Matrix4x4.Multiply(model, Matrix4x4.CreateTranslation(transform.Position));
                    if (transform.Scale != Vector3.Zero)
                        model = Matrix4x4.Multiply(model, Matrix4x4.CreateScale(transform.Scale));

                    transform.LocalMatrix = model;
                    transform.WorldMatrix = model;

                    transform.IsDirty = false;
                }
            }

        }

        public const float DegToRad = (float)(Math.PI / 180.0f);
    }
}
