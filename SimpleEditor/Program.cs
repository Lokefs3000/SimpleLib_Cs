using BaseRuntime = SimpleLib.Runtime.Runtime;
using EditorRuntime = SimpleEditor.Runtime.EditorRuntime;

namespace SimpleEditor
{
    internal class Program
    {
        static void Main(string[] args)
        {
            BaseRuntime.CreateInfo ci = new BaseRuntime.CreateInfo();
            ci.RegistryFilePath = "Files.registry";
            ci.CommandArguments = args;

            using (EditorRuntime runtime = new EditorRuntime(ref ci))
            {
                runtime.Run();
            }
        }
    }
}
