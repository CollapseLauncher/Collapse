﻿using CollapseLauncher.GameSettings.Base;
using CollapseLauncher.GameSettings.Honkai.Context;
using CollapseLauncher.Interfaces;
using Hi3Helper;
using Hi3Helper.EncTool;
using Hi3Helper.Screen;
using Microsoft.Win32;
using System;
using System.Drawing;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using static CollapseLauncher.GameSettings.Base.SettingsBase;
using static Hi3Helper.Logger;

namespace CollapseLauncher.GameSettings.Honkai
{
    internal class ScreenSettingData : BaseScreenSettingData, IGameSettingsValue<ScreenSettingData>
    {
        #region Fields
        private const string _ValueName = "GENERAL_DATA_V2_ScreenSettingData_h1916288658";
        private const string _ValueNameScreenManagerFullscreen = "Screenmanager Is Fullscreen mode_h3981298716";
        private const string _ValueNameScreenManagerWidth = "Screenmanager Resolution Width_h182942802";
        private const string _ValueNameScreenManagerHeight = "Screenmanager Resolution Height_h2627697771";
        private static Size currentRes = ScreenProp.currentResolution;
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
        [JsonIgnore]
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
                if (RegistryRoot == null) throw new NullReferenceException($"Cannot load {_ValueName} RegistryKey is unexpectedly not initialized!");

                object? value = RegistryRoot.GetValue(_ValueName, null);

                if (value != null)
                {
                    ReadOnlySpan<byte> byteStr = (byte[])value;
#if DEBUG
                    LogWriteLine($"Loaded HI3 Settings: {_ValueName}\r\n{Encoding.UTF8.GetString((byte[])value, 0, ((byte[])value).Length - 1)}", LogType.Debug, true);
#endif
                    return (ScreenSettingData?)JsonSerializer.Deserialize(byteStr.Slice(0, byteStr.Length - 1), typeof(ScreenSettingData), HonkaiSettingsJSONContext.Default) ?? new ScreenSettingData();
                }
            }
            catch (Exception ex)
            {
                LogWriteLine($"Failed while reading {_ValueName}" +
                             $"\r\n  Please open the game and change any Graphics Settings, then close normally. After that you can use this feature." +
                             $"\r\n  If the issue persist, please report it on GitHub" +
                             $"\r\n{ex}", LogType.Error, true);
                ErrorSender.SendException(new Exception(
                    $"Failed when reading game settings {_ValueName}\r\n" +
                    $"Please open the game and change any graphics settings, then safely close the game. If the problem persist, report the issue on our GitHub\r\n" +
                    $"{ex}", ex));
            }

            return new ScreenSettingData();
        }

        public override void Save()
        {
            try
            {
                if (RegistryRoot == null) throw new NullReferenceException($"Cannot save {_ValueName} since RegistryKey is unexpectedly not initialized!");

                string data = JsonSerializer.Serialize(this, typeof(ScreenSettingData), HonkaiSettingsJSONContext.Default) + '\0';
                byte[] dataByte = Encoding.UTF8.GetBytes(data);

                RegistryRoot.SetValue(_ValueName, dataByte, RegistryValueKind.Binary);
#if DEBUG
                LogWriteLine($"Saved HI3 Settings: {_ValueName}\r\n{data}", LogType.Debug, true);
#endif
                SaveIndividualRegistry();
            }
            catch (Exception ex)
            {
                LogWriteLine($"Failed to save {_ValueName}!\r\n{ex}", LogType.Error, true);
            }
        }

        private void SaveIndividualRegistry()
        {
            RegistryRoot?.SetValue(_ValueNameScreenManagerFullscreen, isfullScreen ? 1 : 0, RegistryValueKind.DWord);
            RegistryRoot?.SetValue(_ValueNameScreenManagerWidth, width, RegistryValueKind.DWord);
            RegistryRoot?.SetValue(_ValueNameScreenManagerHeight, height, RegistryValueKind.DWord);
        }

        public bool Equals(ScreenSettingData? comparedTo) => TypeExtensions.IsInstancePropertyEqual(this, comparedTo);
        #endregion
    }
}
