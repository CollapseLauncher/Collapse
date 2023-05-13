﻿using CollapseLauncher.GameSettings.Base;
using CollapseLauncher.GameSettings.StarRail.Context;
using CollapseLauncher.Interfaces;
using Hi3Helper;
using Hi3Helper.Screen;
using Microsoft.Win32;
using System;
using System.Drawing;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using static CollapseLauncher.GameSettings.Statics;
using static Hi3Helper.Logger;

namespace CollapseLauncher.GameSettings.StarRail
{
    internal class PCResolution : BaseScreenSettingData, IGameSettingsValue<PCResolution>
    {
        #region Fields
        private const string _ValueName = "GraphicsSettings_PCResolution_h431323223";
        private const string _ValueNameScreenManagerWidth = "Screenmanager Resolution Width_h182942802";
        private const string _ValueNameScreenManagerHeight = "Screenmanager Resolution Height_h2627697771";
        private const string _ValueNameScreenManagerFullscreen = "Screenmanager Fullscreen mode_h3630240806";
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
#nullable enable
        #region Methods
        public static PCResolution Load()
        {
            try
            {
                if (RegistryRoot == null) throw new NullReferenceException($"Cannot load {_ValueName} RegistryKey is unexpectedly not initialized!");

                object? value = RegistryRoot.GetValue(_ValueName, null);

                if (value != null)
                {
                    ReadOnlySpan<byte> byteStr = (byte[])value;
                    return (PCResolution?)JsonSerializer.Deserialize(byteStr.Slice(0, byteStr.Length - 1), typeof(PCResolution), PCResolutionContext.Default) ?? new PCResolution();
                }
            }
            catch (Exception ex)
            {
                LogWriteLine($"Failed while reading {_ValueName}\r\n{ex}", LogType.Error, true);
            }

            return new PCResolution();
        }

        public override void Save()
        {
            try
            {
                if (RegistryRoot == null) throw new NullReferenceException($"Cannot save {_ValueName} since RegistryKey is unexpectedly not initialized!");

                string data = JsonSerializer.Serialize(this, typeof(PCResolution), PCResolutionContext.Default) + '\0';
                byte[] dataByte = Encoding.UTF8.GetBytes(data);

                RegistryRoot.SetValue(_ValueName, dataByte, RegistryValueKind.Binary);

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

        public bool Equals(PCResolution? comparedTo)
        {
            if (ReferenceEquals(this, comparedTo)) return true;
            if (comparedTo == null) return false;

            return comparedTo.sizeRes == this.sizeRes &&
                comparedTo.height == this.height &&
                comparedTo.width == this.width &&
                comparedTo.isfullScreen == this.isfullScreen;
        }
        #endregion
#nullable disable

    }
}