using SimpleEditor.Import;
using SimpleLib.Files;
using Tomlyn;
using Tomlyn.Model;

namespace SimpleEditor.Files
{
    public class ProjectFileSystem : ISubFilesystem, IDisposable
    {
        private Dictionary<ulong, FileData> _files = new Dictionary<ulong, FileData>();
        private Dictionary<string, DirectoryData> _dirs = new Dictionary<string, DirectoryData>();

        private readonly string _fullProjectRootPath = string.Empty;

        private readonly Filesystem _fs;
        private readonly FileSystemWatcher _watcher;
        private readonly Importer _importer;

        public ProjectFileSystem(Filesystem fs, Importer importer, string projectRoot)
        {
            _watcher = new FileSystemWatcher(projectRoot);
            _watcher.EnableRaisingEvents = true;
            _watcher.IncludeSubdirectories = true;
            _watcher.NotifyFilter = NotifyFilters.Attributes
                                 | NotifyFilters.CreationTime
                                 | NotifyFilters.DirectoryName
                                 | NotifyFilters.FileName
                                 | NotifyFilters.LastAccess
                                 | NotifyFilters.LastWrite
                                 | NotifyFilters.Security
                                 | NotifyFilters.Size;

            _watcher.Changed += _watcher_Changed;
            _watcher.Created += _watcher_Created;
            _watcher.Deleted += _watcher_Deleted;
            _watcher.Renamed += _watcher_Renamed;
            _watcher.Error += _watcher_Error;

            _fullProjectRootPath = projectRoot;
            _fs = fs;
            _importer = importer;

            ScanDirectory(projectRoot);
        }

        private void ScanDirectory(string directory, DirectoryData? parent = null)
        {
            DirectoryData dirData = new DirectoryData();
            dirData.Name = Path.GetFileName(directory);
            dirData.LocalPath = (directory == _fullProjectRootPath) ? "" : directory.Substring(_fullProjectRootPath.Length + 1).Replace('\\', '/');
            dirData.FullPath = directory;

            string[] files = Directory.GetFiles(dirData.FullPath);
            string[] dirs = Directory.GetDirectories(dirData.FullPath);

            foreach (string file in files)
            {
                if (file.EndsWith(".associate"))
                    continue;
                FileData fileData = CreateFileData(file);

                _files.TryAdd(fileData.Id, fileData);
                dirData.Files.Add(fileData.Id);
            }

            foreach (string dir in dirs)
            {
                ScanDirectory(dir, dirData);
            }

            _dirs.Add(dirData.LocalPath, dirData);
            if (parent != null)
                parent.Directories.Add(dirData.LocalPath);
        }

        private FileData CreateFileData(string file)
        {
            FileData fd = new FileData();
            fd.Name = Path.GetFileName(file);
            fd.LocalPath = file.Substring(_fullProjectRootPath.Length + 1).Replace('\\', '/');
            fd.FullPath = file;
            fd.RealPath = fd.FullPath;

            string extension = Path.GetExtension(file);
            switch (extension)
            {
                case ".png": fd.Type = FileType.Image; break;
                case ".jpg": fd.Type = FileType.Image; break;
                case ".jpeg": fd.Type = FileType.Image; break;
                case ".tga": fd.Type = FileType.Image; break;
                case ".dds": fd.Type = FileType.Image; break;
                case ".psd": fd.Type = FileType.Image; break;
                case ".obj": fd.Type = FileType.Model; break;
                case ".fbx": fd.Type = FileType.Model; break;
                case ".gltf": fd.Type = FileType.Model; break;
                case ".glb": fd.Type = FileType.Model; break;
                default: break;
            }

            fd.Id =
                _fs.Registry.DoesFileHaveId(fd.LocalPath) ?
                    _fs.Registry.GetIdForFile(fd.LocalPath) :
                    _fs.Registry.CreateNewId(fd.LocalPath);

            if (fd.Type == FileType.Image)
            {
                fd.Associate = Path.ChangeExtension(fd.FullPath, ".associate");
                if (!File.Exists(fd.Associate))
                {
                    TomlTable table = new TomlTable();
                    {
                        TomlTable general = new TomlTable();
                        general.Add("Associated", fd.LocalPath);
                        general.Add("Format", 0);
                        general.Add("Type", 0);
                        general.Add("GenerateMipmaps", true);

                        table.Add("General", general);
                    }
                    {
                        TomlTable mipmaps = new TomlTable();
                        mipmaps.Add("MinMipmapSize", 1);
                        mipmaps.Add("MaxMipmapCount", 10);
                        mipmaps.Add("GammaCorrect", false);
                        mipmaps.Add("PremultipliedAlphaBlending", false);
                        mipmaps.Add("FilterType", 0);

                        table.Add("Mipmaps", mipmaps);
                    }
                    {
                        TomlTable image = new TomlTable();
                        image.Add("CutoutAlpha", false);
                        image.Add("CutoutThreshold", 0.5f);
                        image.Add("CutoutDither", false);
                        image.Add("ScaleAlphaForMipmaps", false);
                        image.Add("FlipVertical", false);
                        image.Add("PremultipliedAlpha", false);
                        image.Add("ColorSpace", 0);

                        table.Add("Image", image);
                    }

                    string src = Toml.FromModel(table);

                    try
                    {
                        File.WriteAllText(fd.Associate, src);
                    }
                    catch (Exception ex)
                    {
                        LogTypes.Filesystem.Error(ex, "Failed to write associate file for: \"{}\"!", fd.FullPath);
                    }
                }

                fd.FullPath = Path.Combine(_fullProjectRootPath, "Library/Generated/Images/", fd.Id.ToString());

                _importer.ImportIfOld(this, fd);
            }
            else if (fd.Type == FileType.Model)
            {
                fd.Associate = Path.ChangeExtension(fd.FullPath, ".associate");
                if (!File.Exists(fd.Associate))
                {
                    TomlTable table = new TomlTable();
                    {
                        TomlTable general = new TomlTable();
                        general.Add("Associated", fd.LocalPath);

                        general.Add("Optimize", true);
                        general.Add("HalfPrecision", false);
                        general.Add("EnableLODs", true);

                        table.Add("General", general);
                    }

                    string src = Toml.FromModel(table);

                    try
                    {
                        File.WriteAllText(fd.Associate, src);
                    }
                    catch (Exception ex)
                    {
                        LogTypes.Filesystem.Error(ex, "Failed to write associate file for: \"{}\"!", fd.FullPath);
                    }
                }

                fd.FullPath = Path.Combine(_fullProjectRootPath, "Library/Generated/Models/", fd.Id.ToString());

                _importer.ImportIfOld(this, fd);
            }

            return fd;
        }

        private void _watcher_Error(object sender, ErrorEventArgs e)
        {

        }

        private void _watcher_Renamed(object sender, RenamedEventArgs e)
        {

        }

        private void _watcher_Deleted(object sender, FileSystemEventArgs e)
        {

        }

        private void _watcher_Created(object sender, FileSystemEventArgs e)
        {
            string parentDirectory = Path.GetDirectoryName(e.FullPath).Replace('\\', '/') ?? throw new DirectoryNotFoundException(e.FullPath);
            string localParentDirectory = (parentDirectory == _fullProjectRootPath) ? "" : parentDirectory.Substring(_fullProjectRootPath.Length + 1);

            DirectoryData dirData = _dirs[localParentDirectory];

            FileData fileData = CreateFileData(e.FullPath);

            _files.TryAdd(fileData.Id, fileData);
            dirData.Files.Add(fileData.Id);
        }

        private void _watcher_Changed(object sender, FileSystemEventArgs e)
        {

        }

        public void Dispose()
        {
            _watcher.Dispose();
        }

        public bool Exists(ulong id)
        {
            return _files.ContainsKey(id);
        }

        public ReadOnlyMemory<byte> ReadBytes(ulong id)
        {
            if (_files.TryGetValue(id, out FileData? fileData) && fileData != null)
            {
                return File.ReadAllBytes(fileData.FullPath);
            }

            LogTypes.Filesystem.Warning("Failed to read bytes for: \"{a}\"!", id);
            return null;
        }

        public string? ReadText(ulong id)
        {
            if (_files.TryGetValue(id, out FileData? fileData) && fileData != null)
            {
                return File.ReadAllText(fileData.FullPath);
            }

            LogTypes.Filesystem.Warning("Failed to read text for: \"{a}\"!", id);
            return null;
        }

        public string? ReadAssociate(ulong id)
        {
            if (_files.TryGetValue(id, out FileData? fileData) && fileData != null && fileData.Associate != null)
            {
                return File.ReadAllText(fileData.Associate);
            }

            LogTypes.Filesystem.Warning("Failed to read associate for: \"{a}\"!", id);
            return null;
        }

        public Span<byte> ReadRealBytes(ulong id)
        {
            if (_files.TryGetValue(id, out FileData? fileData) && fileData != null)
            {
                return File.ReadAllBytes(fileData.RealPath);
            }

            LogTypes.Filesystem.Warning("Failed to read bytes for: \"{a}\"!", id);
            return null;
        }

        public string? GetLocalPath(ulong id)
        {
            if (_files.TryGetValue(id, out FileData? fileData) && fileData != null)
            {
                return fileData.LocalPath;
            }

            LogTypes.Filesystem.Warning("Failed to get local path for: \"{a}\"!", id);
            return null;
        }

        public string? GetFullPath(ulong id)
        {
            if (_files.TryGetValue(id, out FileData? fileData) && fileData != null)
            {
                return fileData.FullPath;
            }

            LogTypes.Filesystem.Warning("Failed to get full path for: \"{a}\"!", id);
            return null;
        }

        public string RootPath => _fullProjectRootPath;

        public class FileData
        {
            public string Name = string.Empty;
            public string LocalPath = string.Empty;
            public string FullPath = string.Empty;
            public string RealPath = string.Empty;

            public FileType Type = FileType.Unkown;
            public string? Associate = null;

            public ulong Id = 0;
        }

        public class DirectoryData
        {
            public string Name = string.Empty;
            public string LocalPath = string.Empty;
            public string FullPath = string.Empty;

            public List<ulong> Files = new List<ulong>();
            public List<string> Directories = new List<string>();
        }

        public enum FileType
        {
            Unkown,
            Image,
            Model,
        }
    }
}
