using CollapseLauncher.Extension;
using CollapseLauncher.GameSettings.Base;
using CollapseLauncher.GameSettings.Honkai.Context;
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
using static CollapseLauncher.GameSettings.Base.SettingsBase;
using static Hi3Helper.Logger;
// ReSharper disable RedundantDefaultMemberInitializer
// ReSharper disable IdentifierTypo
// ReSharper disable StringLiteralTypo

#pragma warning disable CS0659
namespace CollapseLauncher.GameSettings.Honkai
{
    internal class ScreenSettingData : BaseScreenSettingData, IGameSettingsValue<ScreenSettingData>
    {
        #region Fields
        private const           string ValueName                        = "GENERAL_DATA_V2_ScreenSettingData_h1916288658";
        private const           string ValueNameScreenManagerFullscreen = "Screenmanager Is Fullscreen mode_h3981298716";
        private const           string ValueNameScreenManagerWidth      = "Screenmanager Resolution Width_h182942802";
        private const           string ValueNameScreenManagerHeight     = "Screenmanager Resolution Height_h2627697771";
        private static readonly Size   CurrentRes                        = ScreenProp.CurrentResolution;
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
                if (!int.TryParse(size[0], out var w) || !int.TryParse(size[1], out var h))
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
        public override bool isfullScreen { get; set; } = true;
        #endregion

        #region Methods
#nullable enable
        public static ScreenSettingData Load()
        {
            try
            {
                if (RegistryRoot == null) throw new NullReferenceException($"Cannot load {ValueName} RegistryKey is unexpectedly not initialized!");

                object? value = RegistryRoot.TryGetValue(ValueName, null, RefreshRegistryRoot);

                if (value != null)
                {
                    ReadOnlySpan<byte> byteStr = (byte[])value;
#if DEBUG
                    LogWriteLine($"Loaded HI3 Settings: {ValueName}\r\n{Encoding.UTF8.GetString(byteStr.TrimEnd((byte)0))}", LogType.Debug, true);
#endif
                    return byteStr.Deserialize(HonkaiSettingsJsonContext.Default.ScreenSettingData) ?? new ScreenSettingData();
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

            return new ScreenSettingData();
        }

        public override void Save()
        {
            try
            {
                if (RegistryRoot == null) throw new NullReferenceException($"Cannot save {ValueName} since RegistryKey is unexpectedly not initialized!");

                string data = this.Serialize(HonkaiSettingsJsonContext.Default.ScreenSettingData);
                byte[] dataByte = Encoding.UTF8.GetBytes(data);

                RegistryRoot.SetValue(ValueName, dataByte, RegistryValueKind.Binary);
#if DEBUG
                LogWriteLine($"Saved HI3 Settings: {ValueName}\r\n{data}", LogType.Debug, true);
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
            RegistryRoot?.SetValue(ValueNameScreenManagerFullscreen, isfullScreen ? 1 : 0, RegistryValueKind.DWord);
            RegistryRoot?.SetValue(ValueNameScreenManagerWidth, width, RegistryValueKind.DWord);
            RegistryRoot?.SetValue(ValueNameScreenManagerHeight, height, RegistryValueKind.DWord);
        }

        public override bool Equals(object? comparedTo) => comparedTo is ScreenSettingData toThis && TypeExtensions.IsInstancePropertyEqual(this, toThis);
        #endregion
    }
}
