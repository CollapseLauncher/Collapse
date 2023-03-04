using CollapseLauncher.Statics;
using Hi3Helper.Preset;
using Microsoft.UI.Xaml;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Threading;
using static Hi3Helper.Shared.Region.LauncherConfig;

namespace CollapseLauncher.Interfaces
{
    internal class GamePropertyBase<T1, T2>
        where T1 : Enum
    {
        private string _gamePathField { get; init; }

        public GamePropertyBase(UIElement parentUI, string gamePath, string gameRepoURL, PresetConfigV2 gamePreset)
        {
            _parentUI = parentUI;
            _gamePathField = gamePath;
            _gameRepoURL = gameRepoURL;
            _gamePreset = gamePreset;
            _token = new CancellationTokenSource();
            AssetEntry = new ObservableCollection<AssetProperty<T1>>();
        }

        protected const int _refreshInterval = 33;
        protected const int _bufferLength = 4 << 10;
        protected const int _bufferBigLength = 1 << 20;
        protected const int _sizeForMultiDownload = 10 << 20;
        protected const string _userAgent = "UnityPlayer/2017.4.18f1 (UnityWebRequest/1.0, libcurl/7.51.0-DEV)";

        protected byte _downloadThreadCount { get => (byte)AppCurrentDownloadThread; }
        protected byte _threadCount { get => (byte)AppCurrentThread; }
        protected CancellationTokenSource _token { get; set; }
        protected UIElement _parentUI { get; init; }
        protected Stopwatch _stopwatch { get; set; }
        protected Stopwatch _refreshStopwatch { get; set; }
        protected string _gamePath { get => string.IsNullOrEmpty(_gamePathField) ? PageStatics._GameVersion.GameDirPath : _gamePathField; }
        protected string _gameRepoURL { get; set; }
        protected PresetConfigV2 _gamePreset { get; set; }
        protected GameVersion _gameVersion { get => PageStatics._GameVersion.GetGameExistingVersion().Value; }
        protected List<T2> _assetIndex { get; set; }

        public ObservableCollection<AssetProperty<T1>> AssetEntry { get; set; }
    }
}
