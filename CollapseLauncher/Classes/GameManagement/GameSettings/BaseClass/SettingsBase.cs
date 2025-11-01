using CollapseLauncher.GameSettings.Universal;
using CollapseLauncher.Interfaces;
using Microsoft.Win32;
using System.IO;

namespace CollapseLauncher.GameSettings.Base
{
    internal abstract class SettingsBase : ImportExportBase, IGameSettings
    {
        #region Base Properties
        public virtual CustomArgs SettingsCustomArgument { get; set; }
        public virtual BaseScreenSettingData SettingsScreen { get; set; }
        public virtual CollapseScreenSetting SettingsCollapseScreen { get; set; }
        public virtual CollapseMiscSetting SettingsCollapseMisc { get; set; }
        #endregion

#nullable enable

        internal static string? RegistryPath
        {
            get => string.IsNullOrEmpty(GameVersionManager?.GamePreset.InternalGameNameInConfig) ? null :
                Path.Combine($"Software\\{GameVersionManager.VendorTypeProp.VendorType}", GameVersionManager.GamePreset.InternalGameNameInConfig);
        }

        private static RegistryKey? _registryRoot;
        internal static RegistryKey? RegistryRoot
        {
            get
             {
                // If the registry path is null, then return null
                if (RegistryPath == null) return null;

                // Try to open the registry path
                _registryRoot = Registry.CurrentUser.OpenSubKey(RegistryPath, true);

                // If it's still empty, then create a new one
                _registryRoot ??= Registry.CurrentUser.CreateSubKey(RegistryPath, true, RegistryOptions.None);

                return _registryRoot;
            }
        }

        internal static RegistryKey? RefreshRegistryRoot()
        {
            _registryRoot?.Close();
            _registryRoot?.Dispose();
            _registryRoot = null;
            return RegistryRoot;
        }

        public abstract string GetLaunchArguments(GamePresetProperty property);

        protected SettingsBase() {}
        protected SettingsBase(IGameVersion gameVersionManager) => GameVersionManager = gameVersionManager;

        public static IGameSettings CreateBaseFrom<T>(IGameVersion gameVersionManager, bool isEnableResizableWindow = false)
            where T : SettingsBase, new()
        {
            SettingsBase settings = new T();
            GameVersionManager = gameVersionManager;

            settings.InitializeSettings();
            settings.SettingsCollapseScreen.UseResizableWindow = isEnableResizableWindow;

            return settings;
        }

        public virtual void InitializeSettings()
        {
            SettingsCustomArgument = CustomArgs.Load();
            SettingsCollapseScreen = CollapseScreenSetting.Load();
            SettingsCollapseMisc = CollapseMiscSetting.Load();
        }

#nullable disable
        public static IGameVersion GameVersionManager { get; set; }

        public virtual void ReloadSettings() => InitializeSettings();

        public virtual void SaveSettings() => SaveBaseSettings();

        public void SaveBaseSettings()
        {
            SettingsCustomArgument.Save();
            SettingsCollapseScreen.Save();
            SettingsCollapseMisc.Save();
        }

        public virtual IGameSettingsUniversal AsIGameSettingsUniversal() => this;
    }
}
