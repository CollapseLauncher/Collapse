using System.Threading;

// ReSharper disable ConvertIfStatementToSwitchStatement
// ReSharper disable CheckNamespace
#pragma warning disable IDE0130

#nullable enable
namespace CollapseLauncher.GameManagement.ImageBackground;

public partial class ImageBackgroundManager
{
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
        // Trynna spawn the background element if not loaded from first start-up.
        if (CurrentBackgroundElement == null)
            LoadImage();

        CurrentBackgroundElement?.RestoreViewAllocation();
    }

    public void Play(bool isUserRequest = true)
    {
        if (isUserRequest)
        {
            CurrentIsEnableBackgroundAutoPlay = true;
        }

        // Alter to restore video if it has static image.
        if (isUserRequest && !string.IsNullOrEmpty(CurrentSelectedBackgroundContext?.OriginPlaceholderImagePath))
        {
            LoadImage();
            return;
        }

        CurrentBackgroundElement?.Play();
    }

    public void Pause(bool isUserRequest = true)
    {
        if (isUserRequest)
        {
            CurrentIsEnableBackgroundAutoPlay = false;
        }

        // Alter to load static image if it has one instead of pausing.
        if (isUserRequest && !string.IsNullOrEmpty(CurrentSelectedBackgroundContext?.OriginPlaceholderImagePath))
        {
            LoadImage();
            return;
        }

        CurrentBackgroundElement?.Pause();
    }
}
