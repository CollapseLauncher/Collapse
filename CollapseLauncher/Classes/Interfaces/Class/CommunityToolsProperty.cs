using Hi3Helper;
using Hi3Helper.Preset;
using Hi3Helper.Shared.Region;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Text.Json.Serialization;

namespace CollapseLauncher
{
    public class CommunityToolsProperty
    {
        public Dictionary<GameType, List<CommunityToolsEntry>> OfficialToolsDictionary { get; set; }
        public Dictionary<GameType, List<CommunityToolsEntry>> CommunityToolsDictionary { get; set; }

        [JsonIgnore(Condition = JsonIgnoreCondition.Always)]
        public ObservableCollection<CommunityToolsEntry> OfficialToolsList;
        [JsonIgnore(Condition = JsonIgnoreCondition.Always)]
        public ObservableCollection<CommunityToolsEntry> CommunityToolsList;

        public CommunityToolsProperty()
        {
            OfficialToolsList = new ObservableCollection<CommunityToolsEntry>();
            CommunityToolsList = new ObservableCollection<CommunityToolsEntry>();
        }

        public void Clear()
        {
            OfficialToolsList.Clear();
            CommunityToolsList.Clear();
        }

        public static CommunityToolsProperty LoadCommunityTools()
        {
            string filePath = Path.Combine(LauncherConfig.AppFolder, @"Assets\Presets\CommunityTools.json");

            try
            {
                if (!File.Exists(filePath)) throw new FileNotFoundException("Community Tools file is not found!", filePath);

                return File.ReadAllText(filePath).Deserialize<CommunityToolsProperty>(InternalAppJSONContext.Default);
            }
            catch (Exception ex)
            {
                Logger.LogWriteLine($"Error while parsing Community Tools list file in Assets\\Presets\\CommunityTools.json\r\n{ex}", LogType.Error, true);
                return new CommunityToolsProperty();
            }
        }
    }

    public class CommunityToolsEntry
    {
        public string IconFontFamily { get; set; }
        public string IconGlyph { get; set; }
        public string Text { get; set; }
        public string URL { get; set; }
    }
}
