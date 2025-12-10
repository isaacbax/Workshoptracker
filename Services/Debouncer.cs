using System;
using System.Threading;

namespace DesignSheet.Services;

public sealed class Debouncer : IDisposable
{
    private readonly TimeSpan _delay;
    private Timer? _timer;

    public Debouncer(TimeSpan delay)
    {
        _delay = delay;
    }

    public void Execute(Action action)
    {
        // Reset the timer; only the last call after the delay will run.
        _timer?.Dispose();
        _timer = new Timer(_ =>
        {
            try
            {
                action();
            }
            catch
            {
                // swallow – we don't want timer exceptions crashing the app
            }
        }, null, _delay, Timeout.InfiniteTimeSpan);
    }

    public void Dispose()
    {
        _timer?.Dispose();
    }
}
