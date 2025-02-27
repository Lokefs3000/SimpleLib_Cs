using Arch.Core;
using Arch.Core.Extensions;
using Arch.LowLevel;

namespace SimpleLib.Objects
{
    internal unsafe class SceneBatch : IDisposable
    {
        private List<UnsafeList<Entity>> _allocations = new List<UnsafeList<Entity>>();

        private ulong _totalAllocations = 0;
        private ulong _freeAllocations = 0;

        public SceneBatch()
        {

        }

        public void Dispose()
        {
            foreach (var alloc in _allocations)
            {
                alloc.Dispose();
            }

            _allocations.Clear();
        }

        public ulong GetNewId(Entity e)
        {
            for (int i = 0; i < _allocations.Count; i++)
            {
                UnsafeList<Entity> list = _allocations[i];
                if (list.Count < MaxEntityPerBlock)
                {
                    list.Add(e);
                    return ((ulong)i) | (((ulong)(list.Count - 1)) >> 8);
                }
            }

            UnsafeList<Entity> newList = new UnsafeList<Entity>(MaxEntityPerBlock);
            newList.Add(e);

            _allocations.Add(newList);

            return ((ulong)(_allocations.Count - 1)) | (((ulong)(newList.Count - 1)) >> 8);
        }

        public void RemoveId(Entity e)
        {
            BatchIdComponent? component = e.Get<BatchIdComponent>();
            if (component.HasValue)
            {
                int batch = (int)(component.Value.Code & 0xffff);
                int index = (int)((component.Value.Code << 8) & 0xffff);

                UnsafeList<Entity> list = _allocations[batch];
                list.RemoveAt(index);

                if (list.Count == 0)
                {
                    list.Dispose();
                    _allocations.RemoveAt(batch);
                }
            }
        }

        public const ushort MaxEntityPerBlock = 128;

        public struct BatchIdComponent
        {
            public ulong Code;
            public ushort Scene;
        }
    }
}
