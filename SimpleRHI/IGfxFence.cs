namespace SimpleRHI
{
    public interface IGfxFence : IDisposable
    {
        public CreateInfo Desc { get; }

        public ulong CompletedValue { get; }

        public void SetEventOnCompletion(ulong value, WaitHandle? @event);
        public void Signal(ulong value);

        public struct CreateInfo
        {
            public string Name;

            public ulong InitialValue;

            public CreateInfo()
            {
                Name = string.Empty;

                InitialValue = 0;
            }
        }
    }
}
