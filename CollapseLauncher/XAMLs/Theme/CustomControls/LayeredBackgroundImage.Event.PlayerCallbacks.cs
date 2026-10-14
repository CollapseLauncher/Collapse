using System;
using Windows.Media.Playback;

namespace CollapseLauncher.XAMLs.Theme.CustomControls;

public partial class LayeredBackgroundImage
{
    private void MediaPlayer_OnCurrentStateChanged(MediaPlayer player, object args)
    {
        MediaPlayerState value = player.CurrentState;
        DispatcherQueue.TryEnqueue(() =>
        {
            // Avoid being triggered multiple times.
            if ((MediaPlayerState)GetValue(MediaStateProperty) == value)
                return;

            SetValue(MediaStateProperty, value);
        });
    }

    private void MediaPlayer_OnPositionChanged(SourceContext context, TimeSpan args)
    {
        TimeSpan value = args;
        DispatcherQueue.TryEnqueue(() => SetValue(MediaDurationPositionProperty, value));
    }
}
