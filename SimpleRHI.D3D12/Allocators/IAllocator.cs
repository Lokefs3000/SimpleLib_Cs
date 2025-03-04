using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SimpleRHI.D3D12.Allocators
{
    internal interface IAllocator : IDisposable
    {
        public uint Allocate(uint size);
        public void Free(uint size, uint offset, ulong frame);
        public void ReleaseStaleAllocations(ulong frame);

        public uint FreeSpace { get; }

        public const uint Invalid = uint.MaxValue;
    }
}
