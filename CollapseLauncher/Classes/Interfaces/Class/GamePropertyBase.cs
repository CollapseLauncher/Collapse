using CollapseLauncher.Extension;
using Microsoft.UI.Xaml;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using static Hi3Helper.Shared.Region.LauncherConfig;

namespace CollapseLauncher.Interfaces
{
    internal class GamePropertyBase<T1> : IAssetEntry
    {
        private string _gamePathField { get; init; }
        private GameVersion _gameVersionOverride { get; init; }

#nullable enable
        public GamePropertyBase(UIElement parentUI, IGameVersionCheck? gameVersionManager, IGameSettings? gameSettings, string? gamePath, string? gameRepoURL, string? versionOverride)
        {
            _gameSettings = gameSettings;
            _gameVersionManager = gameVersionManager;
            _parentUI = parentUI;
            _gamePathField = gamePath;
            _gameRepoURL = gameRepoURL;
            _token = new CancellationTokenSourceWrapper();
            _isVersionOverride = versionOverride != null;

            // If the version override is not null, then assign the override value
            if (_isVersionOverride)
            {
                _gameVersionOverride = new GameVersion(versionOverride);
            }

            AssetEntry = [];
        }

        public GamePropertyBase(UIElement parentUI, IGameVersionCheck? gameVersionManager, string? gamePath, string? gameRepoURL, string? versionOverride)
            : this(parentUI, gameVersionManager, null, gamePath, gameRepoURL, versionOverride) { }
#nullable restore

        protected const int _bufferLength = 4 << 10; // 4 KiB
        protected const int _bufferMediumLength = 1 << 20; // 1 MiB
        protected const int _bufferBigLength = 2 << 20; // 2 MiB
        protected const int _sizeForMultiDownload = 10 << 20;
        protected const int _downloadThreadCountReserved = 16;
        protected virtual string _userAgent { get; set; } = "UnityPlayer/2017.4.18f1 (UnityWebRequest/1.0, libcurl/7.51.0-DEV)";

        protected bool _isVersionOverride { get; init; }
        protected bool _isBurstDownloadEnabled { get => IsBurstDownloadModeEnabled; }
        protected byte _downloadThreadCount { get => (byte)AppCurrentDownloadThread; }
        protected byte _threadCount { get => (byte)AppCurrentThread; }
        protected int _downloadThreadCountSqrt { get => (int)Math.Max(Math.Sqrt(_downloadThreadCount), 4); }
        protected CancellationTokenSourceWrapper _token { get; set; }
        protected GameVersion _gameVersion
        {
            get
            {
                if (_gameVersionManager != null && _isVersionOverride)
                {
                    return _gameVersionOverride;
                }
                return _gameVersionManager?.GetGameExistingVersion() ?? throw new NullReferenceException();
            }
        }

        protected IGameVersionCheck _gameVersionManager { get; set; }
        protected IGameSettings _gameSettings { get; set; }
        protected string _gamePath { get => string.IsNullOrEmpty(_gamePathField) ? _gameVersionManager.GameDirPath : _gamePathField; }
        protected string _gameRepoURL { get; set; }
        protected List<T1> _assetIndex { get; set; }
        protected bool _useFastMethod { get; set; }

        public ObservableCollection<IAssetProperty> AssetEntry { get; set; }
        public UIElement _parentUI { get; init; }
    }
}
