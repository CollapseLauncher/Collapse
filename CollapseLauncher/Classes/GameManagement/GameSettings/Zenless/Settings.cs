using CollapseLauncher.GameSettings.Base;
using CollapseLauncher.GameSettings.Zenless.Context;
using CollapseLauncher.GameVersioning;
using CollapseLauncher.Interfaces;
using System;
using System.Diagnostics;
using System.Drawing;
using System.Text;
// ReSharper disable InconsistentNaming
// ReSharper disable StringLiteralTypo

// ReSharper disable CheckNamespace

namespace CollapseLauncher.GameSettings.Zenless
{
    internal class ZenlessSettings : SettingsBase
    {
        #region Variables
#nullable enable
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private byte[]? _magicReDo;
#nullable restore
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private byte[] MagicReDo
        {
            get
            {
                if (_magicReDo != null) return _magicReDo;
                _magicReDo = (GameVersionManager as GameTypeZenlessVersion)?.GamePreset
                                                                             .GetGameDataTemplate("ImSleepin", [1, 0, 0, 0
                                                                             ]);
                if (_magicReDo == null || _magicReDo.Length == 0)
                    throw new NullReferenceException("MagicReDo value for ZZZ settings is empty!");
                return _magicReDo;
            }
        }
        #endregion

        #region Properties
        public GeneralData GeneralData { get; set; }
        #endregion

        public ZenlessSettings(IGameVersion gameVersionManager) : base(gameVersionManager)
        {
            // Initialize magic
            _ = MagicReDo;

            InitializeSettings();
        }

        public sealed override void InitializeSettings()
        {
            const string ZZZSettingsConfigFile = @"{0}_Data\Persistent\LocalStorage\{1}";

            base.InitializeSettings();

            SettingsScreen = ScreenManager.Load();
            GeneralData = GeneralData.LoadWithMagic(
                MagicReDo,
                SettingsGameVersionManager.Create(GameVersionManager, ZZZSettingsConfigFile, "GENERAL_DATA.bin"),
                ZenlessSettingsJsonContext.Default.GeneralData);
        }

        public override void ReloadSettings()
        {
            GeneralData.Dispose();
            InitializeSettings();
        }

        public override void SaveSettings()
        {
            base.SaveSettings();
            SettingsScreen.Save();
            GeneralData.Save();
        }

        public override string GetLaunchArguments(GamePresetProperty property)
        {
            StringBuilder parameter = new(1024);

            if (SettingsCollapseScreen.UseCustomResolution)
            {
                Size screenSize = SettingsScreen.sizeRes;
                parameter.Append($"-screen-width {screenSize.Width} -screen-height {screenSize.Height} ");
            }

            if (SettingsCollapseScreen.GameGraphicsAPI == 4)
            {
                parameter.Append("-use-d3d12 ");
            }

            if (SettingsCollapseScreen.UseBorderlessScreen)
            {
                parameter.Append("-popupwindow ");
            }

            string customArgs = SettingsCustomArgument.CustomArgumentValue;
            if (SettingsCollapseMisc.UseCustomArguments &&
                !string.IsNullOrEmpty(customArgs))
            {
                parameter.Append(customArgs);
            }

            return parameter.ToString();
        }

        public override IGameSettingsUniversal AsIGameSettingsUniversal() => this;
    }
}