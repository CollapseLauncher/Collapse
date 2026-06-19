using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media.Animation;
using System;
using System.Threading;
using System.Threading.Tasks;

#nullable enable
namespace CollapseLauncher.XAMLs.Theme.CustomControls;

public partial class PanelSlideshow
{
    #region Fields

    private Storyboard? _timerStoryboard;

    #endregion

    #region Methods

    // ReSharper disable once AsyncVoidMethod
    private async void RestartTimer(double newDurationSeconds, int delayBeforeStartMs = 1000)
    {
        try
        {
            if (!IsLoaded ||
                !_isTemplateLoaded ||
                newDurationSeconds == 0)
            {
                if (newDurationSeconds == 0)
                {
                    _countdownProgressBar.Value = 0;
                }
                DisposeAndDeregisterTimer();

                return;
            }

            if (_timerStoryboard != null)
            {
                return;
            }

            _countdownProgressBar.Minimum = 0;
            _countdownProgressBar.Maximum = 1;

            Interlocked.Exchange(ref _timerStoryboard, new Storyboard());
            DoubleAnimation animation = new()
            {
                Duration                 = new Duration(TimeSpan.FromSeconds(newDurationSeconds)),
                From                     = 0d,
                To                       = 1d,
                EnableDependentAnimation = true
            };
            Storyboard.SetTarget(animation, _countdownProgressBar);
            Storyboard.SetTargetProperty(animation, "Value");

            _timerStoryboard?.Children.Add(animation);

            await Task.Delay(delayBeforeStartMs);
            VisualStateManager.GoToState(this, StateNameCountdownProgressBarFadeIn, true);

            _timerStoryboard?.Completed += TimerStoryboardOnCompleted;
            if (!_isMouseHover)
            {
                _timerStoryboard?.Begin();
            }
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
        }
        return;

        async void TimerStoryboardOnCompleted(object? sender, object e)
        {
            try
            {
                if (sender is not Storyboard storyboard)
                {
                    return;
                }

                storyboard.Completed -= TimerStoryboardOnCompleted;
                await Task.Delay(150);
                VisualStateManager.GoToState(this, StateNameCountdownProgressBarFadeOut, true);
                await Task.Delay(500);

                DisposeAndDeregisterTimer();
                ItemIndex++;
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
            }
        }
    }

    /// <summary>
    /// Stops the slideshow countdown timer.
    /// </summary>
    public void PauseSlideshow() => _timerStoryboard?.Pause();

    /// <summary>
    /// Resumes the slideshow countdown timer.
    /// </summary>
    public void ResumeSlideshow()
    {
        if (_timerStoryboard?.GetCurrentState() == ClockState.Stopped)
        {
            _timerStoryboard?.Begin();
            return;
        }

        _timerStoryboard?.Resume();
    }

    /// <summary>
    /// Resets the slideshow countdown timer.
    /// </summary>
    public void ResetSlideshow()
    {
        _timerStoryboard?.Seek(TimeSpan.FromSeconds(0));
    }

    private void DisposeAndDeregisterTimer()
    {
        Storyboard? oldStoryboard = Interlocked.Exchange(ref _timerStoryboard, null);
        oldStoryboard?.Stop();
        oldStoryboard?.Children?.Clear();
    }

    #endregion
}
