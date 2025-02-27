using Schedulers;
using SimpleLib.Files;
using SimpleLib.Resources.Data;
using SimpleRHI;
using Tomlyn;
using Tomlyn.Model;

namespace SimpleLib.Resources.Loaders
{
    internal class MaterialLoaderImpl : IJob
    {
        public static readonly Queue<Payload> Pending = new Queue<Payload>();
        public static readonly MaterialLoaderImpl Impl = new MaterialLoaderImpl();

        private MaterialLoaderImpl()
        {

        }

        public void Execute()
        {
            Payload payload;
            lock (Pending)
            {
                payload = Pending.Dequeue();
            }

            string? raw = payload.Filesystem.ReadText(payload.Object.Id);
            if (raw == null)
            {
                LogTypes.Resources.Error("Buffer is empty for resource id: {a}!", payload.Object.Id);
                return;
            }

            TomlTable table = Toml.ToModel(raw, payload.Object.Id.ToString());

            TomlTable settings = (TomlTable)table["Settings"];
            ulong shaderId = (ulong)(long)settings["Shader"];

            Shader shader = ResourceHandler.LoadShader(shaderId);
            payload.Object.BindResources(shader, payload.RenderDevice);
        }

        public struct Payload
        {
            public IGfxDevice RenderDevice;
            public Material Object;
            public Filesystem Filesystem;
        }
    }
}
