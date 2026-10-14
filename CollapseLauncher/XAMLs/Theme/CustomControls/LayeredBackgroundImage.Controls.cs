using CollapseLauncher.XAMLs.Theme.CustomControls.VideoPresenter;
using System.Linq;
using Windows.Media.Playback;

#nullable enable
namespace CollapseLauncher.XAMLs.Theme.CustomControls;

public partial class LayeredBackgroundImage
{
    public void ToggleMute(bool isMute)
    {
        if (_backgroundGrid == null! ||
            !TryGetBackgroundSourceContext(out SourceContext? sourceContext,
                                           out MediaPlayer? player))
        {
            return;
        }

        if (isMute)
        {
            sourceContext.SetVolume(0d, () => player.IsMuted = true);
            return;
        }

        player.IsMuted = false;
        player.Volume  = 0;
        sourceContext.SetVolume(AudioVolume / 100d);
    }

    public void SetVolume(double volume)
    {
        if (_backgroundGrid == null! ||
            _backgroundGrid.Children.LastOrDefault() is not MediaFoundationPresenter
            {
                Tag: SourceContext sourceContext
            })
        {
            return;
        }

        sourceContext.SetVolume(volume / 100d);
    }

    public void Play()
    {
        if (_backgroundGrid == null! ||
            _backgroundGrid.Children.LastOrDefault() is not MediaFoundationPresenter
            {
                Tag: SourceContext
                {
                    Player: { } player
                } sourceContext
            })
        {
            return;
        }

        player.Play();
        MediaPlayer_RaiseStateUpdate(player);
        sourceContext.SetVolume(AudioVolume);
    }

    public void Pause()
    {
        if (_backgroundGrid == null! ||
            _backgroundGrid.Children.LastOrDefault() is not MediaFoundationPresenter
            {
                Tag: SourceContext
                {
                    Player: { } player
                } sourceContext
            })
        {
            return;
        }

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
