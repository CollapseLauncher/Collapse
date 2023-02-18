using Hi3Helper.Preset;
using Microsoft.UI.Xaml;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Threading;

namespace CollapseLauncher.Interfaces
{
    internal class GamePropertyBase<T1, T2>
        where T1 : Enum
    {
        public GamePropertyBase(UIElement parentUI, string gameVersion, string gamePath,
            string gameRepoURL, PresetConfigV2 gamePreset, byte repairThread, byte downloadThread)
        {
            _parentUI = parentUI;
            _gameVersion = new GameVersion(gameVersion);
            _gamePath = gamePath;
            _gameRepoURL = gameRepoURL;
            _gamePreset = gamePreset;
            _threadCount = repairThread;
            _downloadThreadCount = downloadThread;
            _token = new CancellationTokenSource();
            AssetEntry = new ObservableCollection<AssetProperty<T1>>();
        }

        protected const int _refreshInterval = 33;
        protected const int _bufferLength = 4 << 10;
        protected const int _bufferBigLength = 1 << 20;
        protected const int _sizeForMultiDownload = 10 << 20;
        protected const string _userAgent = "UnityPlayer/2017.4.18f1 (UnityWebRequest/1.0, libcurl/7.51.0-DEV)";

        protected byte _downloadThreadCount { get; set; }
        protected byte _threadCount { get; set; }
        protected CancellationTokenSource _token { get; set; }
        protected UIElement _parentUI { get; init; }
        protected Stopwatch _stopwatch { get; set; }
        protected Stopwatch _refreshStopwatch { get; set; }
        protected string _gamePath { get; init; }
        protected string _gameRepoURL { get; set; }
        protected PresetConfigV2 _gamePreset { get; set; }
        protected GameVersion _gameVersion { get; init; }
        protected List<T2> _assetIndex { get; set; }

        public ObservableCollection<AssetProperty<T1>> AssetEntry { get; set; }
    }
}
