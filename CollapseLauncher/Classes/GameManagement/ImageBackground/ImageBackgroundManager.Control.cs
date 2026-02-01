using CollapseLauncher.XAMLs.Theme.CustomControls.LayeredBackgroundImage;
using System.Threading;
// ReSharper disable ConvertIfStatementToSwitchStatement
// ReSharper disable CheckNamespace
#pragma warning disable IDE0130

#nullable enable
namespace CollapseLauncher.GameManagement.ImageBackground;

public partial class ImageBackgroundManager
{
    private bool _isPausedByUser;

    public void SetWindowFocusedEvent()
    {
        if (GlobalKeepPlayVideoWhenWindowUnfocused)
        {
            CurrentBackgroundElement?.FadeInAudio();
        }
        else
        {
            Play(false);
        }
    }

    public void SetWindowUnfocusedEvent()
    {
        if (GlobalKeepPlayVideoWhenWindowUnfocused)
        {
            CurrentBackgroundElement?.FadeOutAudio();
        }
        else
        {
            Pause(false);
        }
    }

    public void SetWindowMinimizeEvent() => Pause(false);

    public void SetWindowRestoreEvent() => Play(false);

    public void Play(bool isUserRequest = true)
    {
        // Block play request for window event if paused by user
        if (!isUserRequest && _isPausedByUser)
        {
            return;
        }

        if (isUserRequest)
        {
            CurrentIsEnableBackgroundAutoPlay = true;
        }

        // Force to restore autoplay status to true.
        CurrentBackgroundElement?.SetValue(LayeredBackgroundImage.IsVideoAutoplayProperty, true);

        Interlocked.Exchange(ref _isPausedByUser, false);
        CurrentBackgroundElement?.Play();
    }

    public void Pause(bool isUserRequest = true)
    {
        if (isUserRequest)
        {
            Interlocked.Exchange(ref _isPausedByUser, true);
            CurrentIsEnableBackgroundAutoPlay = false;
        }

        CurrentBackgroundElement?.Pause();
    }
}
