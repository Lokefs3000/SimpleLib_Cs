using SharpGen.Runtime;
using SimpleLib.Debugging;
using SimpleLib.Render.Copy;
using SimpleLib.Utility;
using SimpleRHI;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace SimpleLib.Resources.Data
{
    public class Texture : Resource
    {
        private readonly Storage _storage;

        internal Texture(ulong id, IDisposable? data = null) : base(id)
        {
            _storage = (data != null && data is Storage) ? (Storage)data : new Storage();

            _storage.Size = Vector3.Zero;
        }

        internal void SetupBasicResources(IGfxDevice device, Vector3 size, GfxFormat pixelFormat, GfxTextureDimension dimension, int mipLevels, bool frequentUpdate)
        {
            if (mipLevels > 1)
            {
                if (size.X > 0 && !BitOperations.IsPow2((uint)size.X))
                {
                    throw new ArgumentException("Width must be a power of 2 for mipmaps to be usable!");
                }

                if (dimension > GfxTextureDimension.Texture1D && size.Y > 0 && !BitOperations.IsPow2((uint)size.Y))
                {
                    throw new ArgumentException("Height must be a power of 2 for mipmaps to be usable!");
                }

                if (dimension > GfxTextureDimension.Texture2D && size.Z > 0 && !BitOperations.IsPow2((uint)size.Z))
                {
                    throw new ArgumentException("Depth must be a power of 2 for mipmaps to be usable!");
                }
            }

            _storage.Device = device;
            _storage.Size = size;
            _storage.PixelFormat = pixelFormat;
            _storage.Dimension = dimension;
            _storage.MipLevels = mipLevels;
            _storage.IsUploadable = true;
            _storage.FrequentUpdate = frequentUpdate;

            _storage.PixelBuffers = new nint[mipLevels];
            _storage.ModifedPages = new bool[mipLevels];

            Array.Fill(_storage.PixelBuffers, nint.Zero);
            Array.Fill(_storage.ModifedPages, false);
        }

        internal void BindResources(IGfxTexture texture, IGfxTextureView view)
        {
            _storage.Dispose();

            _storage.Texture = texture;
            _storage.View = view;

            HasLoaded = true;
        }

        //i am shaking please dont let the GC move this around :|

        //nah this is like 5mins old when i was using GetPointerUnsafe!
        //im now using the better "fixed" keyword instead!

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public unsafe void SetPixelData<TPixel>(ReadOnlySpan<TPixel> pixelData, int mipLevel = 0) where TPixel : unmanaged
        {
            fixed (TPixel* ptr = pixelData)
            {
                InternalUploadPixelData((nint)ptr, (uint)(pixelData.Length * (uint)sizeof(TPixel)), mipLevel);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public unsafe void UploadPixelData<TPixel>(TPixel[] pixelData, int mipLevel = 0) where TPixel : unmanaged
        {
            fixed (TPixel* ptr = pixelData)
            {
                InternalUploadPixelData((nint)ptr, (uint)(pixelData.Length * (uint)sizeof(TPixel)), mipLevel);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void UploadPixelData(nint pixelData, int mipLevel = 0)
        {
            InternalUploadPixelData(pixelData, uint.MaxValue, mipLevel);
        }

        private unsafe void InternalUploadPixelData(nint buffer, uint dataSize, int mipLevel)
        {
            if (!_storage.IsUploadable)
            {
                throw new InvalidOperationException("Cannot set pixel data on non-uploadable texture!");
            }

            if (dataSize < uint.MaxValue && dataSize != _storage.GetTotalRequiredSize(mipLevel))
            {
                throw new Exception("Data size supplied to upload is improperly sized!");
            }

            if (mipLevel >= _storage.MipLevels)
            {
                throw new ArgumentOutOfRangeException(nameof(mipLevel), mipLevel, "Specified mip level is more then total mip levels!");
            }

            if (_storage.ModifedPages == null || _storage.PixelBuffers == null)
            {
                throw new InvalidOperationException("Internal buffers null!");
            }

            nint bufferAt = _storage.PixelBuffers[mipLevel];
            if (bufferAt == nint.Zero)
            {
                bufferAt = _storage.PixelBuffers[mipLevel] = (nint)NativeMemory.Alloc(dataSize);
                MemoryCounter.IncrementCounter("Texture", dataSize);
            }

            NativeMemory.Copy(buffer.ToPointer(), bufferAt.ToPointer(), dataSize);

            _storage.ModifedPages[mipLevel] = true;
        }

        public void UploadPixelData(bool finalize = true)
        {
            if (!_storage.IsUploadable)
            {
                throw new InvalidOperationException("Texture is not uploadable!");
            }

            if (finalize && _storage.FrequentUpdate)
            {
                throw new InvalidOperationException("Cannot finalize a resource marked for frequent updates!");
            }

            if (_storage.ModifedPages == null || _storage.PixelBuffers == null)
            {
                throw new InvalidOperationException("Internal buffers null!");
            }

            if (_storage.Texture == null)
            {
                try
                {
                    _storage.View?.Dispose();

                    IGfxTexture.CreateInfo.SubresourceData[] subresources = new IGfxTexture.CreateInfo.SubresourceData[_storage.MipLevels];
                    for (int i = 0; i < subresources.Length; i++)
                    {
                        subresources[i] = new IGfxTexture.CreateInfo.SubresourceData()
                        {
                            Data = _storage.PixelBuffers[i],
                            Stride = GraphicsUtilities.GetStride(_storage.PixelFormat)
                        };

                        _storage.ModifedPages[i] = false;
                    }

                    _storage.Texture = _storage.Device?.CreateTexture(new IGfxTexture.CreateInfo
                    {
                        Name = Id.ToString(),
                        Width = (uint)_storage.Size.X,
                        Height = (uint)((_storage.Dimension > GfxTextureDimension.Texture1D) ? _storage.Size.Y : 1),
                        Depth = (uint)((_storage.Dimension > GfxTextureDimension.Texture2D) ? _storage.Size.Z : 1),
                        MipLevels = (uint)_storage.MipLevels,
                        Format = _storage.PixelFormat,
                        Bind = GfxBindFlags.ShaderResource,
                        MemoryUsage = finalize ? GfxMemoryUsage.Immutable : (_storage.FrequentUpdate ? GfxMemoryUsage.Dynamic : GfxMemoryUsage.Default),
                        Dimension = _storage.Dimension,
                    });

                    _storage.View = _storage.Texture?.CreateView(new IGfxTextureView.CreateInfo { Type = GfxTextureViewType.ShaderResource });

                    MemoryCounter.IncrementCounter("Texture.VRAM", GraphicsUtilities.CalculateMemorySizeForMips(_storage.Size, _storage.PixelFormat, _storage.MipLevels));

                    ResourceUploader.Upload(this, _storage.Texture ?? throw new NullReferenceException(), subresources);
                }
                catch (Exception ex)
                {
                    LogTypes.Resources.Error(ex, "Failed to create and/or upload texture data!");
                }
            }
            else
            {
                for (int i = 0; i < _storage.MipLevels; i++)
                {
                    if (_storage.ModifedPages[i])
                    {
                        _storage.ModifedPages[i] = false;
                    }
                }
            }

            _storage.IsUploadable = finalize;

            if (finalize)
            {
                unsafe
                {
                    uint total = _storage.GetTotalRequiredSize(0);
                    for (int i = 0; i < _storage.PixelBuffers.Length; i++)
                    {
                        if (_storage.PixelBuffers[i] != nint.Zero)
                        {
                            NativeMemory.Free(_storage.PixelBuffers[i].ToPointer());
                            MemoryCounter.DecrementCounter("Texture", total);
                        }

                        total /= 2u;
                    }
                }

                _storage.PixelBuffers = null;
                _storage.ModifedPages = null;
            }
        }

        public override string ToString()
        {
            return $"Texture2D {{ Id:{Id} }}";
        }

        internal Storage Data => _storage;

        public Vector3 Size => _storage.Size;
        public GfxFormat PixelFormat => _storage.PixelFormat;
        public GfxTextureDimension TextureDimension => _storage.Dimension;
        public int MipLevels => _storage.MipLevels;
        public bool IsUploadable => _storage.IsUploadable;
        public bool FrequentUpdate => _storage.FrequentUpdate;

        internal class Storage : IDisposable
        {
            public IGfxDevice? Device;

            public IGfxTexture? Texture = null;
            public IGfxTextureView? View = null;

            public Vector3 Size;
            public GfxFormat PixelFormat;
            public GfxTextureDimension Dimension;
            public int MipLevels;
            public bool IsUploadable;
            public bool FrequentUpdate;

            public nint[]? PixelBuffers = null;
            public bool[]? ModifedPages = null;

            public void Dispose()
            {
                Texture?.Dispose();
                View?.Dispose();

                unsafe
                {
                    if (PixelBuffers != null)
                    {
                        foreach (nint buffer in PixelBuffers)
                        {
                            if (buffer != nint.Zero)
                            {
                                NativeMemory.Free(buffer.ToPointer());
                            }
                        }

                        PixelBuffers = null;
                    }

                    ModifedPages = null;
                }
            }

            public uint GetTotalRequiredSize(int mipLevel)
            {
                mipLevel++;

                int width = (int)Size.X;
                int height = (int)Size.Y;
                int depth = (int)Size.Z;

                uint totalPixels = 0u;

                while (mipLevel-- > 0)
                {
                        totalPixels = (uint)width;
                    if (Dimension > GfxTextureDimension.Texture1D)
                        totalPixels *= (uint)height;
                    if (Dimension > GfxTextureDimension.Texture2D)
                        totalPixels *= (uint)depth;

                    width /= 2;
                    height /= 2;
                    depth /= 2;
                }

                uint stride = GraphicsUtilities.GetStride(PixelFormat);
                return stride * totalPixels;
            }
        }
    }
}
