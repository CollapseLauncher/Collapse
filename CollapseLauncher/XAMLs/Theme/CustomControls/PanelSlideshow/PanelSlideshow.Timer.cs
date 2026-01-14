using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;
using Timer = System.Timers.Timer;

#nullable enable
namespace CollapseLauncher.XAMLs.Theme.CustomControls.PanelSlideshow;

public partial class PanelSlideshow
{
    #region Fields

    private volatile Timer? _timer;
    private          double _mTimer;

    #endregion

    #region Methods

    private void RestartTimer(double newDuration, double countIntervalMs = 100d, int delayBeforeStartMs = 1000)
    {
        if (!IsLoaded || newDuration == 0)
        {
            DisposeAndDeregisterTimer();
            return;
        }

        new Thread(Impl)
        {
            IsBackground = true
        }.Start();
        return;

        async void Impl()
        {
            try
            {
                using (_atomicLock.EnterScope())
                {
                    _mTimer = newDuration;

                    DisposeAndDeregisterTimer();

                    _timer         =  new Timer(countIntervalMs);
                    _timer.Elapsed += Timer_Elapsed;
                }

                await Task.Delay(delayBeforeStartMs);

                DispatcherQueue.TryEnqueue(DispatcherQueuePriority.Low,
                                           () => VisualStateManager.GoToState(this, StateNameCountdownProgressBarFadeIn, true));
                if (!_isMouseHover && _timer != null)
                {
                    _timer.Start();
                }
            }
            catch
            {
                // ignored
            }
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
            DispatcherQueue.TryEnqueue(DispatcherQueuePriority.Low,
                                       () => _countdownProgressBar.Value = 0);
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
        PerformIntervalDecrement();
        return;

        // ReSharper disable once AsyncVoidMethod
        void PerformIntervalDecrement()
        {
            try
            {
                DispatcherQueue.TryEnqueue(DispatcherQueuePriority.Low,
                                           () =>
                                           {
                                               try
                                               {
                                                   _countdownProgressBar.Value += interval;
                                               }
                                               catch
                                               {
                                                   // ignored
                                               }
                                           });
            }
            catch
            {
                // ignored
            }

            _mTimer -= interval;
            if (!(_mTimer < 0))
            {
                return;
            }

            thisTimer.Stop();

            try
            {
                DispatcherQueue.TryEnqueue(DispatcherQueuePriority.Low,
                                           async void () =>
                                           {
                                               try
                                               {
                                                   VisualStateManager.GoToState(this, StateNameCountdownProgressBarFadeOut,
                                                                                    true);
                                                   await Task.Delay(500);

                                                   ItemIndex++;
                                               }
                                               catch
                                               {
                                                   // ignored
                                               }
                                           });
            }
            catch
            {
                // ignored
            }
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
