using CollapseLauncher.GameSettings.Base;
using CollapseLauncher.Helper;
using CollapseLauncher.Interfaces;
using Hi3Helper;
using Hi3Helper.EncTool;
using Microsoft.Win32;
using Hi3Helper.Win32.Screen;
using System;
using System.Drawing;
using Hi3Helper.SentryHelper;
using static CollapseLauncher.GameSettings.Base.SettingsBase;
using static Hi3Helper.Logger;

namespace CollapseLauncher.GameSettings.Zenless
{
    internal class ScreenManager : BaseScreenSettingData, IGameSettingsValue<ScreenManager>
    {
        #region Fields
        private const  string _ValueNameScreenManagerWidth      = "Screenmanager Resolution Width_h182942802";
        private const  string _ValueNameScreenManagerWidthDef   = "Screenmanager Resolution Width Default_h680557497";
        private const  string _ValueNameScreenManagerHeight     = "Screenmanager Resolution Height_h2627697771";
        private const  string _ValueNameScreenManagerHeightDef  = "Screenmanager Resolution Height Default_h1380706816";
        private const  string _ValueNameScreenManagerFullscreen = "Screenmanager Fullscreen mode_h3630240806";
        private static Size   currentRes                        = ScreenProp.CurrentResolution;
        #endregion

        #region Enums
        public enum FullScreenMode
        {
            Fullscreen = 1,
            Something,
            Windowed
        }

        #endregion

        #region Properties
        /// <summary>
        /// This value references values from inner width and height.<br/><br/>
        /// Range: 64 x 64 - int.MaxValue x int.MaxValue<br/>
        /// Default: Your screen resolution
        /// </summary>
        public override Size sizeRes
        {
            get => new Size(width, height);
            set
            {
                width = value.Width < 64 ? currentRes.Width : value.Width;
                height = value.Height < 64 ? currentRes.Height : value.Height;
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
                int w;
                int h;
                if (!int.TryParse(size[0], out w) || !int.TryParse(size[1], out h))
                {
                    width = currentRes.Width;
                    height = currentRes.Height;
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
        public override int width { get; set; } = currentRes.Width;

        /// <summary>
        /// This defines "<c>Game Resolution</c>'s Height" combobox In-game settings -> Video.<br/><br/>
        /// Range: 0 - int.MaxValue<br/>
        /// Default: Your screen Height
        /// </summary>
        public override int height { get; set; } = currentRes.Height;

        /// <summary>
        /// This defines "<c>Fullscreen</c>" checkbox In-game settings -> Video.<br/><br/>
        /// Range: 0 - 1
        /// Default: 1
        /// </summary>
        public FullScreenMode fullscreen { get; set; } = FullScreenMode.Fullscreen;

        public override bool isfullScreen
        {
            get => fullscreen switch
            {
                FullScreenMode.Fullscreen => true,
                _ => false,
            };
            set => fullscreen = value switch
            {
                true => FullScreenMode.Fullscreen,
                false => FullScreenMode.Windowed,
            };
        }
        #endregion

        #region Methods
#nullable enable
        public static ScreenManager Load()
        {
            try
            {
                if (RegistryRoot == null) throw new NullReferenceException($"Cannot load Zenless Screen Manager settings as RegistryKey is unexpectedly not initialized!");

                object? valueWidth = RegistryRoot.GetValue(_ValueNameScreenManagerWidth, null);
                object? valueHeight = RegistryRoot.GetValue(_ValueNameScreenManagerHeight, null);
                object? valueFullscreen = RegistryRoot.GetValue(_ValueNameScreenManagerFullscreen, null);
                if (valueWidth != null && valueHeight != null && valueFullscreen != null)
                {
                    int width = (int)valueWidth;
                    int height = (int)valueHeight;
                    FullScreenMode fullscreen = (FullScreenMode)valueFullscreen;
#if DEBUG
                    LogWriteLine($"Loaded Zenless Settings:\r\n\t" +
                                 $"{_ValueNameScreenManagerWidth} : {width}\r\n\t" +
                                 $"{_ValueNameScreenManagerHeight} : {height}\r\n\t" +
                                 $"{_ValueNameScreenManagerFullscreen} : {fullscreen}", LogType.Debug, true);
#endif
                    return new ScreenManager { width = width, height = height, fullscreen = fullscreen };
                }
            }
            catch (Exception ex)
            {
                LogWriteLine($"Failed while reading Zenless ScreenManager Data" +
                             $"\r\n  Please open the game and change any Graphics Settings, then close normally. After that you can use this feature." +
                             $"\r\n  If the issue persist, please report it on GitHub" +
                             $"\r\n{ex}", LogType.Error, true);
                ErrorSender.SendException(new Exception(
                    $"Failed when reading Zenless ScreenManager Data\r\n" +
                    $"Please open the game and change any graphics settings, then safely close the game. If the problem persist, report the issue on our GitHub\r\n" +
                    $"{ex}", ex));
            }

            return new ScreenManager();
        }

        public override void Save()
        {
            try
            {
                RegistryRoot?.SetValue(_ValueNameScreenManagerFullscreen, fullscreen, RegistryValueKind.DWord);
                RegistryRoot?.SetValue(_ValueNameScreenManagerWidth, width, RegistryValueKind.DWord);
                RegistryRoot?.SetValue(_ValueNameScreenManagerWidthDef, width, RegistryValueKind.DWord);
                RegistryRoot?.SetValue(_ValueNameScreenManagerHeight, height, RegistryValueKind.DWord);
                RegistryRoot?.SetValue(_ValueNameScreenManagerHeightDef, height, RegistryValueKind.DWord);
#if DEBUG
                LogWriteLine($"Saved Zenless Settings:\r\n\t" +
                             $"{_ValueNameScreenManagerFullscreen} : {RegistryRoot?.GetValue(_ValueNameScreenManagerFullscreen, null)}\r\n\t" +
                             $"{_ValueNameScreenManagerWidth} : {RegistryRoot?.GetValue(_ValueNameScreenManagerWidth, null)}\r\n\t" +
                             $"{_ValueNameScreenManagerHeight} : {RegistryRoot?.GetValue(_ValueNameScreenManagerHeight, null)}", LogType.Debug, true);
#endif
            }
            catch (Exception ex)
            {
                LogWriteLine($"Failed to save Zenless ScreenManager Values!\r\n{ex}", LogType.Error, true);
                SentryHelper.ExceptionHandler(ex, SentryHelper.ExceptionType.UnhandledOther);
            }
        }

        public bool Equals(ScreenManager? comparedTo) => TypeExtensions.IsInstancePropertyEqual(this, comparedTo);
    }
    #endregion
}