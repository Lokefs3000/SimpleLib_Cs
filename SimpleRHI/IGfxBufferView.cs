namespace SimpleRHI
{
    public interface IGfxBufferView : IDisposable
    {
        public CreateInfo Desc { get; }

        public struct CreateInfo
        {
            public byte Stride;

            public CreateInfo()
            {
                Stride = 0;
            }
        }
    }
}
