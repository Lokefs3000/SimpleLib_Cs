using Serilog;

namespace SimpleEditor
{
    public partial class LogTypes
    {
        public static readonly ILogger Import = SimpleLib.LogTypes.CreateLogger("EImport");
        public static readonly ILogger Filesystem = SimpleLib.LogTypes.CreateLogger("EFilesystem");
        public static readonly ILogger Resources = SimpleLib.LogTypes.CreateLogger("EResources");
    }
}
