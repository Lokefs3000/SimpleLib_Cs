using SimpleRHI.D3D12.Descriptors;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace SimpleRHI.D3D12.Helpers
{
    internal class BindablePipelineResource
    {
        private uint[] _descriptorIndices = [ushort.MaxValue, ushort.MaxValue, ushort.MaxValue, ushort.MaxValue, ushort.MaxValue, ushort.MaxValue, ushort.MaxValue, ushort.MaxValue, ushort.MaxValue, ushort.MaxValue, ushort.MaxValue, ushort.MaxValue, ushort.MaxValue, ushort.MaxValue, ushort.MaxValue, ushort.MaxValue];
        private ulong[] _descriptorFrames = [0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0];

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool HasIndiceAtIndex(ushort index, ulong frame)
        {
            return _descriptorIndices.Length > index && _descriptorFrames[index] == frame && _descriptorIndices[index] != ushort.MaxValue;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public uint GetIndiceAtIndex(ushort index)
        {
            return _descriptorIndices[index];
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ResetIndiceAtIndex(ushort index)
        {
            _descriptorIndices[index] = uint.MaxValue;
        }

        [MethodImpl (MethodImplOptions.AggressiveInlining)]
        public void SetIndiceAtIndex(ushort index, uint value, ulong frame)
        {
            _descriptorIndices[index] = value;
            _descriptorFrames[index] = frame;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public virtual DescriptorHeapAllocation GetHeapAllocation()
        {
            throw new NotImplementedException();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public virtual ulong GetLocation() //TODO: change command buffers internally to just use GfxBufferView instead as to not clutter this class further?
        {
            throw new NotImplementedException();
        }
    }
}
