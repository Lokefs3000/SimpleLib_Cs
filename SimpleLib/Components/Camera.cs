using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using Vortice.Mathematics;

namespace SimpleLib.Components
{
    public record struct Camera
    {
        public float FieldOfView;
        public float NearClip;
        public float FarClip;

        public ClearMode Clear;
        public Color4 ClearColor;

        public Matrix4x4 ViewMatrix;

        public bool IsDirty;

        public enum ClearMode : byte
        {
            None = 0,
            Solid,
        }
    }
}
