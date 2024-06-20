using CollapseLauncher.GamePlaytime.Universal;
using CollapseLauncher.Interfaces;
using Microsoft.Win32;
using System;
using System.IO;

namespace CollapseLauncher.GamePlaytime.Base
{
    internal class GamePlaytimeBase
    {
#nullable enable
        #region Properties
        internal static DateTime     BaseDate => new(2012, 2, 13, 0, 0, 0, DateTimeKind.Utc);
        
        protected RegistryKey? RegistryRoot;
        protected Playtime?    _playtime;
        #endregion

        public GamePlaytimeBase(IGameVersionCheck GameVersionManager)
        {
            _gameVersionManager = GameVersionManager;
        }
#nullable disable

        protected IGameVersionCheck _gameVersionManager { get; set; }

        protected static string TimeSpanToString(TimeSpan timeSpan)
        {
            return $"{timeSpan.Days * 24 + timeSpan.Hours}h {timeSpan.Minutes}m";
        }
    }
}
