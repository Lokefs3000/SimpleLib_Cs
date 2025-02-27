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
        private uint[] _descriptorIndices = [0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0];

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool HasIndiceAtIndex(ushort index)
        {
            return _descriptorIndices.Length < index && _descriptorIndices[index] != ushort.MaxValue;
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
        public void SetIndiceAtIndex(ushort index, uint value)
        {
            _descriptorIndices[index] = value;
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
