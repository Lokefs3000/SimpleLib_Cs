using SimpleLib.Resources.Data;
using SimpleRHI;
using System.Numerics;

namespace SimpleLib.Resources.Constructors
{
    public static class ModelFactory
    {
        public static Model Create(byte indexStride, bool frequentUpdate = false)
        {
            if (Device == null)
            {
                throw new ArgumentNullException("Device not assigned!");
            }

            Model m = new Model(ulong.MaxValue);
            m.SetupBasicResources(Device, indexStride, frequentUpdate);

            return m;
        }

        internal static IGfxDevice? Device;
    }
}
