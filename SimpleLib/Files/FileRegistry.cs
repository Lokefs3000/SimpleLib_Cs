using SimpleLib.Utility;
using System.Runtime.InteropServices;
using System.Text;

namespace SimpleLib.Files
{
    public class FileRegistry : IDisposable
    {
        private Stream _stream;

        private Dictionary<string, ulong> _idPairs = new Dictionary<string, ulong>();
        private List<ulong> _idStack = new List<ulong>();

        public FileRegistry(string registryFile)
        {
            bool generateRegistryFile = !File.Exists(registryFile);

            if (!generateRegistryFile)
            {
                {
                    using Stream stream = File.Open(registryFile, FileMode.Open, FileAccess.Read, FileShare.Read);

                    StreamUtility.Deserialize(stream, out FileHeader header);

                    if (header.Header != FileHeader.HeaderReal || header.Version != FileHeader.VersionReal)
                    {
                        LogTypes.Filesystem.Error("File registry corrupt or invalid! Regenerating..");
                        generateRegistryFile = true;
                    }
                    else
                    {
                        for (ulong i = 0; i < header.Files; i++)
                        {
                            StreamUtility.Deserialize(stream, out FileEntry entry);
                            byte[] buffer = new byte[entry.Length];
                            stream.ReadExactly(buffer, 0, buffer.Length);

                            _idPairs.Add(Encoding.UTF8.GetString(buffer), entry.Id);
                            _idStack.Add(entry.Id);
                        }
                    }
                }

                if (!generateRegistryFile)
                {
                    _stream = File.Open(registryFile, FileMode.Open, FileAccess.Write, FileShare.Read);
                }
            }

            if (generateRegistryFile)
            {
                _stream = File.Open(registryFile, FileMode.Create, FileAccess.Write, FileShare.Read);

                FileHeader header = new FileHeader();
                header.Header = FileHeader.HeaderReal;
                header.Version = FileHeader.VersionReal;
                StreamUtility.Serialize(_stream, ref header);

                _stream.Flush();
            }
        }

        public void Dispose()
        {
            _stream.Dispose();
        }

        private ulong GenerateId()
        {
            ulong id = 0;
            while (true)
            {
                id = (ulong)Random.Shared.NextInt64(long.MinValue + 1000L, long.MaxValue - 1L);
                if (!_idStack.Contains(id))
                    break;
            }

            _idStack.Add(id);

            return id;
        }

        public ulong CreateNewId(string filePath)
        {
            if (_idPairs.TryGetValue(filePath, out ulong value))
            {
                return value;
            }

            ulong id = GenerateId();

            FileEntry entry = new FileEntry();
            entry.Id = id;
            entry.Length = (ushort)filePath.Length;

            _idPairs.Add(filePath, id);

            StreamUtility.Serialize(_stream, ref entry);
            _stream.Write(Encoding.UTF8.GetBytes(filePath));
            _stream.Seek(6, SeekOrigin.Begin);
            StreamUtility.Serialize(_stream, (ulong)_idStack.Count);
            _stream.Seek(0, SeekOrigin.End);

            _stream.Flush();

            return id;
        }

        public void SetId(string filePath, ulong id)
        {
            if (_idPairs.TryAdd(filePath, id))
            {
                _idStack.Add(id);
            }
            else
            {
                ulong prev = _idPairs[filePath];
                _idStack.Remove(prev);

                _idPairs[filePath] = id;
                _idStack.Add(id);
            }
        }

        public ulong GetIdForFile(string filePath)
        {
            if (_idPairs.TryGetValue(filePath, out ulong value))
            {
                return value;
            }

            return Invalid;
        }

        public bool DoesFileHaveId(string filePath)
        {
            return _idPairs.ContainsKey(filePath);
        }

        public string? GetNameForId(ulong id)
        {
            foreach (var kvp in _idPairs)
            {
                if (kvp.Value == id)
                {
                    return kvp.Key;
                }
            }

            return null;
        }

        public const ulong Invalid = ulong.MaxValue;

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        private struct FileHeader
        {
            public uint Header;
            public ushort Version;

            public ulong Files;

            public const uint HeaderReal = 0x47455246;
            public const ushort VersionReal = 1;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        private struct FileEntry
        {
            public ulong Id;
            public ushort Length;
        }
    }
}
