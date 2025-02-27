using SimpleLib.Files;

namespace SimpleEditor.Files
{
    public class EngineRealFilesystem : ISubFilesystem
    {
        private Dictionary<ulong, string> _files = new Dictionary<ulong, string>();

        public EngineRealFilesystem(FileRegistry registry)
        {
            string[] files = Directory.GetFiles("Engine", "*.*", SearchOption.AllDirectories);
            foreach (string file in files)
            {
                string mod = file.Replace('\\', '/');
                ulong id = registry.GetIdForFile(mod);

                //doesnt exist
                if (id == FileRegistry.Invalid)
                {
                    continue;
                }

                if (_files.ContainsKey(id))
                {
                    LogTypes.Filesystem.Error("File with id already exists: {a} (\"{b}\")! Present element: \"{c}\"", id, mod, _files[id]);
                }
                else
                    _files.Add(registry.GetIdForFile(mod), Path.GetFullPath(file));
            }
        }

        public bool Exists(ulong id)
        {
            return _files.ContainsKey(id);
        }

        public ReadOnlyMemory<byte> ReadBytes(ulong id)
        {
            if (_files.TryGetValue(id, out string? path) && path != null)
            {
                return File.ReadAllBytes(path);
            }

            return null;
        }

        public string? ReadText(ulong id)
        {
            if (_files.TryGetValue(id, out string? path) && path != null)
            {
                return File.ReadAllText(path);
            }

            return null;
        }
    }
}
