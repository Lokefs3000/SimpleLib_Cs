using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace SimpleRHI.D3D12.Allocators
{
    //fixed ring (no fragmentation/must free all at end of frame)
    internal unsafe class RingAllocator<T> : IAllocator
        where T : unmanaged
    {
        private uint* _ring;

        private uint[] _returned;
        private ulong _lastFrame;

        private uint _head;
        private uint _tail;

        private uint _capacity;
        private uint _freeSpace;

        private bool _isFragmented;

        public RingAllocator(uint capacity)
        {
            _ring = (uint*)NativeMemory.Alloc(capacity, (nuint)sizeof(T));
            _returned = new uint[3] { 0, 0, 0 }; /*max 3 frames in flight at once limit*/
            _lastFrame = 0;
            _head = 0;
            _tail = 0;
            _capacity = capacity;
            _freeSpace = capacity;

            for (uint i = 0; i < capacity; i++)
                _ring[i] = i;
        }

        public void Dispose()
        {
            NativeMemory.Free(_ring);

            _head = 0;
            _tail = 0;
            _freeSpace = 0;
        }

        public uint Allocate(uint size)
        {
            size = BitOperations.RoundUpToPowerOf2(size);

            if (_freeSpace < size)
            {
                GfxDevice.Logger?.Error("Not enough space!");
                return IAllocator.Invalid;
            }

            if (_head + size > _capacity)
            {
                _head = size;
                _freeSpace -= size;

#if DEBUG
                /*if (_head < _tail)
                {
                    GfxDevice.Logger?.Error("Tail is ahead of head!");
                    return IAllocator.Invalid;
                }*/
#endif

                return _ring[0];
            }
            else
            {
                uint head = _head;

                _head += size;
                _freeSpace -= size;

#if DEBUG
                /*if (_head < _tail)
                {
                    GfxDevice.Logger?.Error("Tail is ahead of head!");
                    return IAllocator.Invalid;
                }*/
#endif

                return _ring[head];
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Free(uint size, uint offset, ulong frame)
        {
            _returned[_lastFrame - frame] += size;
        }

        public void ReleaseStaleAllocations(ulong frame)
        {
            uint remainder = _returned[0];

            //sanity checks
            if (remainder > _capacity)
            {
                throw new IndexOutOfRangeException($"Retuned more elements then capacity! (Tail:{_tail}, Remainder:{remainder}, Head:{_head})");
            }

            uint nextTail = _tail + remainder;
            if (nextTail >= _capacity)
            {
                nextTail -= _capacity;
            }

            //if (nextTail > _head)
            //{
            //    throw new IndexOutOfRangeException($"Next tail location is ahead of head! Fragmentation is strictly forbidden. (Tail:{_tail}, NextTail:{nextTail}, Remainder:{remainder}, Head:{_head})");
            //}

            _tail = nextTail;
            _freeSpace += remainder;
            _lastFrame = frame + 1;

            //shift
            _returned[0] = _returned[1];
            _returned[1] = _returned[2];
            _returned[2] = 0;
        }

        public uint Head => _head;
        public uint Tail => _tail;
        public uint Capacity => _capacity;
        public uint FreeSpace => _freeSpace;
    }
}
