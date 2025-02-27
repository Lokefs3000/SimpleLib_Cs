using SimpleLib.Resources.Data;
using SimpleRHI;
using System.Numerics;

namespace SimpleLib.Resources.Constructors
{
    public static class TextureFactory
    {
        public static Texture Create(Vector3 size, GfxFormat pixelFormat = GfxFormat.R8G8B8A8_UNORM, GfxTextureDimension dimension = GfxTextureDimension.Texture2D, int mipLevels = 1, bool frequentUpdate = false)
        {
            if (Device == null)
            {
                throw new ArgumentNullException("Device not assigned!");
            }

            Texture t = new Texture(ulong.MaxValue);
            t.SetupBasicResources(Device, size, pixelFormat, dimension, mipLevels, frequentUpdate);

            return t;
        }

        internal static IGfxDevice? Device;
    }
}
