using SimpleLib.Debugging;
using SimpleLib.Render.Components;
using SimpleLib.Resources.Data;
using SimpleLib.Timing;
using SimpleLib.Utility;
using SimpleRHI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using TerraFX.Interop.DirectX;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace SimpleLib.Render.Copy
{
    public class ResourceUploader : IDisposable
    {
        private List<QueuedTextureUploadData> _queuedTextureUploads = new List<QueuedTextureUploadData>();
        private List<QueuedBufferUploadData> _queuedBufferUploads = new List<QueuedBufferUploadData>();

        private Queue<(nint, ulong, CounterType)> _pendingMemFrees = new Queue<(nint, ulong, CounterType)>();

        private IGfxCommandQueue _graphicsCommandQueue;
        private IGfxCommandQueue _copyCommandQueue;
        private IGfxCopyCommandBuffer _copyCommandBuffer;
        private IGfxFence _fence;

        public ResourceUploader(IGfxDevice device, IGfxCommandQueue graphicsCommandQueue)
        {
            _graphicsCommandQueue = graphicsCommandQueue;

            _copyCommandQueue = device.CreateCommandQueue(new IGfxCommandQueue.CreateInfo { Type = GfxQueueType.Copy });
            _copyCommandBuffer = device.CreateCopyCommandBuffer(new IGfxCopyCommandBuffer.CreateInfo { Name = "CopyQueuePrimary" });
            _fence = device.CreateFence(new IGfxFence.CreateInfo { InitialValue = 0, Name = "CopyQueueFence" });

            _instance = this;
        }

        public unsafe void Dispose()
        {
            for (int i = 0; i < _queuedTextureUploads.Count; i++)
            {
                if (_queuedTextureUploads[i].Data != nint.Zero)
                {
                    NativeMemory.Free(_queuedTextureUploads[i].Data.ToPointer());
                }
            }

            GC.SuppressFinalize(this);
        }

        //make threading capable as to run this on a seperate thread while rendering occurs
        public unsafe void UploadPendingResources()
        {
            DebugTimers.StartTimer("ResourceUploader.UploadPendingResources");

            if (_pendingMemFrees.Count > 0)
            {
                while (_pendingMemFrees.TryDequeue(out (nint, ulong, CounterType) result))
                {
                    NativeMemory.Free(result.Item1.ToPointer());
                    MemoryCounter.DecrementCounter(result.Item3 == CounterType.TexData ? "UploadTexData" : "UploadBufData", result.Item2);
                }
            }

            if (_queuedTextureUploads.Count > 0 || _queuedBufferUploads.Count > 0)
            {
                _copyCommandBuffer.Begin();

                UploadPendingTextures();
                UploadPendingBuffers();

                _queuedTextureUploads.Clear();
                _queuedBufferUploads.Clear();

                _copyCommandBuffer.Close();

                ulong nextFence = _fence.CompletedValue;
                _copyCommandQueue.Signal(_fence, nextFence);
                _graphicsCommandQueue.Wait(_fence, nextFence);

                _copyCommandQueue.Submit(_copyCommandBuffer);
            }

            DebugTimers.StopTimer();
        }

        private unsafe void UploadPendingTextures()
        {
            Span<QueuedTextureUploadData> queue = CollectionsMarshal.AsSpan(_queuedTextureUploads);
            for (int i = 0; i < queue.Length; i++)
            {
                ref QueuedTextureUploadData uploadData = ref queue[i];
                if (uploadData.Data != nint.Zero)
                {
                    IGfxTexture texture = (IGfxTexture)uploadData.Resource;

                    nint bufferPointerOffset = uploadData.Data;

                    uint width = Math.Max(texture.Desc.Width, 1u);
                    uint height = Math.Max(texture.Desc.Height, 1u);
                    uint depth = Math.Max(texture.Desc.Depth, 1u);

                    uint stride = GraphicsUtilities.GetStride(texture.Desc.Format);

                    for (int j = 0; j < uploadData.MipBufferSizes.Length; j++)
                    {
                        uint paddedRowPitch = GraphicsUtilities.PadSizeForAlignment(width * stride, GraphicsUtilities.TextureUploadAlignment);

                        bool result = _copyCommandBuffer.CopyCPUTexture(new IGfxCopyCommandBuffer.TextureCopyArguments
                        {
                            SourceData = uploadData.Data + (nint)uploadData.MipBufferSizes[j].BufferOffset,
                            SourceOffset = 0ul,

                            Width = width,
                            Height = height,
                            Depth = depth,
                            RowPitch = paddedRowPitch,

                            Destination = texture,
                            DestinationMipSlice = (uint)i,
                            DestinationArraySlice = 0u,
                            DestinationBox = null,

                            Length = uploadData.MipBufferSizes[j].DataSize
                        });

                        if (!result)
                        {
                            LogTypes.Graphics.Error("Failed to upload mip: {a}, to texture: \"{b}\"! Context: \"{c}\".", i, uploadData.Resource, uploadData.Context);
                            break;
                        }

                        width /= 2u;
                        if (height > 1)
                            height /= 2u;
                        if (depth > 1)
                            depth /= 2u;
                    }

                    _pendingMemFrees.Enqueue((uploadData.Data, uploadData.Size, CounterType.TexData));
                    uploadData.Data = nint.Zero;
                }
            }
        }

        private unsafe void UploadPendingBuffers()
        {
            Span<QueuedBufferUploadData> queue = CollectionsMarshal.AsSpan(_queuedBufferUploads);
            for (int i = 0; i < queue.Length; i++)
            {
                ref QueuedBufferUploadData uploadData = ref queue[i];
                if (uploadData.Data != nint.Zero)
                {
                    IGfxBuffer buffer = (IGfxBuffer)uploadData.Resource;

                    nint bufferPointerOffset = uploadData.Data;
                    bool result = _copyCommandBuffer.CopyCPUBuffer(new IGfxCopyCommandBuffer.BufferCopyArguments
                    {
                        SourceData = uploadData.Data,
                        SourceOffset = 0ul,

                        Destination = buffer,
                        DestinationOffset = 0ul,

                        Length = uploadData.Size
                    });

                    _pendingMemFrees.Enqueue((uploadData.Data, uploadData.Size, CounterType.BufData));
                    uploadData.Data = nint.Zero;

                    if (!result)
                    {
                        LogTypes.Graphics.Error("Failed to upload buffer: \"{b}\"! Context: \"{c}\".", uploadData.Resource, uploadData.Context);
                        break;
                    }
                }
            }
        }

        private unsafe void UploadTexture(object context, IGfxTexture texture, ref IGfxTexture.CreateInfo.SubresourceData[] subresources)
        {
            ulong requiredDataSize = 0ul;
            PaddedMipSize[] mipLevelDataSizes = new PaddedMipSize[texture.Desc.MipLevels];

            uint stride = GraphicsUtilities.GetStride(texture.Desc.Format);

            Vector3 size = new Vector3(texture.Desc.Width, texture.Desc.Height, texture.Desc.Depth);
            for (int i = 0; i < subresources.Length; i++)
            {
                uint required = (uint)GraphicsUtilities.CalculateMemorySizeForMip(size, texture.Desc.Format, i) * stride;
                uint padded = GraphicsUtilities.PadSizeForAlignment(required, GraphicsUtilities.TextureUploadAlignment);

                mipLevelDataSizes[i] = new PaddedMipSize(requiredDataSize, required, padded);
                requiredDataSize += padded;
            }

            {
                Span<QueuedTextureUploadData> queue = CollectionsMarshal.AsSpan(_queuedTextureUploads);
                for (int i = 0; i < queue.Length; i++)
                {
                    ref QueuedTextureUploadData data = ref queue[i];
                    if (data.Context == context && data.Resource == texture)
                    {
                        bool needsRealloc = (data.Size < requiredDataSize || ((double)data.Size / (double)requiredDataSize) > UploadShrinkPercentage);
                        if (needsRealloc)
                        {
                            if (data.Data != nint.Zero)
                                NativeMemory.Free(data.Data.ToPointer());

                            MemoryCounter.DecrementCounter("UploadTexData", data.Size);
                            MemoryCounter.IncrementCounter("UploadTexData", requiredDataSize);

                            data.Data = (nint)NativeMemory.Alloc((nuint)requiredDataSize);
                            data.Size = requiredDataSize;
                        }

                        data.MipBufferSizes = mipLevelDataSizes;

                        nint bufferBasePointer = data.Data;

                        uint rowPitch = GraphicsUtilities.PadSizeForAlignment((uint)(size.X), GraphicsUtilities.TextureUploadAlignment);
                        uint depthPitch = (uint)(size.Y * rowPitch * stride);

                        rowPitch *= stride;

                        uint height = (uint)size.Y;
                        uint depth = (uint)size.Z;

                        for (int j = 0; j < subresources.Length; j++)
                        {
                            IGfxTexture.CreateInfo.SubresourceData subresource = subresources[j];

                            nint localOffset = bufferBasePointer;
                            nint localDataOffset = subresource.Data;

                            for (int k = 0; k < depth; k++)
                            {
                                for (int l = 0; l < height; l++)
                                {
                                    NativeMemory.Copy(localDataOffset.ToPointer(), localOffset.ToPointer(), (nuint)rowPitch);

                                    localOffset += (nint)rowPitch;
                                    localDataOffset += (nint)(size.X * stride);
                                }
                            }

                            bufferBasePointer = data.Data + (nint)mipLevelDataSizes[j].BufferOffset;
                        }

                        return;
                    }
                }
            }

            QueuedTextureUploadData uploadData = new QueuedTextureUploadData();
            uploadData.Context = context;
            uploadData.Resource = texture;
            uploadData.Type = UploadType.Texture2D;

            uploadData.MipBufferSizes = mipLevelDataSizes;

            uploadData.Data = (nint)NativeMemory.Alloc((nuint)requiredDataSize);
            uploadData.Size = requiredDataSize;

            MemoryCounter.IncrementCounter("UploadTexData", requiredDataSize);

            {
                nint bufferBasePointer = uploadData.Data;

                uint rowPitch = GraphicsUtilities.PadSizeForAlignment((uint)(size.X), GraphicsUtilities.TextureUploadAlignment);
                uint depthPitch = (uint)(size.Y * rowPitch * stride);

                rowPitch *= stride;

                uint height = (uint)size.Y;
                uint depth = (uint)size.Z;

                for (int j = 0; j < subresources.Length; j++)
                {
                    IGfxTexture.CreateInfo.SubresourceData subresource = subresources[j];

                    nint localOffset = bufferBasePointer;
                    nint localDataOffset = subresource.Data;

                    for (int k = 0; k < depth; k++)
                    {
                        for (int l = 0; l < height; l++)
                        {
                            NativeMemory.Copy(localDataOffset.ToPointer(), localOffset.ToPointer(), (nuint)rowPitch);

                            localOffset += (nint)rowPitch;
                            localDataOffset += (nint)(size.X * stride);
                        }
                    }

                    bufferBasePointer = uploadData.Data + (nint)mipLevelDataSizes[j].BufferOffset;
                }
            }

            _queuedTextureUploads.Add(uploadData);
        }

        private unsafe void UploadBuffer(object context, IGfxBuffer buffer, nint ptr)
        {
            {
                Span<QueuedBufferUploadData> queue = CollectionsMarshal.AsSpan(_queuedBufferUploads);
                for (int i = 0; i < queue.Length; i++)
                {
                    ref QueuedBufferUploadData data = ref queue[i];
                    if (data.Context == context && data.Resource == buffer)
                    {
                        bool needsRealloc = (data.Size < buffer.Desc.Size || ((double)data.Size / (double)buffer.Desc.Size) > UploadShrinkPercentage);
                        if (needsRealloc)
                        {
                            if (data.Data != nint.Zero)
                                NativeMemory.Free(data.Data.ToPointer());

                            MemoryCounter.DecrementCounter("UploadBufData", data.Size);
                            MemoryCounter.IncrementCounter("UploadBufData", buffer.Desc.Size);

                            data.Data = (nint)NativeMemory.Alloc((nuint)buffer.Desc.Size);
                            data.Size = buffer.Desc.Size;
                        }

                        nint bufferBasePointer = data.Data;
                        NativeMemory.Copy(ptr.ToPointer(), bufferBasePointer.ToPointer(), (nuint)data.Size);

                        return;
                    }
                }
            }

            QueuedBufferUploadData uploadData = new QueuedBufferUploadData();
            uploadData.Context = context;
            uploadData.Resource = buffer;

            uploadData.Data = (nint)NativeMemory.Alloc((nuint)buffer.Desc.Size);
            uploadData.Size = buffer.Desc.Size;

            MemoryCounter.IncrementCounter("UploadBufData", buffer.Desc.Size);
            
            {
                nint bufferBasePointer = uploadData.Data;
                NativeMemory.Copy(ptr.ToPointer(), bufferBasePointer.ToPointer(), (nuint)uploadData.Size);
            }
            
            _queuedBufferUploads.Add(uploadData);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Upload(object context, IGfxTexture texture, IGfxTexture.CreateInfo.SubresourceData[] subresources)
            => _instance?.UploadTexture(context, texture, ref subresources);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Upload(object context, IGfxBuffer buffer, nint data /*expects entire buffer worth of data*/)
            => _instance?.UploadBuffer(context, buffer, data);

        public const float UploadShrinkPercentage = 1.3f; //if its more then 30% smaller then shrink the data pointer

        private struct QueuedTextureUploadData
        {
            public object Context;
            public object Resource;
            public UploadType Type;

            public PaddedMipSize[] MipBufferSizes;

            public nint Data;
            public ulong Size;
        }

        private struct QueuedBufferUploadData
        {
            public object Context;
            public object Resource;

            public nint Data;
            public ulong Size;
        }

        private readonly record struct PaddedMipSize
        {
            public readonly ulong BufferOffset;
            public readonly uint DataSize;
            public readonly uint PaddedDataSize;

            public PaddedMipSize(ulong bufferOffset, uint dataSize, uint paddedDataSize)
            {
                BufferOffset = bufferOffset;
                DataSize = dataSize;
                PaddedDataSize = paddedDataSize;
            }
        }

        public enum UploadType
        {
            Texture1D,
            Texture2D,
            Texture3D,
        }

        private enum CounterType
        {
            TexData,
            BufData
        }

        private static ResourceUploader? _instance;
    }
}
