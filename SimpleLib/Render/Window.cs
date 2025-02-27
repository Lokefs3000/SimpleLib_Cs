using SDL3;
using Vortice.Mathematics;

using static SDL3.SDL3;

namespace SimpleLib.Render
{
    public class Window : IDisposable
    {
        public readonly SDL_Window SDLWindow;
        public readonly SDL_WindowID ID;

        public UInt2 WindowSize { get; private set; }

        internal Window(SDL_Window window)
        {
            SDL_GetWindowSizeInPixels(window, out int w, out int h);

            SDLWindow = window;
            ID = SDL_GetWindowID(window);
            WindowSize = new UInt2((uint)w, (uint)h);
        }

        public void Dispose()
        {
            SDL_DestroyWindow(SDLWindow);
        }
    }
}
