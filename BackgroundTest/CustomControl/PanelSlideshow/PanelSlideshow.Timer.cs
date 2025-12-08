using Microsoft.UI.Xaml.Controls;
using System;
using System.Threading.Tasks;
using System.Timers;

namespace BackgroundTest.CustomControl.PanelSlideshow;

public partial class PanelSlideshow : Control
{
    #region Fields

    private volatile Timer? timer = null;
    private double m_timer = 0;

    #endregion

    #region Methods

    private async void RestartTimer(double newDuration, double countIntervalMs = 100d, double delayBeforeStartMs = 500d)
    {
        using (_atomicLock.EnterScope())
        {
            m_timer = newDuration;

            if (!IsLoaded || newDuration == 0)
            {
                DisposeAndDeregisterTimer();
                return;
            }

            DisposeAndDeregisterTimer();

            timer = new Timer(countIntervalMs);
            timer.Elapsed += Timer_Elapsed;
        }

        await Task.Delay(TimeSpan.FromMilliseconds(delayBeforeStartMs));

        if (!_isMouseHover)
        {
            timer.Start();
        }
    }

    private void DisposeAndDeregisterTimer()
    {
        if (timer != null)
        {
            timer.Elapsed -= Timer_Elapsed;
        }

        if (_countdownProgressBar != null)
        {
            if (DispatcherQueue.HasThreadAccess)
            {
                _countdownProgressBar.Value = 0;
            }
            else
            {
                DispatcherQueue.TryEnqueue(() =>
                _countdownProgressBar.Value = 0);
            }
        }

        timer?.Stop();
        timer?.Dispose();
        timer = null;
    }

    private void Timer_Elapsed(object? sender, ElapsedEventArgs e)
    {
        if (sender is not Timer thisTimer)
        {
            return;
        }

        double interval = thisTimer.Interval / 1000d;
        if (DispatcherQueue.HasThreadAccess)
        {
            Impl();
        }
        else
        {
            DispatcherQueue.TryEnqueue(Impl);
        }

        return;

        void Impl()
        {
            _countdownProgressBar.Value += interval;
            m_timer -= interval;
            if (m_timer < 0)
            {
                ItemIndex++;
                return;
            }
        }
    }

    /// <summary>
    /// Stops the slideshow countdown timer.
    /// </summary>
    public void PauseSlideshow() => timer?.Stop();

    /// <summary>
    /// Resumes the slideshow countdown timer.
    /// </summary>
    public void ResumeSlideshow() => timer?.Start();

    #endregion
}
