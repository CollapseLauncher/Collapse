﻿using Microsoft.UI.Xaml;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Threading;
using static Hi3Helper.Shared.Region.LauncherConfig;

namespace CollapseLauncher.Interfaces
{
    internal class GamePropertyBase<T1, T2> : IAssetEntry<T1> where T1 : Enum
    {
        private string _gamePathField { get; init; }
        private GameVersion _gameVersionOverride { get; init; }

        public GamePropertyBase(UIElement parentUI, IGameVersionCheck gameVersionManager, IGameSettings gameSettings, string gamePath, string gameRepoURL, string versionOverride)
            : this(parentUI, gameVersionManager, gamePath, gameRepoURL, versionOverride)
        {
            _gameSettings = gameSettings;
        }

        public GamePropertyBase(UIElement parentUI, IGameVersionCheck gameVersionManager, string gamePath, string gameRepoURL, string versionOverride)
        {
            _gameVersionManager = gameVersionManager;
            _parentUI = parentUI;
            _gamePathField = gamePath;
            _gameRepoURL = gameRepoURL;
            _token = new CancellationTokenSource();
            AssetEntry = new ObservableCollection<AssetProperty<T1>>();

            // If the version override is not null, then assign the override value
            if (_isVersionOverride = versionOverride != null)
            {
                _gameVersionOverride = new GameVersion(versionOverride);
            }
        }

        protected const int _bufferLength = 4 << 10;
        protected const int _bufferBigLength = 1 << 20;
        protected const int _sizeForMultiDownload = 10 << 20;
        protected const string _userAgent = "UnityPlayer/2017.4.18f1 (UnityWebRequest/1.0, libcurl/7.51.0-DEV)";

        protected bool _isVersionOverride { get; init; }
        protected byte _downloadThreadCount { get => (byte)AppCurrentDownloadThread; }
        protected byte _threadCount { get => (byte)AppCurrentThread; }
        protected CancellationTokenSource _token { get; set; }
        protected Stopwatch _stopwatch { get; set; }
        protected Stopwatch _refreshStopwatch { get; set; }
        protected GameVersion _gameVersion { get => _isVersionOverride ? _gameVersionOverride : _gameVersionManager.GetGameExistingVersion().Value; }
        protected IGameVersionCheck _gameVersionManager { get; set; }
        protected IGameSettings _gameSettings { get; set; }
        protected string _gamePath { get => string.IsNullOrEmpty(_gamePathField) ? _gameVersionManager.GameDirPath : _gamePathField; }
        protected string _gameRepoURL { get; set; }
        protected List<T2> _assetIndex { get; set; }
        protected bool _useFastMethod { get; set; }

        public ObservableCollection<AssetProperty<T1>> AssetEntry { get; set; }
        public UIElement _parentUI { get; init; }
    }
}
