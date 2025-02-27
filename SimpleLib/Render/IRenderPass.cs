using SimpleLib.Render.Components;
using SimpleLib.Render.Data;

namespace SimpleLib.Render
{
    public interface IRenderPass : IDisposable
    {
        public void Pass(RenderEngine engine, RenderPassData data);
    }
}
