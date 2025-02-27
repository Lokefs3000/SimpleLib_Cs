using System.Diagnostics;

namespace SimpleRHI.D3D12.Memory
{
    internal class RingBuffer : IDisposable
    {
        private Queue<FrameTailAttribs> _completedFrameTails = new Queue<FrameTailAttribs>();

        private ulong _head = 0;
        private ulong _tail = 0;
        private ulong _maxSize = 0;
        private ulong _usedSize = 0;
        private ulong _currFrameSize = 0;

        public RingBuffer(ulong maxSize)
        {
            _maxSize = maxSize;
        }

        public virtual void Dispose()
        {

        }

        public ulong Allocate(ulong size)
        {
            if (IsFull)
            {
                return InvalidOffset;
            }

            if (_tail >= _head)
            {
                if (_tail + size <= _maxSize)
                {
                    ulong offset = _tail;
                    _tail += size;
                    _usedSize += size;
                    _currFrameSize += size;
                    return offset;
                }
                else if (size <= _head)
                {
                    ulong addSize = (_maxSize - _tail) + size;
                    _usedSize += addSize;
                    _currFrameSize += addSize;
                    _tail = size;
                    return 0;
                }
            }
            else if (_tail + size <= _head)
            {
                ulong offset = _tail;
                _tail += size;
                _usedSize += size;
                _currFrameSize += size;
                return offset;
            }

            return InvalidOffset;
        }

        public void FinishCurrentFrame(ulong fenceValue)
        {
            _completedFrameTails.Enqueue(new FrameTailAttribs { FenceValue = fenceValue, Offset = _tail, Size = _currFrameSize });
            _currFrameSize = 0;
        }

        public void ReleaseCompletedFrames(ulong completedFenceValue)
        {
            while (_completedFrameTails.Count > 0 && _completedFrameTails.Peek().FenceValue <= completedFenceValue)
            {
                FrameTailAttribs oldestFrameTail = _completedFrameTails.Dequeue();
                Debug.Assert(oldestFrameTail.Size <= _usedSize);
                _usedSize -= oldestFrameTail.Size;
                _head = oldestFrameTail.Offset;
            }
        }

        public bool IsFull => _usedSize == _maxSize;
        public bool IsEmpty => _usedSize == 0;

        public ulong MaxSize => _maxSize;

        public const ulong InvalidOffset = ulong.MaxValue;

        private struct FrameTailAttribs
        {
            public ulong FenceValue;
            public ulong Offset;
            public ulong Size;
        }
    }
}
