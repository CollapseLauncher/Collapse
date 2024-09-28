using Hi3Helper.Data;
using Hi3Helper.Shared.Region;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;

namespace CollapseLauncher.Helper.Database
{
    public static class DbConfig
    {
        #region Properties
        private static readonly string _configFolder   = LauncherConfig.AppDataFolder;

        private const string _configFileName = "dbConfig.ini";
        private const string DbSectionName   = "[database]";

        private static readonly string  _configPath = Path.Combine(_configFolder, _configFileName);

        private static IniFile Config = null;
        #endregion

        public static void Init()
        {
            EnsureConfigExist();
            Load();
            
            if (!Config.ContainsSection(DbSectionName))
                Config.Add(DbSectionName, DbSettingsTemplate);
            
            DefaultChecker();
        }
        
        private static void EnsureConfigExist()
        {
            if (!File.Exists(_configPath)) File.Create(_configPath);
        }

        private static void DefaultChecker()
        {
            foreach (KeyValuePair<string, IniValue> Entry in DbSettingsTemplate)
            {
                if (!Config[DbSectionName].ContainsKey(Entry.Key) ||
                    string.IsNullOrEmpty(Config[DbSectionName][Entry.Key].Value))
                {
                    SetValue(Entry.Key, Entry.Value);
                }
            }
        }

        private static void Load() => Config.Load(_configPath);
        private static void Save() => Config.Save(_configPath);
        
        private static IniValue GetConfig(string key) => Config[DbSectionName][key];

        private static void SetValue(string key, IniValue value) => Config[DbSectionName]![key] = value;

        public static void SetAndSaveValue(string key, IniValue value)
        {
            SetValue(key, value);
            Save();
        }

        #region Template
        private static readonly Dictionary<string, IniValue> DbSettingsTemplate = new()
        {
            { "url", "libsql://test-db-bagusnl.turso.io" },
            { "token", "" },
            { "userGuid", "" }
        };
        #endregion

        public static string DbUrl
        {
            get => GetConfig("url").ToString();
            set => SetAndSaveValue("url", value);
        }

        [DebuggerHidden]
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        public static string DbToken
        {
            get => GetConfig("token").ToString();
            set => SetAndSaveValue("token", value);
        }

        [DebuggerHidden]
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        public static Guid UserGuid
        {
            get
            {
                var  c = GetConfig("userGuid").ToString();

                return string.IsNullOrEmpty(c) ? Guid.CreateVersion7() : Guid.Parse(c);
            }
            set => SetAndSaveValue("userGuid", value.ToString());
        }
    }
}