using CommunityToolkit.HighPerformance;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace SimpleLib.Debugging
{
    public static class MemoryCounter
    {
        private static Dictionary<int, MemoryCounterData> _counters = new Dictionary<int, MemoryCounterData>();

        public static void IncrementCounter(string? name, ulong amount)
        {
            lock (_counters)
            {
                int hash = (name ?? "UnresolvedAlloc").GetDjb2HashCode(); //vroom, vroom hashing
                ref MemoryCounterData counter = ref CollectionsMarshal.GetValueRefOrAddDefault(_counters, hash, out bool exists);

                counter.Name = name ?? "UnresolvedAlloc";
                counter.TotalAllocated += amount;
                counter.IndividualAllocations++;
            }
        }

        public static void DecrementCounter(string? name, ulong amount)
        {
            lock (_counters)
            {
                int hash = (name ?? "UnresolvedAlloc").GetDjb2HashCode();
                ref MemoryCounterData counter = ref CollectionsMarshal.GetValueRefOrNullRef(_counters, hash);

                if (!Unsafe.IsNullRef(ref counter))
                {
                    counter.TotalAllocated -= amount;
                    counter.IndividualAllocations--;
                }
            }
        }

        public static void PrintToConsole(ILogger logger)
        {
            lock (_counters)
            {
                logger.Debug("Memory counter dump:");
                foreach (var kvp in _counters)
                {
                    logger.Debug("    {a} (hash:{b}): {c}mb with {d} total allocations", kvp.Value.Name, kvp.Key, kvp.Value.TotalAllocated / 1024.0 / 1024.0, kvp.Value.IndividualAllocations);
                }
            }
        }

        public static Dictionary<int, MemoryCounterData> Counters => _counters;

        public struct MemoryCounterData
        {
            public string Name = string.Empty;

            public ulong TotalAllocated = 0;
            public ulong IndividualAllocations = 0;

            //i dunno double safety?
            public MemoryCounterData()
            {
                Name = string.Empty;

                TotalAllocated = 0;
                IndividualAllocations = 0;
            }
        }
    }
}
