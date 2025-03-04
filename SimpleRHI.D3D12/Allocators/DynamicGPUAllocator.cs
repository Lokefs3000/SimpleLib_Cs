using System.Numerics;
using System.Runtime.CompilerServices;

namespace SimpleRHI.D3D12.Allocators
{
    //Segregated fit
    internal class DynamicGPUAllocator : IAllocator
    {
        private List<Queue<BlockAllocInfo>> _allocations = new List<Queue<BlockAllocInfo>>();
        private uint _maxSize;

        private uint _freeSize = 0;

        private Queue<StaleAllocInfo> _stale = new Queue<StaleAllocInfo>();

        public DynamicGPUAllocator(uint maxSize, uint blockSize)
        {
            if (!BitOperations.IsPow2(maxSize))
                throw new ArgumentException("Max size needs to be a power of 2!", "maxSize");

            if (!BitOperations.IsPow2(blockSize))
                throw new ArgumentException("Block size needs to be a power of 2!", "blockSize");

            _maxSize = maxSize;
            _freeSize = maxSize * blockSize;

            int currentSize = 1;
            while (currentSize < maxSize)
            {
                currentSize = (int)BitOperations.RoundUpToPowerOf2((uint)currentSize);
                currentSize++;

                _allocations.Add(new Queue<BlockAllocInfo>());
            }

            Queue<BlockAllocInfo> largest = _allocations[_allocations.Count - 1];
            for (uint i = 0; i < blockSize / maxSize; i++)
            {
                largest.Enqueue(new BlockAllocInfo { Size = maxSize, Offset = i * maxSize });
            }
        }

        public void Dispose()
        {

        }

        //ABSOLUTE SKETCHY BIT MANIPULATION
        //Cause' it needs to run potentially thousands of times a frame!
        //Whatever tricks we can do to optimize this call we do!
        //Hence the use of "MethodImpl()".

        //[MethodImpl(MethodImplOptions.AggressiveInlining)]
        public uint Allocate(uint size)
        {
#if DEBUG
            //if (!BitOperations.IsPow2(size))
            //    throw new ArgumentException("Cannot allocate block that is not a power of 2!", "size");
#endif

            size = BitOperations.RoundUpToPowerOf2(size);
            int index = 31 - BitOperations.LeadingZeroCount(size);

            if (_freeSize < size)
            {
                _freeSize -= size;
                return IAllocator.Invalid;
            }

            Queue<BlockAllocInfo> free = _allocations[index];
            if (free.TryDequeue(out BlockAllocInfo r))
            {
                _freeSize -= size;
                return r.Offset;
            }

            if (!SplitLargerBlock(index))
            {
                GfxDevice.Logger?.Error("No available space within \"DynamicGPUAllocator\"!");
                return IAllocator.Invalid;
            }

            _freeSize -= size;
            return free.Dequeue().Offset;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Free(uint size, uint offset, ulong frame)
        {
#if DEBUG
            //if (!BitOperations.IsPow2(size))
            //    throw new ArgumentException("Cannot free block that is not a power of 2!", "size");
#endif
            size = BitOperations.RoundUpToPowerOf2(size);

            _stale.Enqueue(new StaleAllocInfo
            {
                Offset = offset,
                Size = size,
                Frame = frame
            });
        }

        public void ReleaseStaleAllocations(ulong frameIndex)
        {
            while (_stale.Count > 0 && _stale.Peek().Frame < frameIndex)
            {
                StaleAllocInfo info = _stale.Dequeue();

                _freeSize += info.Size;

                int index = 31 - BitOperations.LeadingZeroCount(info.Size);
                _allocations[index].Enqueue(new BlockAllocInfo { Size = info.Size, Offset = info.Offset });
            }
        }

        //[MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool SplitLargerBlock(int index)
        {
            if (index + 1 == _allocations.Count)
            {
                return false;
            }

            Queue<BlockAllocInfo> free = _allocations[index + 1];
            
            if (free.Count == 0)
            {
                if (!SplitLargerBlock(index + 1))
                {
                    return false;
                }
            }

            if (free.TryDequeue(out BlockAllocInfo r))
            {
                r.Size /= 2;

                Queue<BlockAllocInfo> min = _allocations[index];
                min.Enqueue(r); r.Offset += r.Size;
                min.Enqueue(r);

                return true;
            }

            return false;
        }

        public uint FreeSpace => _freeSize;

        private struct StaleAllocInfo
        {
            public uint Offset;
            public uint Size;
            public ulong Frame;
        }

        private struct BlockAllocInfo
        {
            public uint Size;
            public uint Offset;
        }
    }
}
