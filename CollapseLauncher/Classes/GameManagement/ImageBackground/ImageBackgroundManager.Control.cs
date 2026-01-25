using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
#pragma warning disable IDE0130

#nullable enable
namespace CollapseLauncher.GameManagement.ImageBackground;

public partial class ImageBackgroundManager
{
    private bool _isPausedByUser;

    public void SetWindowFocusedEvent()
    {
        Play(false);
    }

    public void SetWindowUnfocusedEvent()
    {
        Pause(false);
    }

    public void Play(bool isUserRequest = true)
    {
        // Block play request for window event if paused by user
        if (!isUserRequest && _isPausedByUser)
        {
            return;
        }

        Interlocked.Exchange(ref _isPausedByUser, false);
        CurrentBackgroundElement?.Play();
    }

    public void Pause(bool isUserRequest = true)
    {
        if (isUserRequest)
        {
            Interlocked.Exchange(ref _isPausedByUser, true);
        }

        CurrentBackgroundElement?.Pause();
    }
}
