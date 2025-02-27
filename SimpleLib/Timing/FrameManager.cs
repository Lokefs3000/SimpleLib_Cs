using System.Diagnostics;

namespace SimpleLib.Timing
{
    public class FrameManager
    {
        private Stopwatch _sw = new Stopwatch();

        private int _targetFramerate = 0;
        private double _preciseTargetDelta = 0.0;

        public int FramerateTarget { get => _targetFramerate; set { _targetFramerate = value; _preciseTargetDelta = 1.0 / (double)value; } }

        public FrameManager()
        {
            DebugTimers.StartTimer("Runtime.Run");
        }

        public void MeasureAndWait()
        {
            _sw.Stop();

            double dur = (((double)_sw.ElapsedTicks) / ((double)Stopwatch.Frequency));
            ulong ns = (ulong)(Math.Clamp(_preciseTargetDelta - dur, -_preciseTargetDelta, _preciseTargetDelta) * 1000000000.0);

            DeltaTimeDP = dur;
            DeltaTime = (float)dur;

            _sw.Restart();

            if (_targetFramerate > 0)
            {
                SDL3.SDL3.SDL_DelayPrecise(ns);
            }

            DebugTimers.StopTimer();
            DebugTimers.FrameEnd();
            DebugTimers.StartTimer("Runtime.Run");
        }

        public static double DeltaTimeDP { get; private set; } = 0.0;
        public static float DeltaTime { get; private set; } = 0.0f;
    }
}
