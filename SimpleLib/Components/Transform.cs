using System.Numerics;

namespace SimpleLib.Components
{
    public record struct Transform
    {
        public Vector3 Position;
        public Vector3 Rotation;
        public Vector3 Scale;

        public Matrix4x4 LocalMatrix;
        public Matrix4x4 WorldMatrix;

        public bool IsDirty;
        public bool WasPrevDirty;

        public Vector3 WorldPosition => WorldMatrix.Translation;

        public Vector3 Right => new Vector3(WorldMatrix.M11, WorldMatrix.M12, WorldMatrix.M13);
        public Vector3 Up => new Vector3(WorldMatrix.M21, WorldMatrix.M22, WorldMatrix.M23);
        public Vector3 Forward => new Vector3(WorldMatrix.M31, WorldMatrix.M32, WorldMatrix.M33);
    }
}
