using System.Numerics;
using System.Runtime.CompilerServices;

namespace SimpleRHI.D3D12.Descriptors
{
    //Segregated fit
    internal class DynamicGPUAllocator
    {
        private List<Queue<ulong>> _allocations = new List<Queue<ulong>>();
        private uint _maxSize;

        private ulong _freeSize = 0;

        private Queue<StaleAllocInfo> _stale = new Queue<StaleAllocInfo>();

        public DynamicGPUAllocator(uint maxSize, ulong blockSize)
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

                _allocations.Add(new Queue<ulong>());
            }

            Queue<ulong> largest = _allocations[_allocations.Count - 1];
            for (ulong i = 0; i < blockSize / maxSize; i++)
            {
                largest.Enqueue(maxSize);
            }
        }

        //ABSOLUTE SKETCHY BIT MANIPULATION
        //Cause' it needs to run potentially thousands of times a frame!
        //Whatever tricks we can do to optimize this call we do!
        //Hence the use of "MethodImpl()".

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ulong Allocate(uint size)
        {
#if DEBUG
            if (!BitOperations.IsPow2(size))
                throw new ArgumentException("Cannot allocate block that is not a power of 2!", "size");
#endif

            int index = 31 - BitOperations.LeadingZeroCount(size);

            if (_freeSize < size)
            {
                _freeSize -= size;
                return InvalidOffset;
            }

            Queue<ulong> free = _allocations[index];
            if (free.TryDequeue(out ulong r))
            {
                return r;
            }

            if (!SplitLargerBlock(index))
            {
                GfxDevice.Logger?.Error("No available space within \"DynamicGPUAllocator\"!");
                return InvalidOffset;
            }

            _freeSize -= size;
            return free.Dequeue();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Free(ulong offset, uint size, ulong frame)
        {
#if DEBUG
            if (!BitOperations.IsPow2(size))
                throw new ArgumentException("Cannot free block that is not a power of 2!", "size");
#endif

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

                int index = 63 - BitOperations.LeadingZeroCount(info.Size);
                _allocations[index].Enqueue(info.Offset);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool SplitLargerBlock(int index)
        {
            if (index + 1 == _allocations.Count)
            {
                return false;
            }

            Queue<ulong> free = _allocations[index + 1];
            
            if (free.Count == 0)
            {
                if (!SplitLargerBlock(index + 1))
                {
                    return false;
                }
            }

            if (free.TryDequeue(out ulong r))
            {
                r /= 2;

                Queue<ulong> min = _allocations[index];
                min.Enqueue(r);
                min.Enqueue(r);

                return true;
            }

            return false;
        }

        public ulong FreeSize => _freeSize;

        public const ulong InvalidOffset = ulong.MaxValue;

        private struct StaleAllocInfo
        {
            public ulong Offset;
            public ulong Size;
            public ulong Frame;
        }
    }
}
