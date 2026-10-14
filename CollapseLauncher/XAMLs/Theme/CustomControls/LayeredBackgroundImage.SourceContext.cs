using CollapseLauncher.XAMLs.Theme.CustomControls.VideoPresenter;
using Hi3Helper;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;
using Windows.Foundation;
using Windows.Media.Playback;
using WinRT;
// ReSharper disable InconsistentNaming

#nullable enable
namespace CollapseLauncher.XAMLs.Theme.CustomControls;

public partial class LayeredBackgroundImage
{
    private readonly Dictionary<TimeSpan, TimeSpan> LastMediaSourcePosition = [];

    public partial class SourceContext : IDisposable
    {
        private static readonly ConcurrentDictionary<SourceContext, bool> LoadedContexts = [];

        private static readonly Guid IID_IMediaPlayer5 = new("cfe537fd-f86a-4446-bf4d-c8e792b7b4b3");

        public ulong Generation;

        public object? Source          { get; set; }
        public bool    IsDisposeSource { get; set; }

        public LayeredBackgroundImage ParentElement { get; init; }

        private nint _playerAbi;
        public MediaPlayer? Player
        {
            get;
            set
            {
                nint oldPlayerAbi = Interlocked.Exchange(ref _playerAbi, nint.Zero);
                if (oldPlayerAbi != nint.Zero) Marshal.Release(oldPlayerAbi);

                if (value == null)
                {
                    field?.MediaOpened -= PlayerOnUpdateCanvasSize;
                }
                else
                {
                    value.MediaOpened += PlayerOnUpdateCanvasSize;
                }

                (value as IWinRTObject)?.NativeObject?.TryAs(IID_IMediaPlayer5, out _playerAbi);
                field = value;
            }
        }

        public TypedEventHandler<MediaPlayer, object>? CurrentStateChangedCallback { get; set; }
        public TypedEventHandler<SourceContext, TimeSpan>? PositionChangedCallback { get; set; }


        public object? MediaSource { get; set; }

        public MediaFoundationPresenter? PlayerPresenter
        {
            get;
            set
            {
                if (value == null)
                {
                    Player?.VideoFrameAvailable -= PlayerOnVideoFrameAvailable;
                }
                else
                {
                    Player?.VideoFrameAvailable += PlayerOnVideoFrameAvailable;
                }

                field = value;
            }
        }

        public CancellationTokenSource SourceLoadingCts = new();

        private CancellationTokenSource? _volumeChangedCts;
        private bool                     _disposed;

        private int _cachedPlayerWidth;
        private int _cachedPlayerHeight;

        public SourceContext(LayeredBackgroundImage parentElement, ulong generation)
        {
            LoadedContexts.TryAdd(this, true);
            ParentElement = parentElement;
            Generation    = generation;
        }

        private void PlayerOnUpdateCanvasSize(MediaPlayer sender, object args)
        {
            _cachedPlayerWidth  = (int)sender.PlaybackSession.NaturalVideoWidth;
            _cachedPlayerHeight = (int)sender.PlaybackSession.NaturalVideoHeight;
        }

        private void PlayerOnVideoFrameAvailable(MediaPlayer sender, object args)
        {
            PlayerPresenter?.DrawUnsafe(DrawConsumer, _cachedPlayerWidth, _cachedPlayerHeight);
            return;

            unsafe void DrawConsumer(nint surfaceAbi)
                => ((delegate* unmanaged[MemberFunction]<nint, nint, int>)(*(*(void***)_playerAbi + 10)))(_playerAbi, surfaceAbi);
        }

        public void RegisterPlayerCallbacks(
            TypedEventHandler<MediaPlayer, object>     currentStateChangedCallback,
            TypedEventHandler<SourceContext, TimeSpan> positionChangedCallback)
        {
            if (Player is not { } player)
            {
                return;
            }

            // -- Un-register previous callbacks before assigning a new one.
            UnregisterPlayerCallbacks();

            player.CurrentStateChanged += CurrentStateChangedCallback = currentStateChangedCallback;
            PositionChangedCallback    =  positionChangedCallback;
        }

        public void UnregisterPlayerCallbacks()
        {
            if (Player is not { } player)
            {
                return;
            }

            if (CurrentStateChangedCallback != null) player.CurrentStateChanged -= CurrentStateChangedCallback;
            PositionChangedCallback = null;
        }

        public void SetVolume(double volume, Action? onCompleted = null)
        {
            if (Player is null)
            {
                onCompleted?.Invoke();
                return;
            }

            if (Player.IsMuted)
            {
                Player.Volume = volume;
                onCompleted?.Invoke();
                return;
            }

            CancellationToken token = CancellationToken.None;
            if (Interlocked.Exchange(ref _volumeChangedCts, new CancellationTokenSource()) is { } lastCts)
            {
                lastCts.Cancel();
                lastCts.Dispose();
                token = _volumeChangedCts.Token;
            }

            Player.SetVolumeFadeCore(Player.Volume, volume, onCompleted: onCompleted, coopToken: token);
        }

        public void SavePlayerLastPosition()
        {
            if (Player is not { } player)
            {
                return;
            }

            ParentElement.LastMediaSourcePosition[player.NaturalDuration] = player.Position;
        }

        public TimeSpan LoadPlayerLastPosition()
        {
            if (Player is not { } player)
            {
                return TimeSpan.Zero;
            }

            return ParentElement.LastMediaSourcePosition.GetValueOrDefault(player.NaturalDuration);
        }

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, true))
                return;

            LoadedContexts.TryRemove(this, out _);
            Console.WriteLine($"Total Remained SourceContext instances: {LoadedContexts.Count} instances", LogType.Debug);

            GC.SuppressFinalize(this); // Early suppress finalization here.

            _volumeChangedCts?.Cancel();
            _volumeChangedCts?.Dispose();
            _volumeChangedCts = null;

            SourceLoadingCts.Cancel();
            SourceLoadingCts.Dispose();
            SourceLoadingCts = null!;

            Player?.MediaOpened         -= PlayerOnUpdateCanvasSize;
            Player?.VideoFrameAvailable -= PlayerOnVideoFrameAvailable;
            Player?.Pause();

            UnregisterPlayerCallbacks();
            SavePlayerLastPosition();

            // Skidaddle Skidoddle, your COM object is now a noodle!
            // -- Early dispose the underneath ABI earlier for MediaPlayer and supposedly,
            //    WinRT's MediaSource objects.
            nint oldPlayerAbi = Interlocked.Exchange(ref _playerAbi, nint.Zero);
            if (oldPlayerAbi != nint.Zero) Marshal.Release(oldPlayerAbi);
            DisposeDisposableOrCOMObject(Player);
            Player = null;

            DisposeDisposableOrCOMObject(MediaSource);
            MediaSource = null;

            if (IsDisposeSource)
            {
                DisposeDisposableOrCOMObject(Source);
                Source = null;
            }

            DisposeDisposableOrCOMObject(PlayerPresenter);
            PlayerPresenter = null;
        }

        private static void DisposeDisposableOrCOMObject(object? obj)
        {
            if (obj is DependencyObject depsObj &&
                !depsObj.DispatcherQueue.HasThreadAccess)
            {
                depsObj.DispatcherQueue.TryEnqueue(DispatcherQueuePriority.High, () => (obj as IDisposable)?.Dispose());
            }
            else
            {
                (obj as IDisposable)?.Dispose();
            }

            (obj as IWinRTObject)?.NativeObject.Dispose();
        }
    }
}
