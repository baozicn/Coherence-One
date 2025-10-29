using LiteFocus.Interop;
using System;
using System.Timers;

namespace LiteFocus.Services
{
    public class IdleWatcher : IDisposable
    {
        public event Action<TimeSpan>? OnIdleThresholdReached;
        private readonly System.Timers.Timer _timer = new(1000);
        private readonly TimeSpan _threshold = TimeSpan.FromSeconds(60);
        private bool _signaled = false;

        public IdleWatcher()
        {
            _timer.Elapsed += (_, __) => Check();
            _timer.Start();
        }

        private void Check()
        {
            var idle = GetIdleTime();
            if (idle >= _threshold)
            {
                if (!_signaled)
                {
                    _signaled = true;
                    OnIdleThresholdReached?.Invoke(idle);
                }
            }
            else
            {
                _signaled = false;
            }
        }

        public static TimeSpan GetIdleTime()
        {
            var info = new LASTINPUTINFO { cbSize = (uint)System.Runtime.InteropServices.Marshal.SizeOf(typeof(LASTINPUTINFO)) };
            if (NativeMethods.GetLastInputInfo(ref info))
            {
                uint idleTicks = (uint)Environment.TickCount - info.dwTime;
                return TimeSpan.FromMilliseconds(idleTicks);
            }
            return TimeSpan.Zero;
        }

        public void Dispose() => _timer.Dispose();
    }
}
