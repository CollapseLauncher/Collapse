using CollapseLauncher.Extension;
using CollapseLauncher.GameSettings.Base;
using CollapseLauncher.GameSettings.StarRail.Context;
using CollapseLauncher.Interfaces;
using Hi3Helper;
using Hi3Helper.EncTool;
using Hi3Helper.SentryHelper;
using Hi3Helper.Win32.Screen;
using Microsoft.Win32;
using System;
using System.Drawing;
using System.Text;
using System.Text.Json.Serialization;
using static Hi3Helper.Logger;
// ReSharper disable RedundantDefaultMemberInitializer
// ReSharper disable IdentifierTypo
// ReSharper disable StringLiteralTypo
// ReSharper disable InconsistentNaming

#pragma warning disable CS0659
namespace CollapseLauncher.GameSettings.StarRail
{
    internal class PCResolution : BaseScreenSettingData, IGameSettingsValue<PCResolution>
    {
        #region Fields
        private const           string ValueName                        = "GraphicsSettings_PCResolution_h431323223";
        private const           string ValueNameScreenManagerWidth      = "Screenmanager Resolution Width_h182942802";
        private const           string ValueNameScreenManagerHeight     = "Screenmanager Resolution Height_h2627697771";
        private const           string ValueNameScreenManagerFullscreen = "Screenmanager Fullscreen mode_h3630240806";
        private static readonly Size   CurrentRes                       = ScreenProp.CurrentResolution;

        public PCResolution() : this(null){}

        private PCResolution(IGameSettings gameSettings) : base(gameSettings)
        {

        }
        #endregion

        #region Properties
        /// <summary>
        /// This value references values from inner width and height.<br/><br/>
        /// Range: 64 x 64 - int.MaxValue x int.MaxValue<br/>
        /// Default: Your screen resolution
        /// </summary>
        [JsonIgnore]
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
        [JsonIgnore]
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
        /// Default: true
        /// </summary>
        public bool isFullScreen { get; set; } = true;

        /// <summary>
        /// Same as <c>isFullScreen</c><br/><br/>
        /// </summary>
        [JsonIgnore]
        public override bool isfullScreen
        {
            get => isFullScreen;
            set => isFullScreen = value;
        }
        #endregion

        #region Methods
#nullable enable
        public static PCResolution Load(IGameSettings gameSettings)
        {
            try
            {
                if (gameSettings.RegistryRoot == null) throw new NullReferenceException($"Cannot load {ValueName} RegistryKey is unexpectedly not initialized!");

                object? value = gameSettings.RegistryRoot.TryGetValue(ValueName, null, gameSettings.RefreshRegistryRoot);

                if (value != null)
                {
                    ReadOnlySpan<byte> byteStr = (byte[])value;
#if DEBUG
                    LogWriteLine($"Loaded StarRail Settings: {ValueName}\r\n{Encoding.UTF8.GetString(byteStr.TrimEnd((byte)0))}", LogType.Debug, true);
#endif
                    PCResolution returnValue = byteStr.Deserialize(StarRailSettingsJsonContext.Default.PCResolution) ?? new PCResolution();
                    returnValue.ParentGameSettings = gameSettings;
                    return returnValue;
                }
            }
            catch (Exception ex)
            {
                LogWriteLine($"Failed while reading {ValueName}" +
                             $"\r\n  Please open the game and change any settings, then close normally. After that you can use this feature." +
                             $"\r\n  If the issue persist, please report it on GitHub" +
                             $"\r\n{ex}", LogType.Error, true);
                ErrorSender.SendException(new Exception(
                    $"Failed when reading game settings {ValueName}\r\n" +
                    $"Please open the game and change any settings, then safely close the game. If the problem persist, report the issue on our GitHub\r\n" +
                    $"{ex}", ex));
            }

            return new PCResolution(gameSettings);
        }

        public override void Save()
        {
            try
            {
                if (ParentGameSettings.RegistryRoot == null) throw new NullReferenceException($"Cannot save {ValueName} since RegistryKey is unexpectedly not initialized!");

                string data = this.Serialize(StarRailSettingsJsonContext.Default.PCResolution);
                byte[] dataByte = Encoding.UTF8.GetBytes(data);

                ParentGameSettings.RegistryRoot.SetValue(ValueName, dataByte, RegistryValueKind.Binary);
#if DEBUG
                LogWriteLine($"Saved StarRail Settings: {ValueName}\r\n{data}", LogType.Debug, true);
#endif
                SaveIndividualRegistry();
            }
            catch (Exception ex)
            {
                LogWriteLine($"Failed to save {ValueName}!\r\n{ex}", LogType.Error, true);
                SentryHelper.ExceptionHandler(new Exception($"Failed to save {ValueName}!", ex), SentryHelper.ExceptionType.UnhandledOther);
            }
        }

        private void SaveIndividualRegistry()
        {
            ParentGameSettings.RegistryRoot?.SetValue(ValueNameScreenManagerFullscreen, isfullScreen ? 1 : 3, RegistryValueKind.DWord);
            ParentGameSettings.RegistryRoot?.SetValue(ValueNameScreenManagerWidth, width, RegistryValueKind.DWord);
            ParentGameSettings.RegistryRoot?.SetValue(ValueNameScreenManagerHeight, height, RegistryValueKind.DWord);
#if DEBUG
            LogWriteLine($"Saved StarRail Settings: {ValueNameScreenManagerFullscreen} : {ParentGameSettings.RegistryRoot?.GetValue(ValueNameScreenManagerFullscreen, null)}", LogType.Debug, true);
            LogWriteLine($"Saved StarRail Settings: {ValueNameScreenManagerWidth} : {ParentGameSettings.RegistryRoot?.GetValue(ValueNameScreenManagerWidth, null)}", LogType.Debug, true);
            LogWriteLine($"Saved StarRail Settings: {ValueNameScreenManagerHeight} : {ParentGameSettings.RegistryRoot?.GetValue(ValueNameScreenManagerHeight, null)}", LogType.Debug, true);
#endif
        }

        public override bool Equals(object? comparedTo) => comparedTo is PCResolution toThis && TypeExtensions.IsInstancePropertyEqual(this, toThis);
        #endregion
    }
}
