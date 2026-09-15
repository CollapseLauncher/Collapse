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

            _countdownProgressBar.Minimum = 0d;
            _countdownProgressBar.Maximum = 1d;
            _countdownProgressBar.Value   = 0d;

            _timerStoryboard ??= new Storyboard();
            DoubleAnimationUsingKeyFrames keyframe = CreateLowFrequencyAnimation(
                 0d,
                 1d,
                 TimeSpan.FromSeconds(newDurationSeconds),
                 TimeSpan.FromSeconds(1));
            Storyboard.SetTarget(keyframe, _countdownProgressBar);
            Storyboard.SetTargetProperty(keyframe, "Value");

            _timerStoryboard?.Stop();
            _timerStoryboard?.Children.Clear();
            _timerStoryboard?.Children.Add(keyframe);

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

        static DoubleAnimationUsingKeyFrames CreateLowFrequencyAnimation(
            double   from,
            double   to,
            TimeSpan duration,
            TimeSpan frequencySecond)
        {
            var animation = new DoubleAnimationUsingKeyFrames
            {
                Duration                 = duration,
                EnableDependentAnimation = true
            };

            int steps = (int)(duration.TotalSeconds / frequencySecond.TotalSeconds);

            for (int i = 0; i <= steps; i++)
            {
                double   progress = (double)i / steps;
                TimeSpan keyTime  = TimeSpan.FromTicks(frequencySecond.Ticks * i);
                double   value    = from + (to - from) * progress;

                animation.KeyFrames.Add(CreateKeyFrame<DiscreteDoubleKeyFrame>(keyTime, value));
            }

            return animation;
        }

        static T CreateKeyFrame<T>(TimeSpan keyTime, double value) where T : DoubleKeyFrame, new()
            => new() { KeyTime = keyTime, Value = value };

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
