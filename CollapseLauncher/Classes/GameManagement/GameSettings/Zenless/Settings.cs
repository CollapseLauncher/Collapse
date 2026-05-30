using CollapseLauncher.GameSettings.Base;
using CollapseLauncher.GameSettings.Zenless.Context;
using CollapseLauncher.GameSettings.Zenless.Enums;
using CollapseLauncher.GameVersioning;
using CollapseLauncher.Interfaces;
using System;
using System.Diagnostics;
using System.Drawing;
using System.Runtime.CompilerServices;
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
        public GeneralData GeneralData { get; private set; }
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

            SettingsScreen = ScreenManager.Load(this);
            GeneralData?.Dispose();
            GeneralData = GeneralData.LoadWithMagic(
                MagicReDo,
                SettingsGameVersionManager.Create(GameVersionManager, ZZZSettingsConfigFile, "GENERAL_DATA.bin"),
                ZenlessSettingsJsonContext.Default.GeneralData);
        }

        public override void ReloadSettings()
        {
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
            DefaultInterpolatedStringHandler builder = new();

            if (SettingsCollapseScreen.UseCustomResolution)
            {
                Size screenSize = SettingsScreen.sizeRes;
                builder.AppendFormatted(screenSize.Width, "-screen-width 0 ");
                builder.AppendFormatted(screenSize.Height, "-screen-height 0 ");
            }
            
            // Enable MobileMode
            if (SettingsCollapseMisc.LaunchMobileMode)
            {
                // Force save on every launch
                GeneralData.LocalUILayoutPlatform = LocalUiLayoutPlatform.Mobile;
                GeneralData.Save();
            }

            if (SettingsCollapseScreen.GameGraphicsAPI == 4)
            {
                builder.AppendLiteral("-use-d3d12 ");
            }

            if (SettingsCollapseScreen.UseBorderlessScreen)
            {
                builder.AppendLiteral("-popupwindow ");
            }

            string customArgs = SettingsCustomArgument.CustomArgumentValue;
            if (SettingsCollapseMisc.UseCustomArguments &&
                !string.IsNullOrEmpty(customArgs))
            {
                builder.AppendLiteral(customArgs);
            }

            return builder.ToStringAndClear();
        }

        public override IGameSettingsUniversal AsIGameSettingsUniversal() => this;
    }
}