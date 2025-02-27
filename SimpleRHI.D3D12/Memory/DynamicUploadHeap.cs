using System.Diagnostics;
using Vortice.Direct3D12;

namespace SimpleRHI.D3D12.Memory
{
    internal class DynamicUploadHeap : IDisposable
    {
        private bool _isCpuAccesible;
        private List<GPURingBuffer> _ringBuffers = new List<GPURingBuffer>();
        private ID3D12Device14 _device;

        public DynamicUploadHeap(bool isCpuAccesible, ID3D12Device14 device, ulong initialSize)
        {
            _isCpuAccesible = isCpuAccesible;
            _device = device;

            _ringBuffers.Add(new GPURingBuffer(initialSize, device, isCpuAccesible));
        }

        public void Dispose()
        {
            for (int i = 0; i < _ringBuffers.Count; i++)
            {
                _ringBuffers[i].Dispose();
            }

            _ringBuffers.Clear();
        }

        public DynamicAllocation Allocate(ulong sizeInBytes, ulong alignment = 0)
        {
            ulong alignmentMask = alignment - 1;
            Debug.Assert((alignmentMask & alignment) == 0);

            ulong alignedSize = Math.Max((sizeInBytes + alignmentMask) & ~alignment, sizeInBytes);
            DynamicAllocation dynAlloc = _ringBuffers[_ringBuffers.Count - 1].Allocate(alignedSize);
            if (dynAlloc.Buffer == null)
            {
                ulong newMaxSize = _ringBuffers[_ringBuffers.Count - 1].MaxSize * 2;
                while (newMaxSize < sizeInBytes) newMaxSize *= 2;

                GfxDevice.Logger?.Debug("Allocating new GPU ring buffer with size: {a}mb", newMaxSize / 1024.0 / 1024.0);

                _ringBuffers.Add(new GPURingBuffer(newMaxSize, _device, _isCpuAccesible));
                dynAlloc = _ringBuffers[_ringBuffers.Count - 1].Allocate(alignedSize);
            }

            return dynAlloc;
        }

        public void FinishFrame(ulong fenceValue, ulong lastCompletedFenceValue)
        {
            int numBuffsToDelete = 0;
            for (int i = 0; i < _ringBuffers.Count; i++)
            {
                GPURingBuffer ringBuffer = _ringBuffers[i];
                ringBuffer.FinishCurrentFrame(fenceValue);
                ringBuffer.ReleaseCompletedFrames(lastCompletedFenceValue);

                if (numBuffsToDelete == i && i < _ringBuffers.Count - 1 && ringBuffer.IsEmpty)
                {
                    numBuffsToDelete++;
                }
            }

            if (numBuffsToDelete > 0)
            {
                GfxDevice.Logger?.Debug("Releasing #{a} gpu ring buffers..", numBuffsToDelete);

                for (int i = 0; i < numBuffsToDelete; i++)
                {
                    _ringBuffers[i].Dispose();
                }

                _ringBuffers.RemoveRange(0, numBuffsToDelete);
            }
        }
    }
}
