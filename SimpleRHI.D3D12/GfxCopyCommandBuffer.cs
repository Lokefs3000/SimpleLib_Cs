using SharpGen.Runtime;
using SimpleRHI.D3D12.Memory;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using Vortice.Direct3D12;
using Vortice.Mathematics;

namespace SimpleRHI.D3D12
{
    internal class GfxCopyCommandBuffer : IGfxCopyCommandBuffer
    {
        public IGfxCopyCommandBuffer.CreateInfo Desc => _desc;
        private IGfxCopyCommandBuffer.CreateInfo _desc;

        private GfxDevice _device;
        private ushort _id;

        private ID3D12CommandAllocator _commandAllocator;
        private ID3D12GraphicsCommandList10 _commandList;

        private DynamicUploadHeap _ringBuffer;

        private bool _isOpen = false;

        public GfxCopyCommandBuffer(in IGfxCopyCommandBuffer.CreateInfo ci, GfxDevice device, ushort id)
        {
            _desc = ci;
            _device = device;
            _id = id;

            Result r = device.D3D12Device.CreateCommandAllocator(CommandListType.Copy, out _commandAllocator);
            if (r.Failure || _commandAllocator == null)
            {
                GfxDevice.Logger?.Error("Failed to create command allocator!");
                throw new Exception(r.Code.ToString());
            }

            r = device.D3D12Device.CreateCommandList(0, CommandListType.Copy, _commandAllocator, null, out _commandList);
            if (r.Failure || _commandList == null)
            {
                _commandAllocator?.Dispose();

                GfxDevice.Logger?.Error("Failed to create command allocator!");
                throw new Exception(r.Code.ToString());
            }

            _commandList.Close();

            _ringBuffer = device.GetRingBuffer();
        }

        public void Dispose()
        {
            _commandAllocator?.Dispose();
            _commandList?.Dispose();

            _device.ReturnRingBuffer(_ringBuffer);
        }

        public void Begin()
        {
            if (_isOpen)
            {
                return;
            }

            _commandAllocator.Reset();
            _commandList.Reset(_commandAllocator);

            _isOpen = true;
        }

        public void Close()
        {
            if (!_isOpen)
            {
                return;
            }

            _commandList.Close();

            _isOpen = false;
        }

        public bool CopyBuffer(IGfxCopyCommandBuffer.BufferCopyArguments arguments)
        {
            if (_isOpen && arguments.Source != null && arguments.Destination != null)
            {
                GfxBuffer src = (GfxBuffer)arguments.Source;
                GfxBuffer dst = (GfxBuffer)arguments.Destination;

                /*if (src.CurrentState != ResourceStates.CopySource || dst.CurrentState != ResourceStates.CopyDest)
                {
                    GfxDevice.Logger?.Verbose("CopyCPUBuffer cannot occur as source and/or destination is in an incorrect resource state!");
                    return false;
                }*/

                if (src.CurrentState != ResourceStates.CopySource)
                    _device.EnqueueTransitionForCopyQueue(this, src, ResourceStates.CopySource);
                if (dst.CurrentState != ResourceStates.CopyDest)
                    _device.EnqueueTransitionForCopyQueue(this, dst, ResourceStates.CopyDest);

                _commandList.CopyBufferRegion(
                    dst.D3D12Resource, arguments.DestinationOffset,
                    src.D3D12Resource, arguments.SourceOffset,
                    arguments.Length);

                return true;
            }

            return false;
        }

        public bool CopyTexture(IGfxCopyCommandBuffer.TextureCopyArguments arguments)
        {
            if (_isOpen && arguments.Source != null && arguments.Destination != null)
            {
                GfxBuffer src = (GfxBuffer)arguments.Source;
                GfxTexture dst = (GfxTexture)arguments.Destination;

                /*if (src.CurrentState != ResourceStates.CopySource || dst.CurrentState != ResourceStates.CopyDest)
                {
                    GfxDevice.Logger?.Verbose("CopyCPUBuffer cannot occur as source and/or destination is in an incorrect resource state!");
                    return false;
                }*/

                if (src.CurrentState != ResourceStates.CopySource)
                    _device.EnqueueTransitionForCopyQueue(this, src, ResourceStates.CopySource);
                if (dst.CurrentState != ResourceStates.CopyDest)
                    _device.EnqueueTransitionForCopyQueue(this, dst, ResourceStates.CopyDest);

                TextureCopyLocation srcLocation = new TextureCopyLocation(src.D3D12Resource, new PlacedSubresourceFootPrint()
                {
                    Offset = arguments.SourceOffset,
                    Footprint = new SubresourceFootPrint(FormatConverter.Translate(dst.Desc.Format), arguments.Width, arguments.Height, arguments.Depth, arguments.RowPitch)
                });

                TextureCopyLocation dstLocation = new TextureCopyLocation(dst.D3D12Resource, arguments.DestinationMipSlice + (dst.Desc.MipLevels * arguments.DestinationArraySlice));

                _commandList.CopyTextureRegion(
                    dstLocation, Int3.Zero,
                    srcLocation, arguments.DestinationBox
                    );

                return true;
            }

            return false;
        }

        public unsafe bool CopyCPUBuffer(IGfxCopyCommandBuffer.BufferCopyArguments arguments)
        {
            if (_isOpen && (arguments.Source != null || arguments.SourceData != nint.Zero) && arguments.Destination != null)
            {
                GfxBuffer dst = (GfxBuffer)arguments.Destination;
                /*if (dst.CurrentState != ResourceStates.CopyDest)
                {
                    GfxDevice.Logger?.Verbose("CopyCPUBuffer cannot occur as destination is in an incorrect resource state!");
                    return false;
                }*/

                if (dst.CurrentState != ResourceStates.Common)
                    _device.EnqueueTransitionForCopyQueue(this, dst, ResourceStates.Common);

                DynamicAllocation alloc = _ringBuffer.Allocate(arguments.Length);
                NativeMemory.Copy(arguments.SourceData.ToPointer(), alloc.CPUAddress.ToPointer(), (nuint)arguments.Length);

                _commandList.CopyBufferRegion(
                    dst.D3D12Resource, arguments.DestinationOffset,
                    alloc.Buffer, arguments.SourceOffset + alloc.Offset,
                    arguments.Length);

                return true;
            }

            return false;
        }

        public unsafe bool CopyCPUTexture(IGfxCopyCommandBuffer.TextureCopyArguments arguments)
        {
            if (_isOpen && (arguments.Source != null || arguments.SourceData != nint.Zero) && arguments.Destination != null)
            {
                GfxTexture dst = (GfxTexture)arguments.Destination;
                /*if (dst.CurrentState != ResourceStates.CopyDest)
                {
                    GfxDevice.Logger?.Verbose("CopyCPUTexture cannot occur as destination is in an incorrect resource state!");
                    return false;
                }*/

                if (dst.CurrentState != ResourceStates.Common)
                    _device.EnqueueTransitionForCopyQueue(this, dst, ResourceStates.Common);

                DynamicAllocation alloc = _ringBuffer.Allocate(arguments.Length);
                NativeMemory.Copy(arguments.SourceData.ToPointer(), alloc.CPUAddress.ToPointer(), (nuint)arguments.Length);

                TextureCopyLocation srcLocation = new TextureCopyLocation(alloc.Buffer, new PlacedSubresourceFootPrint()
                {
                    Offset = arguments.SourceOffset,
                    Footprint = new SubresourceFootPrint(FormatConverter.Translate(dst.Desc.Format), arguments.Width, arguments.Height, arguments.Depth, arguments.RowPitch)
                });

                TextureCopyLocation dstLocation = new TextureCopyLocation(dst.D3D12Resource, arguments.DestinationMipSlice + (dst.Desc.MipLevels * arguments.DestinationArraySlice));

                _commandList.CopyTextureRegion(
                    dstLocation, Int3.Zero,
                    srcLocation, arguments.DestinationBox
                    );

                return true;
            }

            return false;
        }

        public bool IsCopySourceCapable(IGfxBuffer buffer) => ((GfxBuffer)buffer).CurrentState == ResourceStates.CopySource;

        public bool IsCopyDestCapable(IGfxBuffer buffer) => ((GfxBuffer)buffer).CurrentState == ResourceStates.CopyDest;
        public bool IsCopyDestCapable(IGfxTexture texture) => ((GfxTexture)texture).CurrentState == ResourceStates.CopyDest;

        public ID3D12GraphicsCommandList10 D3D12GraphicsCommandList => _commandList;
        public ushort Id => _id;
    }
}
