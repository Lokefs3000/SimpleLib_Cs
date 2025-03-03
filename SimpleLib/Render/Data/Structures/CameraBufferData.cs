using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace SimpleLib.Render.Data.Structures
{
    [StructLayout(LayoutKind.Sequential)]
    public struct CameraBufferData
    {
        public Vector3 ViewPosition;               private float _padding00;
        public Matrix4x4 ViewProjection;
    }
}
