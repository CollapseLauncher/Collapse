using CollapseLauncher.Extension;
using CollapseLauncher.Interfaces;
using Hi3Helper;
using Hi3Helper.SentryHelper;
using Microsoft.Win32;
using System;
using System.Text.Json.Serialization;
using static Hi3Helper.Logger;
// ReSharper disable RedundantDefaultMemberInitializer
// ReSharper disable IdentifierTypo
// ReSharper disable StringLiteralTypo

#pragma warning disable CS0659
namespace CollapseLauncher.GameSettings.Honkai
{
    internal class GraphicsGrade : IGameSettingsValue<GraphicsGrade>
    {
        #region Fields
        private const string ValueName = "GENERAL_DATA_V2_GraphicsGrade_h1073342808";

        [JsonIgnore]
        public IGameSettings ParentGameSettings { get; }

        private GraphicsGrade(IGameSettings gameSettings)
        {
            ParentGameSettings = gameSettings;
        }
        #endregion

        #region Enum
        // ReSharper disable once UnusedMember.Global
        public enum SelectGraphicsGrade {Performance = 1, Normal, HD, Quality, Max, Custom}
        #endregion

        #region Properties
        /// <summary>
        /// This defines the "<c>Video -> Graphics</c>" Quality preset Combobox <br/><br/>
        /// Value is int with possible value of 1 - 6
        /// 
        /// </summary>
        public SelectGraphicsGrade GraphicsGradeInt { get; set; } = SelectGraphicsGrade.Custom;
        #endregion
        
        #region Methods
        #nullable enable
        public static GraphicsGrade Load(IGameSettings gameSettings)
        {
            try
            {
                if (gameSettings.RegistryRoot == null) throw new NullReferenceException($"Cannot load {ValueName} RegistryKey is unexpectedly not initialized!");

                object? value = gameSettings.RegistryRoot.TryGetValue(ValueName, null, gameSettings.RefreshRegistryRoot);
                if (value != null)
                {
                    SelectGraphicsGrade graphicsGrade = (SelectGraphicsGrade)value;
                    #if DEBUG
                    LogWriteLine($"Loaded HI3 Settings: {ValueName} : {value}", LogType.Debug, true);
                    #endif

                    return new GraphicsGrade(gameSettings) { GraphicsGradeInt = graphicsGrade };
                }
            }
            catch ( Exception ex )
            {
                LogWriteLine($"Failed while reading {ValueName}" +
                                $"\r\n  Please open the game and change any Graphics Settings, then close normally. After that you can use this feature." +
                                $"\r\n  If the issue persist, please report it on GitHub" +
                                $"\r\n{ex}", LogType.Error, true);
                ErrorSender.SendException(new Exception(
                            $"Failed when reading game settings {ValueName}\r\n" +
                            $"Please open the game and change any graphics settings, then safely close the game. If the problem persist, report the issue on our GitHub\r\n" +
                            $"{ex}", ex));
            }

            return new GraphicsGrade(gameSettings);
        }

        public void Save()
        {
            try
            {
                if (ParentGameSettings.RegistryRoot == null) throw new NullReferenceException($"Cannot save {ValueName} since RegistryKey is unexpectedly not initialized!");
                LogWriteLine("(HI3 GSP) Forcing GraphicsGrade to Custom!", LogType.Warning, true);
                GraphicsGradeInt = SelectGraphicsGrade.Custom;
                ParentGameSettings.RegistryRoot.SetValue(ValueName, GraphicsGradeInt, RegistryValueKind.DWord);
                #if DEBUG
                LogWriteLine($"Saved HI3 Settings: {ValueName} : {GraphicsGradeInt}", LogType.Debug, true);
                #endif
            }
            catch (Exception ex)
            {
                LogWriteLine($"Failed to save {ValueName}!\r\n{ex}", LogType.Error, true);
                SentryHelper.ExceptionHandler(new Exception($"Failed to save {ValueName}!", ex), SentryHelper.ExceptionType.UnhandledOther);
            }
        }

        public override bool Equals(object? comparedTo) => comparedTo is GraphicsGrade toThis && GraphicsGradeInt == toThis.GraphicsGradeInt;
        #endregion
    }
}