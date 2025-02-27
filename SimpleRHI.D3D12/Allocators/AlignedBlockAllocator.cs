using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace SimpleRHI.D3D12.Allocators
{
    internal unsafe class AlignedBlockAllocator : IDisposable
    {
        private nint _block = nint.Zero;
        private uint _blockSize = 0;
        private uint _totalSize = 0;

        public AlignedBlockAllocator(uint blockSize, uint blockCount)
        {
            _blockSize = blockSize;
            _totalSize = blockSize * blockCount;
            _block = (nint)NativeMemory.Alloc(_totalSize);
        }

        public void Dispose()
        {
            NativeMemory.Free(_block.ToPointer());

            _block = nint.Zero;
            _blockSize = 0;
            _totalSize = 0;
        }

        public Span<T> At<T>(uint offset)
            where T : struct
        {
            return new Span<T>((void*)(_block + offset * _blockSize), (int)_blockSize);
        }
    }
}
