using SimpleRHI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace SimpleLib.Render.Data
{
    public class ViewportRenderData : RenderPassDataComponent
    {
        public Vector2 RenderResolution;
        public IGfxTextureView BackbufferTextureView;
    }
}
