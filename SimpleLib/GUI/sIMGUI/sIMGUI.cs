using SimpleRHI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace SimpleLib.GUI.sIMGUI
{
    public static class sIMGUI
    {
        private static GuiContext? _context;

        internal static GuiContext CreateContext() //!!! overwrites previous context !!!
        {
            _context = new GuiContext();
            return _context;
        }

        internal static void DestroyContext(GuiContext? context = null)
        {
            context = context ?? _context;
            context?.Dispose();

            if (context == _context)
            {
                _context = null;
            }
        }

        //Control
        [MethodImpl(MethodImplOptions.AggressiveInlining)] internal static void NewFrame() => _context?.NewFrame();
        [MethodImpl(MethodImplOptions.AggressiveInlining)] internal static void Render() => _context?.Render();

        //Layout
        [MethodImpl(MethodImplOptions.AggressiveInlining)] public static void PushArea(Vector2 position, Vector2 size) => _context?.PushArea(position, size);
        [MethodImpl(MethodImplOptions.AggressiveInlining)] public static void PopArea() => _context.PopArea();
        [MethodImpl(MethodImplOptions.AggressiveInlining)] public static void SameLine() => _context.SameLine();
        [MethodImpl(MethodImplOptions.AggressiveInlining)] public static void AddItem(Vector2 size) => _context.AddItem(size);

        //Widgets
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Text(string text, Vector4 color)
        {
            Vector2 next = _context.ScreenCursor;
            Vector2 size = _context.CalcTextSize(text);

            _context.DrawList.AddText(next, text, color);
            _context.AddItem(size);
        }

        //Measurements
        [MethodImpl(MethodImplOptions.AggressiveInlining)] public static Vector2 CalcTextSize(string text) => _context.CalcTextSize(text);

        //Accesors
        public static Vector2 ScreenCursor { get => Context.ScreenCursor; set { Context.ScreenCursor = value; } }

        internal static GuiContext Context => _context ?? throw new ArgumentNullException();
        public static DrawList DrawList => _context?.DrawList ?? throw new ArgumentNullException();
    }
}
