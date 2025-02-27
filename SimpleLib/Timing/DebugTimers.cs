using System.Diagnostics;

namespace SimpleLib.Timing
{
    public static class DebugTimers
    {
        private static Dictionary<string, TimerObject> _timers = new Dictionary<string, TimerObject>();
        private static Dictionary<uint, double> _lastTimers = new Dictionary<uint, double>();

        private static TimerObject? _activeTimer = null;

        public static void StartTimer(string name)
        {
            if (_timers.TryGetValue(name, out TimerObject? timer) && timer != null)
            {
                timer.Stopwatch.Restart();
            }
            else
            {
                timer = new TimerObject();
                timer.Name = name;
                timer.Id = (uint)_timers.Count;

                _timers.Add(name, timer);
            }

            if (_activeTimer != null && timer.Parent != _activeTimer.Name)
            {
                _activeTimer.Children.Add(name);
                timer.Parent = _activeTimer.Name;
            }

            _activeTimer = timer;
        }

        public static void StopTimer()
        {
            if (_activeTimer != null)
            {
                _activeTimer.Stopwatch.Stop();
                _activeTimer.Duration += (double)_activeTimer.Stopwatch.ElapsedTicks / (double)Stopwatch.Frequency;

                if (_activeTimer.Parent != null)
                    _timers.TryGetValue(_activeTimer.Parent, out _activeTimer);
                else
                    _activeTimer = null;
            }
        }

        public static void ClearValues()
        {
            foreach (var kvp in _timers)
            {
                _lastTimers[kvp.Value.Id] = kvp.Value.Duration;
                kvp.Value.Duration = 0.0;
            }
        }

        public static TimerObject GetTimer(string name)
        {
            return _timers[name];
        }

        public static double GetTimerDuration(uint id)
        {
            if (!_lastTimers.TryGetValue(id, out double v))
                return 0.0;
            return v;
        }

        public static bool HasParent(string name)
        {
            return _timers[name].Parent != null;
        }

        internal static void FrameEnd()
        {

        }

        public static Dictionary<string, TimerObject> Timers => _timers;

        public class TimerObject
        {
            public uint Id;

            public Stopwatch Stopwatch = new Stopwatch();
            public List<string> Children = new List<string>();

            public double Duration = 0.0;

            public string Name = string.Empty;
            public string? Parent = null;
        }
    }
}
