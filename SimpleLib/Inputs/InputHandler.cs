using SDL3;
using SimpleLib.Timing;
using System.Runtime.CompilerServices;
using static SDL3.SDL3;

namespace SimpleLib.Inputs
{
    public class InputHandler
    {
        private Dictionary<string, InputBinding> _bindings = new Dictionary<string, InputBinding>();

        private KeyState[] _keys = new KeyState[(int)KeyCode.Max];

        private char? _lastPressedKey = null;
        private bool _wasUpdatedThisFrame = false;

        private bool _textInputEnabled = false;
        private bool _textInputActiveState = false;

        internal InputHandler()
        {
            _instance = this;

            Array.Fill(_keys, KeyState.None);
        }

        internal void Update(SDL_Event @event)
        {
            DebugTimers.StartTimer("InputHandler.Update");

            switch (@event.type)
            {
                case SDL_EventType.KeyDown:
                    {
                        KeyCode kc = ConvertSDLKeyCode(@event.key.key);
                        KeyState prevState = _keys[(int)kc];

                        if (!prevState.HasFlag(KeyState.Pressed))
                            prevState |= KeyState.UpdatedThisFrame | KeyState.FrameDebounce;
                        if (@event.key.repeat)
                            prevState |= KeyState.Repeated | KeyState.FrameDebounce;

                        _keys[(int)kc] = prevState | KeyState.Pressed;

                        break;
                    }
                case SDL_EventType.KeyUp:
                    {
                        KeyCode kc = ConvertSDLKeyCode(@event.key.key);
                        KeyState prevState = _keys[(int)kc];

                        if (!prevState.HasFlag(KeyState.Pressed))
                            prevState |= KeyState.UpdatedThisFrame | KeyState.FrameDebounce;

                        _keys[(int)kc] = prevState & ~KeyState.Pressed;
                        break;
                    }
                case SDL_EventType.TextInput:
                    {
                        _lastPressedKey = @event.text.GetText()?.First();
                        _wasUpdatedThisFrame = true;
                        break;
                    }
                default: break;
            }

            foreach (KeyValuePair<string, InputBinding> binding in _bindings)
            {
                bool positive = _keys[(int)binding.Value.Positive].HasFlag(KeyState.Pressed);
                bool negative = _keys[(int)binding.Value.Negative].HasFlag(KeyState.Pressed);

                InputBinding b = binding.Value;

                if (positive && !negative)
                    b.Value = 1.0f;
                else if (!positive && negative)
                    b.Value = -1.0f;
                else
                    b.Value = 0.0f;
            }

            DebugTimers.StopTimer();
        }

        internal void FrameUpdate()
        {
            if (!_wasUpdatedThisFrame)
            {
                _lastPressedKey = null;
            }

            _wasUpdatedThisFrame = false;

            if (_textInputEnabled != _textInputActiveState)
            {
                if (_textInputEnabled)
                    _textInputActiveState = SDL_StartTextInput(SDL_GetKeyboardFocus());
                else
                    _textInputActiveState = SDL_StartTextInput(SDL_GetKeyboardFocus());
            }

            for (int i = 0; i < _keys.Length; i++)
            {
                KeyState state = _keys[i];
                if (!state.HasFlag(KeyState.FrameDebounce))
                    _keys[i] = state & ~(KeyState.Repeated | KeyState.UpdatedThisFrame);
                else
                    _keys[i] = state & ~KeyState.FrameDebounce;
            }

        }

        private KeyCode ConvertSDLKeyCode(SDL_Keycode kc)
        {
            switch (kc)
            {
                case SDL_Keycode.A: return KeyCode.A;
                case SDL_Keycode.B: return KeyCode.B;
                case SDL_Keycode.C: return KeyCode.C;
                case SDL_Keycode.D: return KeyCode.D;
                case SDL_Keycode.E: return KeyCode.E;
                case SDL_Keycode.F: return KeyCode.F;
                case SDL_Keycode.G: return KeyCode.G;
                case SDL_Keycode.H: return KeyCode.H;
                case SDL_Keycode.I: return KeyCode.I;
                case SDL_Keycode.J: return KeyCode.J;
                case SDL_Keycode.K: return KeyCode.K;
                case SDL_Keycode.L: return KeyCode.L;
                case SDL_Keycode.M: return KeyCode.M;
                case SDL_Keycode.N: return KeyCode.N;
                case SDL_Keycode.O: return KeyCode.O;
                case SDL_Keycode.P: return KeyCode.P;
                case SDL_Keycode.Q: return KeyCode.Q;
                case SDL_Keycode.R: return KeyCode.R;
                case SDL_Keycode.S: return KeyCode.S;
                case SDL_Keycode.T: return KeyCode.T;
                case SDL_Keycode.U: return KeyCode.U;
                case SDL_Keycode.V: return KeyCode.V;
                case SDL_Keycode.W: return KeyCode.W;
                case SDL_Keycode.X: return KeyCode.X;
                case SDL_Keycode.Y: return KeyCode.Y;
                case SDL_Keycode.Z: return KeyCode.Z;
                case SDL_Keycode.F1: return KeyCode.F1;
                case SDL_Keycode.F2: return KeyCode.F2;
                case SDL_Keycode.F3: return KeyCode.F3;
                case SDL_Keycode.F4: return KeyCode.F4;
                case SDL_Keycode.F5: return KeyCode.F5;
                case SDL_Keycode.F6: return KeyCode.F6;
                case SDL_Keycode.F7: return KeyCode.F7;
                case SDL_Keycode.F8: return KeyCode.F8;
                case SDL_Keycode.F9: return KeyCode.F9;
                case SDL_Keycode.F10: return KeyCode.F10;
                case SDL_Keycode.F11: return KeyCode.F11;
                case SDL_Keycode.F12: return KeyCode.F12;
                case SDL_Keycode._0: return KeyCode.Zero;
                case SDL_Keycode._1: return KeyCode.One;
                case SDL_Keycode._2: return KeyCode.Two;
                case SDL_Keycode._3: return KeyCode.Three;
                case SDL_Keycode._4: return KeyCode.Four;
                case SDL_Keycode._5: return KeyCode.Five;
                case SDL_Keycode._6: return KeyCode.Six;
                case SDL_Keycode._7: return KeyCode.Seven;
                case SDL_Keycode._8: return KeyCode.Eight;
                case SDL_Keycode._9: return KeyCode.Nine;
                case SDL_Keycode.Up: return KeyCode.Up;
                case SDL_Keycode.Left: return KeyCode.Left;
                case SDL_Keycode.Right: return KeyCode.Right;
                case SDL_Keycode.Down: return KeyCode.Down;
                case SDL_Keycode.Backspace: return KeyCode.Backspace;
                case SDL_Keycode.Return: return KeyCode.Return;
                case SDL_Keycode.Escape: return KeyCode.Escape;
                case SDL_Keycode.Tab: return KeyCode.Tab;
                case SDL_Keycode.Capslock: return KeyCode.CapsLock;
                case SDL_Keycode.LeftShift: return KeyCode.LeftShift;
                case SDL_Keycode.LeftControl: return KeyCode.LeftControl;
                case SDL_Keycode.LeftAlt: return KeyCode.LeftAlt;
                case SDL_Keycode.RightShift: return KeyCode.RightShift;
                case SDL_Keycode.RightControl: return KeyCode.RightControl;
                case SDL_Keycode.RightAlt: return KeyCode.RightAlt;
                case SDL_Keycode.Insert: return KeyCode.Insert;
                case SDL_Keycode.Home: return KeyCode.Home;
                case SDL_Keycode.PageUp: return KeyCode.PageUp;
                case SDL_Keycode.Delete: return KeyCode.Delete;
                case SDL_Keycode.End: return KeyCode.End;
                case SDL_Keycode.PageDown: return KeyCode.PageDown;
            }

            return KeyCode.Unknown;
        }

        public void AddBinding(string name, InputBinding binding)
        {
            _bindings.Add(name, binding);
        }

        public InputBinding GetBinding(string name)
        {
            return _bindings[name];
        }

        public void RemoveBinding(string name)
        {
            _bindings.Remove(name);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)] public static bool IsKeyDown(KeyCode code) => _instance._keys[(int)code].HasFlag(KeyState.Pressed);
        [MethodImpl(MethodImplOptions.AggressiveInlining)] public static bool IsKeyUp(KeyCode code) => !_instance._keys[(int)code].HasFlag(KeyState.Pressed);
        [MethodImpl(MethodImplOptions.AggressiveInlining)] public static bool IsKeyRepeated(KeyCode code) => _instance._keys[(int)code].HasFlag(KeyState.Repeated);
        [MethodImpl(MethodImplOptions.AggressiveInlining)] public static bool IsKeyPressed(KeyCode code) => IsKeyDown(code) && _instance._keys[(int)code].HasFlag(KeyState.UpdatedThisFrame);
        [MethodImpl(MethodImplOptions.AggressiveInlining)] public static bool IsKeyReleased(KeyCode code) => IsKeyUp(code) && _instance._keys[(int)code].HasFlag(KeyState.UpdatedThisFrame);
        [MethodImpl(MethodImplOptions.AggressiveInlining)] public static bool IsKeyRepeatedOrPressed(KeyCode code) => IsKeyRepeated(code) || IsKeyPressed(code);

        public static char? CharTextInput => _instance?._lastPressedKey;
        public static bool NeedsTextInputNextFrame { get => _instance._textInputEnabled; set { _instance._textInputEnabled = value; } }

        private static InputHandler? _instance;

        private enum KeyState : byte
        {
            None = 0,
            Pressed = 1 << 0,
            UpdatedThisFrame = 1 << 1,
            Repeated = 1 << 2,
            FrameDebounce = 1 << 3,
        }
    }
}
