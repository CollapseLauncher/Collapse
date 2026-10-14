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
            CurrentBackgroundElement?.Play();
            return;
        }

        CurrentBackgroundElement?.SetVolume(GlobalBackgroundAudioVolume);
    }

    public void SetWindowUnfocusedEvent()
    {
        if (!GlobalKeepPlayVideoWhenWindowUnfocused)
        {
            CurrentBackgroundElement?.Pause();
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
        // Block play request for window event if paused by user
        if (!isUserRequest && (_isPausedByUser || !CurrentIsEnableBackgroundAutoPlay))
        {
            return;
        }

        if (isUserRequest)
        {
            CurrentIsEnableBackgroundAutoPlay = true;
        }
        
        // Alter to load static image if it has one instead of pausing.
        LayeredImageBackgroundContext? context        = CurrentSelectedBackgroundContext;
        bool                           hasStaticImage = !string.IsNullOrEmpty(context?.BackgroundImageStaticPath);
        if (isUserRequest && hasStaticImage && context != null)
        {
            LoadImageAtIndex(CurrentSelectedBackgroundIndex, token: CancellationToken.None);
        }
        
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

        // Alter to load static image if it has one instead of pausing.
        LayeredImageBackgroundContext? context        = CurrentSelectedBackgroundContext;
        bool                           hasStaticImage = !string.IsNullOrEmpty(context?.BackgroundImageStaticPath);
        if (isUserRequest && hasStaticImage && context != null)
        {
            LoadImageAtIndex(CurrentSelectedBackgroundIndex, token: CancellationToken.None);
            return;
        }

        CurrentBackgroundElement?.Pause();
    }
}
