using CollapseLauncher.Helper.Metadata;
using CollapseLauncher.Interfaces.Class;
using Hi3Helper;
using Hi3Helper.SentryHelper;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using WinRT;

// ReSharper disable PartialTypeWithSinglePart
// ReSharper disable CheckNamespace

#nullable enable
namespace CollapseLauncher
{
    [JsonSourceGenerationOptions(IncludeFields = false, GenerationMode = JsonSourceGenerationMode.Metadata, IgnoreReadOnlyFields = true)]
    [JsonSerializable(typeof(CommunityToolsProperty))]
    internal sealed partial class CommunityToolsPropertyJsonContext : JsonSerializerContext;

    public class CommunityToolsProperty
    {
        public Dictionary<GameNameType, List<CommunityToolsEntry>> OfficialToolsDictionary  { get; set; } = [];
        public Dictionary<GameNameType, List<CommunityToolsEntry>> CommunityToolsDictionary { get; set; } = [];

        private readonly Dictionary<string, List<CommunityToolsEntry>> _officialToolsDictionaryPerProfileCache  = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, List<CommunityToolsEntry>> _communityToolsDictionaryPerProfileCache = new(StringComparer.OrdinalIgnoreCase);

        public void Clear()
        {
            OfficialToolsDictionary.Clear();
            _officialToolsDictionaryPerProfileCache.Clear();

            CommunityToolsDictionary.Clear();
            _communityToolsDictionaryPerProfileCache.Clear();
        }

        public static async Task<CommunityToolsProperty?> LoadCommunityTools(Stream fileStream)
        {
            try
            {
                CommunityToolsProperty? communityToolkitProperty = await fileStream.DeserializeAsync(CommunityToolsPropertyJsonContext.Default.CommunityToolsProperty);
                return communityToolkitProperty;
            }
            catch (Exception ex)
            {
                await SentryHelper.ExceptionHandlerAsync(ex, SentryHelper.ExceptionType.UnhandledOther);
                Logger.LogWriteLine($"Error while parsing Community Tools list from the {(fileStream is FileStream fs ? $"FileStream: {fs.Name}" : "Stream")}\r\n{ex}", LogType.Error, true);
                return new CommunityToolsProperty();
            }
        }

        public void CombineFrom(CommunityToolsProperty fromSource)
        {
            AddOrUpdate(OfficialToolsDictionary,  fromSource.OfficialToolsDictionary);
            AddOrUpdate(CommunityToolsDictionary, fromSource.CommunityToolsDictionary);
        }

        public CommunityToolsContext GetContext(PresetConfig config)
        {
            string profileName = config.ProfileName ?? throw new NullReferenceException("Game config's ProfileName field cannot be null!");

            ref List<CommunityToolsEntry>? officialEntries =
                ref CollectionsMarshal.GetValueRefOrAddDefault(_officialToolsDictionaryPerProfileCache, profileName, out _);

            ref List<CommunityToolsEntry>? communityEntries =
                ref CollectionsMarshal.GetValueRefOrAddDefault(_communityToolsDictionaryPerProfileCache, profileName, out _);

            officialEntries  ??= [];
            communityEntries ??= [];

            if (officialEntries.Count != 0 ||
                communityEntries.Count != 0)
            {
                return new CommunityToolsContext
                {
                    OfficialEntries  = officialEntries,
                    CommunityEntries = communityEntries,
                    GamePresetConfig = config
                };
            }

            AddListBasedOnRegion(officialEntries,  OfficialToolsDictionary,  profileName);
            AddListBasedOnRegion(communityEntries, CommunityToolsDictionary, profileName);

            return new CommunityToolsContext
            {
                OfficialEntries  = officialEntries,
                CommunityEntries = communityEntries,
                GamePresetConfig = config
            };
        }

        private static void AddListBasedOnRegion(
            List<CommunityToolsEntry>                           listToAdd,
            Dictionary<GameNameType, List<CommunityToolsEntry>> source,
            string                                              profileName)
        {
            listToAdd.AddRange(source.Values
                                     .SelectMany(x => x)
                                     .Where(entry => entry.Profiles.Contains(profileName)));
        }

        private static void AddOrUpdate(
            Dictionary<GameNameType, List<CommunityToolsEntry>> toUpdate,
            Dictionary<GameNameType, List<CommunityToolsEntry>> source)
        {
            foreach (KeyValuePair<GameNameType, List<CommunityToolsEntry>> kvp in source)
            {
                ref List<CommunityToolsEntry>? value = ref CollectionsMarshal.GetValueRefOrAddDefault(toUpdate, kvp.Key, out _);
                (value ??= []).AddRange(kvp.Value);
            }
        }
    }

    [GeneratedBindableCustomProperty]
    public partial class CommunityToolsContext : NotifyPropertyChanged
    {
        public required PresetConfig GamePresetConfig
        {
            get;
            init
            {
                field = value;
                OnPropertyChanged();
            }
        }

        public List<CommunityToolsEntry> OfficialEntries
        {
            get;
            init
            {
                field = value;
                OnPropertyChanged();
            }
        } = [];

        public List<CommunityToolsEntry> CommunityEntries
        {
            get;
            init
            {
                field = value;
                OnPropertyChanged();
            }
        } = [];

        public bool IsHasAnyEntries => OfficialEntries.Count > 0 &&
                                       CommunityEntries.Count > 0;
    }

    [GeneratedBindableCustomProperty]
    public partial class CommunityToolsEntry : NotifyPropertyChanged
    {
        public string? IconFontFamily
        {
            get;
            set
            {
                field = value;
                OnPropertyChanged();
            }
        }

        public string? IconGlyph
        {
            get;
            init
            {
                field = value;
                OnPropertyChanged();
            }
        }

        public string? Text
        {
            get;
            init
            {
                field = value;
                OnPropertyChanged();
            }
        }

        public string? URL
        {
            get;
            init
            {
                field = value;
                OnPropertyChanged();
            }
        }

        public HashSet<string> Profiles
        {
            get;
            init
            {
                field = value;
                OnPropertyChanged();
            }
        } = [];
    }
}
