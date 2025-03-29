using SDL3;
using BaseRuntime = SimpleLib.Runtime.Runtime;
using EditorRuntime = SimpleEditor.Runtime.EditorRuntime;

using static SDL3.SDL3;

namespace SimpleEditor
{
    internal class Program
    {
        static void Main(string[] args)
        {
			try
			{
                BaseRuntime.CreateInfo ci = new BaseRuntime.CreateInfo();
                ci.RegistryFilePath = "Files.registry";
                ci.CommandArguments = args;

                using (EditorRuntime runtime = new EditorRuntime(ref ci))
                {
                    runtime.Run();
                }
            }
			catch (Exception ex)
			{
#if DEBUG
                throw;
#else
                SDL_ShowSimpleMessageBox(SDL_MessageBoxFlags.Error, "FATAL ERROR", ex.ToString(), SDL_Window.Null);
#endif
            }
        }
    }
}
