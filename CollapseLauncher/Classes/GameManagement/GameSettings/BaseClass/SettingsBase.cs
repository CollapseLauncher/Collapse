using CollapseLauncher.GameSettings.Universal;
using CollapseLauncher.Interfaces;

namespace CollapseLauncher.GameSettings.Base
{
    internal abstract class SettingsBase : ImportExportBase, IGameSettings
    {
        #region Base Properties
        public virtual CustomArgs            SettingsCustomArgument { get; set; }
        public virtual BaseScreenSettingData SettingsScreen         { get; set; }
        public virtual CollapseScreenSetting SettingsCollapseScreen { get; set; }
        public virtual CollapseMiscSetting   SettingsCollapseMisc   { get; set; }
        #endregion

#nullable enable

        public abstract string GetLaunchArguments(GamePresetProperty property);

        protected SettingsBase() {}

        protected SettingsBase(IGameVersion gameVersionManager) : base(gameVersionManager) { }

        public static IGameSettings CreateBaseFrom<T>(IGameVersion gameVersionManager, bool isEnableResizableWindow = false)
            where T : SettingsBase, new()
        {
            SettingsBase settings = new T();
            settings.GameVersionManager = gameVersionManager;

            settings.InitializeSettings();
            settings.SettingsCollapseScreen.UseResizableWindow = isEnableResizableWindow;

            return settings;
        }

        public virtual void InitializeSettings()
        {
            SettingsCustomArgument = CustomArgs.Load(this);
            SettingsCollapseScreen = CollapseScreenSetting.Load(this);
            SettingsCollapseMisc   = CollapseMiscSetting.Load(this);
        }

#nullable disable

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
