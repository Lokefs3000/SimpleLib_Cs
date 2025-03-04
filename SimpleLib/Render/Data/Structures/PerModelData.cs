using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace SimpleLib.Render.Data.Structures
{
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct PerModelData
    {
        public uint TransformIndex;

        private float __padding0;
        private float __padding1;
        private float __padding2;
    }
}
