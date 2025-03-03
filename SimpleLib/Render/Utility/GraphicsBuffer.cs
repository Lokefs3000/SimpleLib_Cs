using SimpleRHI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SimpleLib.Render.Utility
{
    public class GraphicsBuffer : IDisposable
    {
        public readonly IGfxBuffer Buffer;
        public readonly IGfxBufferView View;

        public GraphicsBuffer(IGfxDevice device, in IGfxBuffer.CreateInfo bci, in IGfxBufferView.CreateInfo vci)
        {
            Buffer = device.CreateBuffer(bci);
            View = Buffer.CreateView(vci);
        }

        public void Dispose()
        {
            View?.Dispose();
            Buffer?.Dispose();
        }
    }
}
