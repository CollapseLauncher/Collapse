using CollapseLauncher.Helper.Metadata;
using Hi3Helper;
using Hi3Helper.Shared.Region;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace CollapseLauncher
{
    public class CommunityToolsProperty
    {
        public Dictionary<GameNameType, List<CommunityToolsEntry>> OfficialToolsDictionary { get; set; }
        public Dictionary<GameNameType, List<CommunityToolsEntry>> CommunityToolsDictionary { get; set; }

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

        public static async Task<CommunityToolsProperty> LoadCommunityTools(Stream fileStream)
        {
            try
            {
                return await fileStream.DeserializeAsync<CommunityToolsProperty>(InternalAppJSONContext.Default);
            }
            catch (Exception ex)
            {
                Logger.LogWriteLine($"Error while parsing Community Tools list from the {(fileStream is FileStream fs ? $"FileStream: {fs.Name}" : "Stream")}\r\n{ex}", LogType.Error, true);
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
        public List<string> Profiles { get; set; }
    }
}
