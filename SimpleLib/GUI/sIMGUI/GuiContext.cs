using SimpleLib.Resources.Constructors;
using SimpleLib.Resources.Data;
using SimpleRHI;
using StbImageSharp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace SimpleLib.GUI.sIMGUI
{
    public class GuiContext : IDisposable
    {
        private int _hotItem;
        private int _activeItem;
        private int _keyboardFocus;

        private byte _mouseButtons;
        private byte _prevButtons;

        private Vector2 _mousePosition;

        private Texture _globalTexture;
        private GuiFont _globalFont;

        private DrawList _drawList;

        private Stack<SavedAreaState> _areaStack = new Stack<SavedAreaState>();

        private Vector2 _screenCursor;
        private Vector2 _screenMinimum;
        private Vector2 _screenMaximum;
        private bool _sameLine;

        public GuiContext()
        {
            _drawList = new DrawList();

            {
                using Stream stream = GetType().Assembly.GetManifestResourceStream("SimpleLib.GUI.sIMGUI.Resources.poppins.png") ??
                    throw new FileNotFoundException("Assembly:GUI/sIMGUI/Resources/poppins.png");
                ImageResult ir = ImageResult.FromStream(stream, ColorComponents.RedGreenBlueAlpha);
                _globalTexture = TextureFactory.Create(new Vector3(ir.Width, ir.Height, 0.0f));

                _globalTexture.SetPixelData<byte>(ir.Data);
                _globalTexture.UploadPixelData();
            }

            {
                using Stream stream = GetType().Assembly.GetManifestResourceStream("SimpleLib.GUI.sIMGUI.Resources.poppins.bin") ??
                    throw new FileNotFoundException("Assembly:GUI/sIMGUI/Resources/poppins.bin");
                _globalFont = new GuiFont(stream, new Vector2(_globalTexture.Size.X, _globalTexture.Size.Y));
            }
        }

        public void Dispose()
        {
            _drawList.Dispose();
        }

        internal void NewFrame()
        {
            _prevButtons = _mouseButtons;
            _hotItem = 0;

            if (_mouseButtons > 0)
                _keyboardFocus = 0;

            _areaStack.Clear();
            _screenCursor = Vector2.Zero;
            _screenMinimum = Vector2.Zero;
            _screenMaximum = Vector2.Zero;
            _sameLine = false;

            _drawList.Reset();
        }

        internal void Render()
        {
            if (_areaStack.Count > 0)
            {
                LogTypes.Gui.Warning("Incompatible area Push/Pop calls!");
            }

            if (_mouseButtons == 0)
            {
                _activeItem = 0;
            }
            else if (_activeItem == 0)
            {
                _activeItem = -1;
            }

            _drawList.End();
        }

        public void PushArea(Vector2 position, Vector2 size)
        {
            _areaStack.Push(new SavedAreaState(_screenCursor, _screenMinimum, _screenMaximum));

            _screenCursor = position;
            _screenMinimum = position;
            _screenMaximum = position + size;

            _drawList.PushClip(new Vector4(_screenMinimum.X, _screenMinimum.Y, _screenMaximum.X, _screenMaximum.Y));
        }

        public void PopArea()
        {
            if (_areaStack.Count > 0)
            {
                SavedAreaState area = _areaStack.Pop();
                _drawList.PopClip();

                _screenCursor = area.ScreenCursor;
                _screenMinimum = area.ScreenMinimum;
                _screenMaximum = area.ScreenMaximum;
            }
            else
            {
                LogTypes.Gui.Error("Trying to pop area but none are in stack!");
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SameLine()
        {
            _sameLine = true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void AddItem(Vector2 size)
        {
            if (!_sameLine)
            {
                _screenCursor = new Vector2(_screenMinimum.X, _screenCursor.Y + 16.0f);
                _sameLine = false;
            }
            else
            {
                _screenCursor += new Vector2(size.X + 4.0f, 0.0f);
            }
        }

        public Vector2 CalcTextSize(string text, int limit = int.MaxValue)
        {
            Vector2 size = new Vector2(0.0f, 24.0f * 0.5f);

            limit = Math.Min(text.Length, limit);
            for (int i = 0; i < limit; i++)
            {
                if (_globalFont.TryGetGlyph((byte)text[i], out GuiGlyph glyph))
                {
                    size.X += glyph.Advance;
                }
            }

            return size;
        }

        public (bool hovered, bool held, bool pressed) HandleButtonState(int id, Vector2 min, Vector2 max)
        {
            bool isWithin = IsWithinRectBounds(min, max);

            bool pressed = false;
            if (isWithin)
            {
                _hotItem = id;

                if (_activeItem == 0 && IsButtonDown(MouseButtonId.Left))
                    _activeItem = id;
            }

            if (!IsButtonDown(MouseButtonId.Left) && _hotItem == id && _activeItem == id)
            {
                pressed = true;
            }

            return (_hotItem == id, _activeItem == id, pressed);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool IsWithinRectBounds(Vector2 min, Vector2 max)
        {
            return _mousePosition.X > min.X && -_mousePosition.Y > min.Y && _mousePosition.X < max.X && -_mousePosition.Y < max.Y;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void UpdateButton(MouseButtonId id, bool state)
        {
            if (!state)
                _mouseButtons = (byte)(_mouseButtons & ~(byte)(_mouseButtons | (byte)id));
            else
                _mouseButtons = (byte)(_mouseButtons | (byte)id);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool IsButtonDown(MouseButtonId id)
        {
            return (_prevButtons & (byte)id) > 0;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool IsButtonReleased(MouseButtonId id)
        {
            return (_mouseButtons & (byte)id) > 0 && !IsButtonDown(id);
        }

        public Vector2 ScreenCursor { get => _screenCursor; set { _screenCursor = value; } }

        internal Texture GlobalTexture => _globalTexture;
        internal GuiFont GlobalFont => _globalFont;

        internal DrawList DrawList => _drawList;

        public enum MouseButtonId : byte
        {
            Left = 1,
            Middle = 2,
            Right = 4
        }

        private readonly record struct SavedAreaState
        {
            public readonly Vector2 ScreenCursor;
            public readonly Vector2 ScreenMinimum;
            public readonly Vector2 ScreenMaximum;

            public SavedAreaState(Vector2 cursor, Vector2 minimum, Vector2 maximum)
            {
                ScreenCursor = cursor;
                ScreenMinimum = minimum;
                ScreenMaximum = maximum;
            }
        }
    }
}
