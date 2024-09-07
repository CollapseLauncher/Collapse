using CollapseLauncher.GameSettings.Universal;
using CollapseLauncher.Interfaces;
using Microsoft.Win32;
using System.IO;

namespace CollapseLauncher.GameSettings.Base
{
    internal class SettingsBase : ImportExportBase, IGameSettings
    {
        #region Base Properties
        public virtual CustomArgs SettingsCustomArgument { get; set; }
        public virtual BaseScreenSettingData SettingsScreen { get; set; }
        public virtual CollapseScreenSetting SettingsCollapseScreen { get; set; }
        public virtual CollapseMiscSetting SettingsCollapseMisc { get; set; }
        #endregion

#nullable enable
        private static RegistryKey? _registryRoot;

        internal static string? RegistryPath
        {
            get => string.IsNullOrEmpty(_gameVersionManager?.GamePreset?.InternalGameNameInConfig) ? null :
                Path.Combine($"Software\\{_gameVersionManager.VendorTypeProp.VendorType}", _gameVersionManager.GamePreset.InternalGameNameInConfig);
        }

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

        protected SettingsBase(IGameVersionCheck GameVersionManager) => _gameVersionManager = GameVersionManager;

        public virtual void InitializeSettings()
        {
            SettingsCustomArgument = CustomArgs.Load();
            SettingsCollapseScreen = CollapseScreenSetting.Load();
            SettingsCollapseMisc = CollapseMiscSetting.Load();
        }

#nullable disable
        public static IGameVersionCheck _gameVersionManager { get; set; }

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
