using CollapseLauncher.GameSettings.Genshin;
using CollapseLauncher.GameSettings.Honkai;
using CollapseLauncher.GameSettings.StarRail;
using CollapseLauncher.GameVersioning;
using CollapseLauncher.InstallManager.Genshin;
using CollapseLauncher.InstallManager.Honkai;
using CollapseLauncher.InstallManager.StarRail;
using CollapseLauncher.Interfaces;
using Hi3Helper.Preset;
using Hi3Helper.Shared.ClassStruct;
using Microsoft.UI.Xaml;
using System;
using System.Collections.Generic;
using System.Linq;

namespace CollapseLauncher.Statics
{
    internal class GamePresetProperty
    {
        internal GamePresetProperty(UIElement UIElementParent, RegionResourceProp APIResouceProp, PresetConfigV2 GamePreset)
        {
            CopyGamePreset(GamePreset);
            _APIResouceProp = APIResouceProp;

            switch (GamePreset.GameType)
            {
                case GameType.Honkai:
                    _GameVersion = new GameTypeHonkaiVersion(UIElementParent, _APIResouceProp, _GamePreset);
                    _GameSettings = new HonkaiSettings(_GameVersion);
                    _GameCache = new HonkaiCache(UIElementParent, _GameVersion);
                    _GameRepair = new HonkaiRepair(UIElementParent, _GameVersion, _GameCache, _GameSettings);
                    _GameInstall = new HonkaiInstall(UIElementParent, _GameVersion, _GameCache);
                    break;
                case GameType.StarRail:
                    _GameVersion = new GameTypeStarRailVersion(UIElementParent, _APIResouceProp, _GamePreset);
                    _GameSettings = new StarRailSettings(_GameVersion);
                    _GameCache = new StarRailCache(UIElementParent, _GameVersion);
                    _GameRepair = new StarRailRepair(UIElementParent, _GameVersion);
                    _GameInstall = new StarRailInstall(UIElementParent, _GameVersion);
                    break;
                case GameType.Genshin:
                    _GameVersion = new GameTypeGenshinVersion(UIElementParent, _APIResouceProp, _GamePreset);
                    _GameSettings = new GenshinSettings(_GameVersion);
                    _GameCache = null;
                    _GameRepair = new GenshinRepair(UIElementParent, _GameVersion, _GameVersion.GameAPIProp.data.game.latest.decompressed_path);
                    _GameInstall = new GenshinInstall(UIElementParent, _GameVersion);
                    break;
                default:
                    throw new NotSupportedException($"[GamePresetProperty.Ctor] Game type: {GamePreset.GameType} ({GamePreset.ProfileName} - {GamePreset.ZoneName}) is not supported!");
            }
        }

        private static T[] CopyReturn<T>(T[] Source)
        {
            if (Source == null) return null;
            T[] returnNew = new T[Source.Length];
            Array.Copy(Source, returnNew, Source.Length);
            return returnNew;
        }

        private static List<T> CopyReturn<T>(IEnumerable<T> Source)
        {
            if (Source == null) return null;
            return new List<T>(Source);
        }

        #region Goofy Ah Copy Method. TODO: Use Generics for this :pepehands:
        private void CopyGamePreset(PresetConfigV2 GamePreset) => _GamePreset = new PresetConfigV2
        {
            ActualGameDataLocation = GamePreset.ActualGameDataLocation,
            BetterHi3LauncherVerInfoReg = GamePreset.BetterHi3LauncherVerInfoReg,
            ConvertibleTo = CopyReturn(GamePreset.ConvertibleTo),
            DispatcherKey = GamePreset.DispatcherKey,
            DispatcherKeyBitLength = GamePreset.DispatcherKeyBitLength,
            FallbackGameType = GamePreset.FallbackGameType,
            FallbackLanguage = GamePreset.FallbackLanguage,
            GameChannel = GamePreset.GameChannel,
            GameDefaultCVLanguage = GamePreset.GameDefaultCVLanguage,
            GameDirectoryName = GamePreset.GameDirectoryName,
            GameDispatchArrayURL = CopyReturn(GamePreset.GameDispatchArrayURL),
            GameDispatchChannelName = GamePreset.GameDispatchChannelName,
            GameDispatchDefaultName = GamePreset.GameDispatchDefaultName,
            GameDispatchURL = GamePreset.GameDispatchURL,
            GameDispatchURLTemplate = GamePreset.GameDispatchURLTemplate,
            GameExecutableName = GamePreset.GameExecutableName,
            GameGatewayDefault = GamePreset.GameGatewayDefault,
            GameGatewayURLTemplate = GamePreset.GameGatewayURLTemplate,
            GameName = GamePreset.GameName,
            GameSupportedLanguages = CopyReturn(GamePreset.GameSupportedLanguages),
            GameType = GamePreset.GameType,
            HashID = GamePreset.HashID,
            InternalGameNameFolder = GamePreset.InternalGameNameFolder,
            InternalGameNameInConfig = GamePreset.InternalGameNameInConfig,
            IsCacheUpdateEnabled = GamePreset.IsCacheUpdateEnabled,
            IsConvertible = GamePreset.IsConvertible,
            IsExperimental = GamePreset.IsExperimental,
            IsGenshin = GamePreset.IsGenshin,
            IsHideSocMedDesc = GamePreset.IsHideSocMedDesc,
            IsRepairEnabled = GamePreset.IsRepairEnabled,
            LauncherResourceURL = GamePreset.LauncherResourceURL,
            LauncherSpriteURL = GamePreset.LauncherSpriteURL,
            LauncherSpriteURLMultiLang = GamePreset.LauncherSpriteURLMultiLang,
            LauncherSpriteURLMultiLangFallback = GamePreset.LauncherSpriteURLMultiLangFallback,
            ProfileName = GamePreset.ProfileName,
            ProtoDispatchKey = GamePreset.ProtoDispatchKey,
            SteamGameID = GamePreset.SteamGameID,
            SteamInstallRegistryLocation = GamePreset.SteamInstallRegistryLocation,
            UseRightSideProgress = GamePreset.UseRightSideProgress,
            VendorType = GamePreset.VendorType,
            ZoneDescription = GamePreset.ZoneDescription,
            ZoneFullname = GamePreset.ZoneFullname,
            ZoneLogoURL = GamePreset.ZoneLogoURL,
            ZoneName = GamePreset.ZoneName,
            ZonePosterURL = GamePreset.ZonePosterURL,
            ZoneURL = GamePreset.ZoneURL,
        };
        #endregion

        internal RegionResourceProp _APIResouceProp { get; set; }
        internal PresetConfigV2 _GamePreset { get; set; }
        internal IGameSettings _GameSettings { get; set; }
        internal IRepair _GameRepair { get; set; }
        internal ICache _GameCache { get; set; }
        internal IGameVersionCheck _GameVersion { get; set; }
        internal IGameInstallManager _GameInstall { get; set; }
    }

    internal static class GamePropertyVault
    {
        private static Dictionary<int, GamePresetProperty> Vault = new Dictionary<int, GamePresetProperty>();
        public static int LastGameHashID { get; set; }
        public static int CurrentGameHashID { get; set; }
        public static GamePresetProperty CurrentGameProperty { get; set; }

        public static void LoadGameProperty(UIElement UIElementParent, RegionResourceProp APIResouceProp, PresetConfigV2 GamePreset)
        {
            LastGameHashID = LastGameHashID == 0 ? GamePreset.HashID : LastGameHashID;
            CurrentGameHashID = GamePreset.HashID;
            CurrentGameProperty = RegisterGameProperty(UIElementParent, APIResouceProp, GamePreset);
        }

        public static GamePresetProperty RegisterGameProperty(UIElement UIElementParent, RegionResourceProp APIResouceProp, PresetConfigV2 GamePreset)
        {
            CleanupUnusedGameProperty();
            if (Vault.ContainsKey(GamePreset.HashID))
            {
#if DEBUG
                Logger.LogWriteLine($"[GamePropertyVault] Returning cached game property by Hash ID: {GamePreset.HashID}", LogType.Debug, true);
#endif
                return Vault[GamePreset.HashID];
            }

            GamePresetProperty Property = new GamePresetProperty(UIElementParent, APIResouceProp, GamePreset);
            Vault.Add(GamePreset.HashID, Property);
#if DEBUG
            Logger.LogWriteLine($"[GamePropertyVault] Creating & caching game property by Hash ID: {GamePreset.HashID}", LogType.Debug, true);
#endif
            return Property;
        }

        private static void CleanupUnusedGameProperty()
        {
            if (Vault.Count == 0) return;

            int[] unusedGamePropertyHashID = Vault.Values
                .Where(x => !x._GameInstall.IsRunning && x._GamePreset.HashID != CurrentGameHashID)?
                .Select(x => x._GamePreset.HashID)?
                .ToArray();

            foreach (int key in unusedGamePropertyHashID)
            {
#if DEBUG
                Logger.LogWriteLine($"[GamePropertyVault] Cleaning up unused game property by Hash ID: {key}", LogType.Debug, true);
#endif
                Vault.Remove(key);
            }
        }

        public static void AttachNotifForCurrentGame()
        {
            if (Vault.ContainsKey(CurrentGameHashID)) AttachNotifForCurrentGame_Inner(CurrentGameHashID);
        }

        private static void AttachNotifForCurrentGame_Inner(int HashID)
        {
            GamePresetProperty GameProperty = Vault[HashID];
            if (GameProperty._GameInstall.IsRunning)
            {
                string actTitle = $"Downloading game: {GameProperty._GameVersion.GamePreset.GameName}";
                string actSubtitle = GameProperty._GameVersion.GamePreset.ZoneName;
                BackgroundActivityManager.Attach(HashID, GameProperty._GameInstall, actTitle, actSubtitle);
            }
        }

        public static void DetachNotifForCurrentGame()
        {
            if (Vault.ContainsKey(CurrentGameHashID)) BackgroundActivityManager.Detach(CurrentGameHashID);
        }
    }

    internal partial class PageStatics
    {
        internal static CommunityToolsProperty _CommunityToolsProperty { get; set; }
    }
}
