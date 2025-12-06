using CollapseLauncher.Extension;
using CollapseLauncher.GameSettings.Base;
using CollapseLauncher.Interfaces;
using Hi3Helper;
using Hi3Helper.EncTool;
using Hi3Helper.Win32.Screen;
using Microsoft.Win32;
using System;
using System.Drawing;
using static Hi3Helper.Logger;
// ReSharper disable RedundantDefaultMemberInitializer
// ReSharper disable IdentifierTypo
// ReSharper disable StringLiteralTypo
// ReSharper disable InconsistentNaming
// ReSharper disable UnusedMember.Global
// ReSharper disable MemberCanBePrivate.Global

#pragma warning disable CS0659
namespace CollapseLauncher.GameSettings.Genshin
{
    internal class ScreenManager : BaseScreenSettingData, IGameSettingsValue<ScreenManager>
    {
        #region Fields
        private const           string        ValueNameScreenManagerWidth      = "Screenmanager Resolution Width_h182942802";
        private const           string        ValueNameScreenManagerHeight     = "Screenmanager Resolution Height_h2627697771";
        private const           string        ValueNameScreenManagerFullscreen = "Screenmanager Is Fullscreen mode_h3981298716";
        private static readonly Size          CurrentRes                       = ScreenProp.CurrentResolution;

        private ScreenManager(IGameSettings gameSettings) : base(gameSettings) { }
        #endregion

        #region Properties
        /// <summary>
        /// This value references values from inner width and height.<br/><br/>
        /// Range: 64 x 64 - int.MaxValue x int.MaxValue<br/>
        /// Default: Your screen resolution
        /// </summary>
        public override Size sizeRes
        {
            get => new(width, height);
            set
            {
                width = value.Width < 64 ? CurrentRes.Width : value.Width;
                height = value.Height < 64 ? CurrentRes.Height : value.Height;
            }
        }

        /// <summary>
        /// This value references values from inner width and height as string.<br/><br/>
        /// Range: 64 x 64 - int.MaxValue x int.MaxValue<br/>
        /// Default: Your screen resolution
        /// </summary>
        public override string sizeResString
        {
            get => $"{width}x{height}";
            set
            {
                string[] size = value.Split('x');
                if (!int.TryParse(size[0], out int w) || !int.TryParse(size[1], out int h))
                {
                    width = CurrentRes.Width;
                    height = CurrentRes.Height;
                }
                else
                {
                    width = w;
                    height = h;
                }
            }
        }

        /// <summary>
        /// This defines "<c>Game Resolution</c>'s Width" combobox In-game settings -> Video.<br/><br/>
        /// Range: 0 - int.MaxValue<br/>
        /// Default: Your screen width
        /// </summary>
        public override int width { get; set; } = CurrentRes.Width;

        /// <summary>
        /// This defines "<c>Game Resolution</c>'s Height" combobox In-game settings -> Video.<br/><br/>
        /// Range: 0 - int.MaxValue<br/>
        /// Default: Your screen Height
        /// </summary>
        public override int height { get; set; } = CurrentRes.Height;

        /// <summary>
        /// This defines "<c>Fullscreen</c>" checkbox In-game settings -> Video.<br/><br/>
        /// Range: 0 - 1
        /// Default: 1
        /// </summary>
        public int fullscreen { get; set; } = 1;

        public override bool isfullScreen
        {
            get => fullscreen switch
            {
                1 => true,
                _ => false
            };
            set => fullscreen = value switch
            {
                true => 1,
                _ => 0
            };
        }
        #endregion

        #region Methods
#nullable enable
        public static ScreenManager Load(IGameSettings gameSettings)
        {
            try
            {
                if (gameSettings.RegistryRoot == null)
                    throw new NullReferenceException("Cannot load Genshin Screen Manager settings as RegistryKey is unexpectedly not initialized!");

                object? valueWidth = gameSettings.RegistryRoot.TryGetValue(ValueNameScreenManagerWidth, null, gameSettings.RefreshRegistryRoot);
                object? valueHeight = gameSettings.RegistryRoot.TryGetValue(ValueNameScreenManagerHeight, null, gameSettings.RefreshRegistryRoot);
                object? valueFullscreen = gameSettings.RegistryRoot.TryGetValue(ValueNameScreenManagerFullscreen, null, gameSettings.RefreshRegistryRoot);
                if (valueWidth != null && valueHeight != null && valueFullscreen != null)
                {
                    int width = (int)valueWidth;
                    int height = (int)valueHeight;
                    int fullscreen = (int)valueFullscreen;
#if DEBUG
                    LogWriteLine($"Loaded Genshin Settings: {ValueNameScreenManagerWidth} : {width}", LogType.Debug, true);
                    LogWriteLine($"Loaded Genshin Settings: {ValueNameScreenManagerHeight} : {height}", LogType.Debug, true);
                    LogWriteLine($"Loaded Genshin Settings: {ValueNameScreenManagerFullscreen} : {fullscreen}", LogType.Debug, true);
#endif
                    return new ScreenManager(gameSettings) { width = width, height = height, fullscreen = fullscreen };
                }
            }
            catch (Exception ex)
            {
                LogWriteLine($"Failed while reading Genshin ScreenManager Data" +
                             $"\r\n  Please open the game and change any Graphics Settings, then close normally. After that you can use this feature." +
                             $"\r\n  If the issue persist, please report it on GitHub" +
                             $"\r\n{ex}", LogType.Error, true);
                ErrorSender.SendException(new Exception(
                    $"Failed when reading Genshin ScreenManager Data\r\n" +
                    $"Please open the game and change any graphics settings, then safely close the game. If the problem persist, report the issue on our GitHub\r\n" +
                    $"{ex}", ex));
            }

            return new ScreenManager(gameSettings);
        }

        public override void Save()
        {
            try
            {
                if (ParentGameSettings.RegistryRoot == null)
                    return;

                ParentGameSettings.RegistryRoot.SetValue(ValueNameScreenManagerFullscreen, fullscreen, RegistryValueKind.DWord);
                ParentGameSettings.RegistryRoot.SetValue(ValueNameScreenManagerWidth, width, RegistryValueKind.DWord);
                ParentGameSettings.RegistryRoot.SetValue(ValueNameScreenManagerHeight, height, RegistryValueKind.DWord);
#if DEBUG
                LogWriteLine($"Saved Genshin Settings: {ValueNameScreenManagerFullscreen} : {ParentGameSettings.RegistryRoot.GetValue(ValueNameScreenManagerFullscreen, null)}", LogType.Debug, true);
                LogWriteLine($"Saved Genshin Settings: {ValueNameScreenManagerWidth} : {ParentGameSettings.RegistryRoot.GetValue(ValueNameScreenManagerWidth, null)}", LogType.Debug, true);
                LogWriteLine($"Saved Genshin Settings: {ValueNameScreenManagerHeight} : {ParentGameSettings.RegistryRoot.GetValue(ValueNameScreenManagerHeight, null)}", LogType.Debug, true);
#endif
            }
            catch (Exception ex)
            {
                LogWriteLine($"Failed to save Genshin Impact ScreenManager Values!\r\n{ex}", LogType.Error, true);
                ErrorSender.SendException(new Exception("Failed to save Genshin Impact ScreenManager Values!", ex));
            }
        }

        public override bool Equals(object? comparedTo) => comparedTo is ScreenManager toThis && TypeExtensions.IsInstancePropertyEqual(this, toThis);
    }
    #endregion
}