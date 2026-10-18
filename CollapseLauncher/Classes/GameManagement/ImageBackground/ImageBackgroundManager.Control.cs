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
        if (!GlobalKeepPlayVideoWhenWindowUnfocused)
        {
            Play(false);
            return;
        }

        CurrentBackgroundElement?.SetVolume(GlobalBackgroundAudioVolume / 100d);
    }

    public void SetWindowUnfocusedEvent()
    {
        if (!GlobalKeepPlayVideoWhenWindowUnfocused)
        {
            Pause(false);
            return;
        }

        CurrentBackgroundElement?.SetVolume(0);
    }

    public void SetWindowMinimizeEvent()
    {
        CurrentBackgroundElement?.ReleaseViewAllocation();
    }

    public void SetWindowRestoreEvent()
    {
        CurrentBackgroundElement?.RestoreViewAllocation();
    }

    public void Play(bool isUserRequest = true)
    {
        if (isUserRequest)
        {
            CurrentIsEnableBackgroundAutoPlay = true;
        }

        // Alter to load static image if it has one instead of pausing.
        LoadImage();
        CurrentBackgroundElement?.Play();
    }

    public void Pause(bool isUserRequest = true)
    {
        if (isUserRequest)
        {
            CurrentIsEnableBackgroundAutoPlay = false;
        }

        // Alter to load static image if it has one instead of pausing.
        LoadImage();
        CurrentBackgroundElement?.Pause();
    }
}
