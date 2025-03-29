using SDL3;
using SimpleLib.Runtime;
using SimpleRHI;

using static SDL3.SDL3;

namespace SimpleLib.Render.Components
{
    public class SwapChainHandler : IDisposable
    {
        private Dictionary<SDL_WindowID, WindowData> _windows = new Dictionary<SDL_WindowID, WindowData>();

        public SDL_WindowID PrimaryWindowId { get; private set; }

        private IGfxDevice _device;
        private IGfxCommandQueue _immediate;

        private readonly bool _vsyncAllowed;

        public SwapChainHandler(IGfxDevice device, IGfxCommandQueue immediate)
        {
            _device = device;
            _immediate = immediate;

            _vsyncAllowed = CommandArguments.GetValueOrDefault("-r-vsync-allowed", true);
        }

        public void Dispose()
        {
            foreach (var kvp in _windows)
            {
                kvp.Value.SwapChain?.Dispose();
            }

            GC.SuppressFinalize(this);
        }

        public void RegisterWindow(Window window, bool isPrimary = false)
        {
            if (!_windows.ContainsKey(window.ID))
            {
                WindowData data = new WindowData();
                data.Window = window;

                try
                {
                    IGfxSwapChain.CreateInfo desc = new IGfxSwapChain.CreateInfo();
                    desc.Width = window.WindowSize.X;
                    desc.Height = window.WindowSize.Y;
                    desc.BufferCount = 2;
                    desc.ColorFormat = GfxFormat.R8G8B8A8_UNORM;
                    desc.DepthFormat = GfxFormat.D24_UNORM_S8_UINT;
                    desc.GraphicsCommandQueue = _immediate;
                    desc.WindowHandle = SDL_GetPointerProperty(SDL_GetWindowProperties(window.SDLWindow), SDL_PROP_WINDOW_WIN32_HWND_POINTER, nint.Zero);

                    data.SwapChain = _device.CreateSwapChain(desc);
                }
                catch (Exception ex)
                {
                    LogTypes.Graphics.Error(ex, "Failed to register window!");
                }

                if (isPrimary)
                    PrimaryWindowId = window.ID;

                _windows.Add(window.ID, data);
            }
        }

        public void UnregisterWindow(Window window)
        {
            if (_windows.TryGetValue(window.ID, out WindowData data))
            {
                data.SwapChain?.Dispose();
            }
        }

        public void PresentAll()
        {
            foreach (var kvp in _windows)
            {
                kvp.Value.SwapChain?.Present(_vsyncAllowed ? 1u : 0u);
            }
        }

        public IGfxSwapChain? GetSwapChain(SDL_WindowID windowID)
        {
            if (_windows.TryGetValue(windowID, out WindowData data))
                return data.SwapChain;
            return null;
        }

        private struct WindowData
        {
            public Window Window;
            public IGfxSwapChain? SwapChain;
        }
    }
}
