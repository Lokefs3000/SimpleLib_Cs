namespace SimpleLib.Files
{
    public class Filesystem : IDisposable
    {
        private List<ISubFilesystem> _filesystems = new List<ISubFilesystem>();

        public readonly FileRegistry Registry;

        public Filesystem(string registryFilePath)
        {
            Registry = new FileRegistry(registryFilePath);

            AutoFileRegisterer.RegisterDefault(Registry);
        }

        public void Dispose()
        {

        }

        public ReadOnlyMemory<byte> ReadBytes(ulong id)
        {
            ISubFilesystem? fs = _filesystems.Where(s => s.Exists(id)).FirstOrDefault();
            if (fs != null)
            {
                return fs.ReadBytes(id);
            }

            LogTypes.Filesystem.Error("Failed to find file: \"{a}\"!", id);
            return null;
        }

        public string? ReadText(ulong id)
        {
            ISubFilesystem? fs = _filesystems.Where(s => s.Exists(id)).FirstOrDefault();
            if (fs != null)
            {
                return fs.ReadText(id);
            }

            LogTypes.Filesystem.Error("Failed to find file: \"{a}\"!", id);
            return null;
        }

        public bool Exists(ulong id)
        {
            return _filesystems.Where(s => s.Exists(id)).FirstOrDefault() != null;
        }

        public void AddNewFilesystem(ISubFilesystem fs)
        {
            _filesystems.Add(fs);
        }
    }
}
