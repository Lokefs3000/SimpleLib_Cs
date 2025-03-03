using SimpleLib.Components;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SimpleLib.Render.Data
{
    public class CameraRenderData : RenderPassDataComponent
    {
        public Transform RenderTransform { get; internal set; }
        public Camera RenderCamera { get; internal set; }

        internal CameraRenderData()
        {

        }
    }
}
