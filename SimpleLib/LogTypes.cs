using Serilog;

namespace SimpleLib
{
    public partial class LogTypes
    {
        public static readonly ILogger Runtime = CreateLogger("RRuntime");
        public static readonly ILogger Filesystem = CreateLogger("RFilesystem");
        public static readonly ILogger Resources = CreateLogger("RResources");
        public static readonly ILogger Graphics = CreateLogger("RGraphics");
        public static readonly ILogger Gui = CreateLogger("RGUI");
        public static readonly ILogger Debug = CreateLogger("RDebug");

        public static readonly ILogger RHI = CreateLogger("SRHI");

        public static ILogger CreateLogger(string name)
        {
            return new LoggerConfiguration()
#if DEBUG
                .MinimumLevel.Debug()
#else
                .MinimumLevel.Information()
#endif
                .WriteTo.Console(outputTemplate:
                    $"[{{Timestamp:HH:mm:ss}} {{Level:u3}}/{name}]: {{Message:lj}}{{NewLine}}{{Exception}}")
                .CreateLogger();
        }
    }
}
