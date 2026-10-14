using Windows.Media.Playback;

#nullable enable
namespace CollapseLauncher.XAMLs.Theme.CustomControls;

public partial class LayeredBackgroundImage
{
    public void ToggleMute(bool isMute)
    {
        if (!TryGetBackgroundSourceContext(out _, out MediaPlayer? player))
            return;

        if (isMute)
        {
            player.IsMuted = true;
            return;
        }

        player.IsMuted = false;
    }

    public void SetVolume(double volume)
    {
        if (!TryGetBackgroundSourceContext(out SourceContext? sourceContext, out _))
            return;

        sourceContext.SetVolume(volume);
    }

    public void Play()
    {
        if (!TryGetBackgroundSourceContext(out SourceContext? sourceContext, out MediaPlayer? player))
            return;

        player.Play();
        MediaPlayer_RaiseStateUpdate(player);
        sourceContext.SetVolume(AudioVolume);
    }

    public void Pause()
    {
        if (!TryGetBackgroundSourceContext(out SourceContext? sourceContext, out MediaPlayer? player))
            return;

        // Ignore if the player is already in paused, closed or stopped state.
        if (player.CurrentState
            is MediaPlayerState.Paused
            or MediaPlayerState.Closed
            or MediaPlayerState.Stopped)
            return;

        // Use SourceContext's SetVolume onCompleted event to perform fade-out then pause action.
        sourceContext.SetVolume(0d, () =>
        {
            player.Pause();
            MediaPlayer_RaiseStateUpdate(player);
        });
    }

    public void RestoreViewAllocation()
    {
        LoadAllPresenters();
    }

    public void ReleaseViewAllocation()
    {
        UnloadAllPresenters();
    }

    private void MediaPlayer_RaiseStateUpdate(MediaPlayer player)
    {
        DispatcherQueue.TryEnqueue(() =>
        {
            SetValue(MediaStateProperty, player.CurrentState);
        });
    }
}
