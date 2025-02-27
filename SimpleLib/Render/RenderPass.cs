using Vortice.Mathematics;

namespace SimpleLib.Render
{
    internal class RenderPass : IDisposable
    {
        public readonly string Name;
        public readonly IRenderPass Pass;
        public readonly string[] Required;
        public readonly Color4 Color;

        public RenderPass(IRenderPass pass, string[] required)
        {
            Name = pass.GetType().FullName ?? pass.GetType().Name;
            Pass = pass;
            Required = required;
            Color = new Color4(Random.Shared.NextSingle(), Random.Shared.NextSingle(), Random.Shared.NextSingle(), 1.0f);
        }

        public void Dispose()
        {
            Pass?.Dispose();
        }
    }
}
