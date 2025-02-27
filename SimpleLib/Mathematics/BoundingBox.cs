using System.Numerics;
using System.Runtime.CompilerServices;

namespace SimpleLib.Mathematics
{
    public struct BoundingBox
    {
        public Vector3 Min;
        public Vector3 Max;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public BoundingBox()
        {
            Min = Vector3.Zero;
            Max = Vector3.Zero;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public BoundingBox(Vector3 min, Vector3 max)
        {
            Min = new Vector3(Math.Min(min.X, max.X), Math.Min(min.Y, max.Y), Math.Min(min.Z, max.Z));
            Max = new Vector3(Math.Max(min.X, max.X), Math.Max(min.Y, max.Y), Math.Max(min.Z, max.Z));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Vector3 GetSize()
        {
            return Max - Min;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Vector3 GetCenter()
        {
            return Vector3.Lerp(Min, Max, 0.5f);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static BoundingBox Combine(BoundingBox a, BoundingBox b)
        {
            return new BoundingBox(
                new Vector3(Math.Min(a.Min.X, b.Min.X), Math.Min(a.Min.Y, b.Min.Y), Math.Min(a.Min.Z, b.Min.Z)),
                new Vector3(Math.Max(a.Max.X, b.Max.X), Math.Max(a.Max.Y, b.Max.Y), Math.Max(a.Max.Z, b.Max.Z)));
        }
    }
}
