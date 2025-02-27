namespace SimpleLib.Files
{
    public interface ISubFilesystem
    {
        public ReadOnlyMemory<byte> ReadBytes(ulong id);
        public string? ReadText(ulong id);

        public bool Exists(ulong id);
    }
}
