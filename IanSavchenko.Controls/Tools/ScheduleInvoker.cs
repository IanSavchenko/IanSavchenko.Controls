using System;
using System.Threading;
using Windows.UI.Core;

namespace IanSavchenko.Controls.Tools
{
    internal class ScheduleInvoker
    {
        private readonly CoreDispatcher _dispatcher;
        private readonly Timer _timer;
        private Action _action;

        public ScheduleInvoker(CoreDispatcher dispatcher = null)
        {
            _dispatcher = dispatcher;
            _timer = new Timer(TimerCallback, null, Timeout.Infinite, Timeout.Infinite);
        }

        public void Schedule(TimeSpan timeSpan, Action action)
        {
            Stop();
            _action = action;
            _timer.Change(timeSpan, Timeout.InfiniteTimeSpan);
        }

        public void Stop()
        {
            _timer.Change(Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
        }

        private void TimerCallback(object state)
        {
            if (_dispatcher != null)
            {
                _dispatcher.RunAsync(CoreDispatcherPriority.Normal, (() => _action())).AsTask().ConfigureAwait(false);
            }
            else
            {
                _action.Invoke();
            }
        }
    }
}
