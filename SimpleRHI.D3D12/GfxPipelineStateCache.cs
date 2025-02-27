using SharpGen.Runtime;
using Vortice.Direct3D12;

namespace SimpleRHI.D3D12
{
    internal class GfxPipelineStateCache : IGfxPipelineStateCache
    {
        public IGfxPipelineStateCache.CreateInfo Desc => _desc;
        private IGfxPipelineStateCache.CreateInfo _desc;

        private ID3D12PipelineLibrary _library;

        public GfxPipelineStateCache(in IGfxPipelineStateCache.CreateInfo ci, ID3D12Device14 device)
        {
            _desc = ci;

            Result r = device.CreatePipelineLibrary(ci.CacheBinary, out ID3D12PipelineLibrary? library);
            if (r.Failure || library == null)
            {
                GfxDevice.Logger?.Error("Failed to create pipeline library!");
                throw new Exception(r.Code.ToString());
            }

            _library = library;
        }

        public void Dispose()
        {
            _library.Dispose();
        }

        public unsafe ReadOnlySpan<byte> Serialize()
        {
            byte[] raw = new byte[_library.SerializedSize];
            fixed (byte* ptr = raw)
            {
                _library.Serialize((nint)ptr, (PointerUSize)(ulong)raw.Length);
            }

            return raw;
        }

        public ID3D12PipelineState? LoadGraphics(string name, GraphicsPipelineStateDescription stateDescription)
        {
            try
            {
                return _library.LoadGraphicsPipeline(name, stateDescription);
            }
            catch (Exception)
            {
                //no need to inform the user of errors as this could also happen if it hasnt been stored yet
            }

            return null;
        }

        public void Store(string name, ID3D12PipelineState pipelineState)
        {
            try
            {
                _library.StorePipeline(name, pipelineState);
            }
            catch (Exception ex)
            {
                GfxDevice.Logger?.Error(ex, "Failed to store graphics pipeline state!");
            }
        }
    }
}
