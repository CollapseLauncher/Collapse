using Hi3Helper.Data;
using Hi3Helper.Shared.Region;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;

namespace CollapseLauncher.Helper.Database
{
    public static class DbConfig
    {
        #region Properties

        private static readonly string _configFolder = LauncherConfig.AppDataFolder;

        private const string _configFileName = "dbConfig.ini";
        private const string DbSectionName   = "database";

        private static readonly string  _configPath = Path.Combine(_configFolder, _configFileName);

        // ReSharper disable once FieldCanBeMadeReadOnly.Local
        private static IniFile _config = new();
        #endregion

        public static void Init()
        {
            EnsureConfigExist();
            Load();
            if (!_config.ContainsKey(DbSectionName))
                _config.Add(DbSectionName, DbSettingsTemplate);

            DefaultChecker();
        }
        
        private static void EnsureConfigExist()
        {
            if (File.Exists(_configPath)) return;

            var f = File.Create(_configPath);
            f.Close();
        }

        private static void DefaultChecker()
        {
            foreach (KeyValuePair<string, IniValue> Entry in DbSettingsTemplate)
            {
                if (!_config[DbSectionName].ContainsKey(Entry.Key) ||
                    string.IsNullOrEmpty(_config[DbSectionName][Entry.Key]))
                {
                    SetValue(Entry.Key, Entry.Value);
                }
            }
        }

        private static void Load(CancellationToken token = default) => _config.Load(_configPath);
        private static void Save(CancellationToken token = default) => _config.Save(_configPath);
        
        public static IniValue GetConfig(string key) => _config[DbSectionName][key];

        private static void SetValue(string key, IniValue value) => _config[DbSectionName]![key] = value;

        public static void SetAndSaveValue(string key, IniValue value)
        {
            SetValue(key, value);
            Save();
        }

        #region Template
        private static readonly Dictionary<string, IniValue> DbSettingsTemplate = new()
        {
            { "enabled", false },
            { "url", "" },
            { "token", "" },
            { "userGuid", "" }
        };
        #endregion

        public static bool DbEnabled
        {
            get => GetConfig("enabled");
            set => SetAndSaveValue("enabled", value);
        }

        [DebuggerHidden]
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        public static string DbUrl
        {
            get => GetConfig("url");
            set => SetAndSaveValue("url", value);
        }
        
        [DebuggerHidden]
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        public static string DbToken
        {
            get => GetConfig("token");
            set => SetAndSaveValue("token", value);
        }

        [DebuggerHidden]
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        public static string UserGuid
        {
            get
            {
                var  c = GetConfig("userGuid");

                if (!string.IsNullOrEmpty(c)) return c; // Return early if config is set

                c = Guid.CreateVersion7().ToString();
                SetAndSaveValue("userGuid", c);

                return c;
            }
            set => SetAndSaveValue("userGuid", value);
        }
    }
}