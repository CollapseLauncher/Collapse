using Microsoft.UI.Xaml;
using System.Threading.Tasks;
using System.Timers;

namespace BackgroundTest.CustomControl.PanelSlideshow;

public partial class PanelSlideshow
{
    #region Fields

    private volatile Timer? _timer;
    private          double _mTimer;

    #endregion

    #region Methods

    private async void RestartTimer(double newDuration, double countIntervalMs = 100d, int delayBeforeStartMs = 1000)
    {
        try
        {
            using (_atomicLock.EnterScope())
            {
                _mTimer = newDuration;

                if (!IsLoaded || newDuration == 0)
                {
                    DisposeAndDeregisterTimer();
                    return;
                }

                DisposeAndDeregisterTimer();

                _timer         =  new Timer(countIntervalMs);
                _timer.Elapsed += Timer_Elapsed;
            }

            await Task.Delay(delayBeforeStartMs);

            VisualStateManager.GoToState(this, StateNameCountdownProgressBarFadeIn, true);
            if (!_isMouseHover)
            {
                _timer.Start();
            }
        }
        catch
        {
            // ignored
        }
    }

    private void DisposeAndDeregisterTimer()
    {
        if (!_isTemplateLoaded)
        {
            return;
        }

        if (_timer != null)
        {
            _timer.Elapsed -= Timer_Elapsed;
        }

        if (DispatcherQueue.HasThreadAccess)
        {
            _countdownProgressBar.Value = 0;
        }
        else
        {
            DispatcherQueue.TryEnqueue(() => _countdownProgressBar.Value = 0);
        }

        _timer?.Stop();
        _timer?.Dispose();
        _timer = null;
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

        // ReSharper disable once AsyncVoidMethod
        async void Impl()
        {
            _countdownProgressBar.Value += interval;
            _mTimer -= interval;
            if (!(_mTimer < 0))
            {
                return;
            }

            thisTimer.Stop();
            VisualStateManager.GoToState(this, StateNameCountdownProgressBarFadeOut, true);
            await Task.Delay(500);
            ItemIndex++;
        }
    }

    /// <summary>
    /// Stops the slideshow countdown timer.
    /// </summary>
    public void PauseSlideshow() => _timer?.Stop();

    /// <summary>
    /// Resumes the slideshow countdown timer.
    /// </summary>
    public void ResumeSlideshow() => _timer?.Start();

    #endregion
}
