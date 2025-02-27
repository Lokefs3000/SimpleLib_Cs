using SimpleLib.Files;
using SimpleRHI;

namespace SimpleLib.Resources.Data
{
    public class Resource //: IDisposable
    {
        public readonly ulong Id;
        public bool HasLoaded { get; protected set; }

        public Resource(ulong id = FileRegistry.Invalid)
        {
            Id = id;
        }

        /*public int References { get; private set; }

        public bool IsLoaded { get; internal set; }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void AddRef() => References++;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Release() => References--;

        public virtual void Dispose()
        {
            
        }*/
    }
}
