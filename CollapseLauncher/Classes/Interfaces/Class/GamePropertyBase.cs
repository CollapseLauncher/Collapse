using CollapseLauncher.Extension;
using Hi3Helper.Plugin.Core.Management;
using Microsoft.UI.Xaml;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics.CodeAnalysis;
using static Hi3Helper.Shared.Region.LauncherConfig;

namespace CollapseLauncher.Interfaces
{
    internal class GamePropertyBase<T1> : IAssetEntry
    {
        private string      GamePathField       { get; }
        private GameVersion GameVersionOverride { get; }

#nullable enable
        public GamePropertyBase(UIElement parentUI, IGameVersion? gameVersionManager, IGameSettings? gameSettings, string? gamePath, string? gameRepoURL, string? versionOverride)
        {
            GameSettings = gameSettings;
            GameVersionManager = gameVersionManager;
            ParentUI = parentUI;
            GamePathField = gamePath;
            GameRepoURL = gameRepoURL;
            Token = new CancellationTokenSourceWrapper();
            IsVersionOverride = versionOverride != null;

            // If the version override is not null, then assign the override value
            if (IsVersionOverride)
            {
                GameVersionOverride = new GameVersion(versionOverride);
            }

            AssetEntry = [];
        }

        public GamePropertyBase(UIElement parentUI, IGameVersion? gameVersionManager, string? gamePath, string? gameRepoURL, string? versionOverride)
            : this(parentUI, gameVersionManager, null, gamePath, gameRepoURL, versionOverride) { }
#nullable restore

        protected const   int    BufferMediumLength                        = 1 << 20; // 1 MiB
        protected const   int    BufferBigLength                           = 2 << 20; // 2 MiB
        protected const   double DownloadThreadCountReservedMultiplication = 1.5d;
        protected virtual string UserAgent { get; set; } = "UnityPlayer/2017.4.18f1 (UnityWebRequest/1.0, libcurl/7.51.0-DEV)";

        protected static bool                           IsBurstDownloadEnabled          { get => IsBurstDownloadModeEnabled; }
        protected static int                            DownloadThreadCount             { get => AppCurrentDownloadThread; }
        protected static int                            DownloadThreadWithReservedCount { get => (int)Math.Round(DownloadThreadCount * DownloadThreadCountReservedMultiplication); }
        protected static int                            ThreadCount                     { get => (byte)AppCurrentThread; }
        protected        bool                           IsVersionOverride               { get; init; }
        protected        CancellationTokenSourceWrapper Token                           { get; set; }
        protected GameVersion GameVersion
        {
            get
            {
                if (GameVersionManager != null && IsVersionOverride)
                {
                    return GameVersionOverride;
                }
                return GameVersionManager?.GetGameExistingVersion() ?? throw new NullReferenceException();
            }
        }

        protected IGameVersion GameVersionManager { get; set; }
        protected IGameSettings GameSettings { get; set; }
        protected string GamePath { get => string.IsNullOrEmpty(GamePathField) ? GameVersionManager.GameDirPath : GamePathField; }

        [field: MaybeNull, AllowNull]
        protected string GameRepoURL
        {
            get => string.IsNullOrEmpty(field) ? GameVersionManager.GameApiProp?.data?.game?.latest?.decompressed_path : field;
            set;
        }

        protected List<T1> AssetIndex { get; set; }
        protected bool UseFastMethod { get; set; }

        public ObservableCollection<IAssetProperty> AssetEntry { get; set; }
        public UIElement ParentUI { get; init; }
    }
}
