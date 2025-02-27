using SDL3;
using SimpleLib.Timing;

namespace SimpleLib.Inputs
{
    public class InputHandler
    {
        private Dictionary<string, InputBinding> _bindings = new Dictionary<string, InputBinding>();

        private bool[] _keys = new bool[(int)KeyCode.Max];

        public InputHandler()
        {
            Array.Fill(_keys, false);
        }

        public void Update(SDL_Event @event)
        {
            DebugTimers.StartTimer("InputHandler.Update");

            switch (@event.type)
            {
                case SDL_EventType.KeyDown:
                    {
                        KeyCode kc = ConvertSDLKeyCode(@event.key.key);
                        _keys[(int)kc] = true;
                        break;
                    }
                case SDL_EventType.KeyUp:
                    {
                        KeyCode kc = ConvertSDLKeyCode(@event.key.key);
                        _keys[(int)kc] = false;
                        break;
                    }
                default: break;
            }

            foreach (KeyValuePair<string, InputBinding> binding in _bindings)
            {
                bool positive = _keys[(int)binding.Value.Positive];
                bool negative = _keys[(int)binding.Value.Negative];

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
            }

            return KeyCode.Unkown;
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
    }
}
