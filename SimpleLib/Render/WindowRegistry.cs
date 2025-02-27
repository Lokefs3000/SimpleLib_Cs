using SDL3;
using Vortice.Mathematics;

using static SDL3.SDL3;

namespace SimpleLib.Render
{
    public class WindowRegistry : IDisposable
    {
        public Dictionary<SDL_WindowID, Window> _windows = new Dictionary<SDL_WindowID, Window>();

        public WindowRegistry()
        {

        }

        public void Dispose()
        {
            foreach (KeyValuePair<SDL_WindowID, Window> window in _windows)
            {
                window.Value.Dispose();
            }
        }

        public Window CreateNewWindow(in CreateInfo ci)
        {
            SDL_Window window = SDL_CreateWindow(ci.Title, (int)ci.Size.X, (int)ci.Size.Y, SDL_WindowFlags.Resizable);
            return new Window(window);
        }

        public struct CreateInfo
        {
            public string Title;
            public UInt2 Size;
        }
    }
}
