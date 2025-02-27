using SimpleLib.Debugging;
using SimpleLib.Render.Components;
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

namespace SimpleLib.Render.Copy
{
    public class ResourceUploader : IDisposable
    {
        private List<QueuedUploadData> _uploadData = new List<QueuedUploadData>();
        private Queue<(nint, ulong)> _pendingMemFrees = new Queue<(nint, ulong)>();

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
            for (int i = 0; i < _uploadData.Count; i++)
            {
                if (_uploadData[i].Data != nint.Zero)
                {
                    NativeMemory.Free(_uploadData[i].Data.ToPointer());
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
                while (_pendingMemFrees.TryDequeue(out (nint, ulong) result))
                {
                    NativeMemory.Free(result.Item1.ToPointer());
                    MemoryCounter.DecrementCounter("UploadTexData", result.Item2);
                }
            }

            if (_uploadData.Count > 0)
            {
                _copyCommandBuffer.Begin();

                Span<QueuedUploadData> queue = CollectionsMarshal.AsSpan(_uploadData);
                for (int i = 0; i < queue.Length; i++)
                {
                    ref QueuedUploadData uploadData = ref queue[i];
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

                        _pendingMemFrees.Enqueue((uploadData.Data, uploadData.Size));
                        uploadData.Data = nint.Zero;
                    }
                }

                _uploadData.Clear();
                _copyCommandBuffer.Close();

                ulong nextFence = _fence.CompletedValue;
                _copyCommandQueue.Signal(_fence, nextFence);
                _graphicsCommandQueue.Wait(_fence, nextFence);

                _copyCommandQueue.Submit(_copyCommandBuffer);
            }

            DebugTimers.StopTimer();
        }

        private unsafe void Upload(object context, IGfxTexture texture, ref IGfxTexture.CreateInfo.SubresourceData[] subresources)
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
                Span<QueuedUploadData> queue = CollectionsMarshal.AsSpan(_uploadData);
                for (int i = 0; i < queue.Length; i++)
                {
                    ref QueuedUploadData data = ref queue[i];
                    if (data.Context == context)
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

            QueuedUploadData uploadData = new QueuedUploadData();
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

            _uploadData.Add(uploadData);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Upload(object context, IGfxTexture texture, IGfxTexture.CreateInfo.SubresourceData[] subresources)
            => _instance?.Upload(context, texture, ref subresources);
        
        public const float UploadShrinkPercentage = 1.3f; //if its more then 30% smaller then shrink the data pointer

        private struct QueuedUploadData
        {
            public object Context;
            public object Resource;
            public UploadType Type;

            public PaddedMipSize[] MipBufferSizes;

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

        private static ResourceUploader? _instance;
    }
}
