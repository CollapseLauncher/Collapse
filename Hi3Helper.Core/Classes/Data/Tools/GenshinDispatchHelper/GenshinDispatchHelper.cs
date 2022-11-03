using Hi3Helper.Preset;
using Hi3Helper.Shared.ClassStruct;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using static System.Convert;

namespace Hi3Helper.Data
{
    public class GenshinDispatchHelper : Http.Http
    {
        private string DispatchBaseURL { get; set; }
        private string RegionSubdomain { get; set; }
        private string ChannelName = "OSRELWin";
        private string Version { get; set; }

        private QueryProto DispatchProto = new QueryProto();
        private QueryProperty returnValProp;
        private CancellationToken cancelToken;

        public GenshinDispatchHelper(int RegionID, string DispatchKey, string DispatchURLPrefix, string VersionString = "2.6.0", CancellationToken cancelToken = new CancellationToken()) : base(false, 1, 1)
        {
            this.RegionSubdomain = GetSubdomainByRegionID(RegionID);
            this.Version = VersionString;
            this.DispatchBaseURL = string.Format(DispatchURLPrefix, RegionSubdomain, $"{ChannelName}{VersionString}", DispatchKey);
            this.cancelToken = cancelToken;
        }

        private async Task<byte[]> LoadRemoteDispatch()
        {
            using (MemoryStream response = new MemoryStream())
            {
                await Download(DispatchBaseURL, response, null, null, cancelToken);

                return response.GetBuffer();
            }
        }

        public async Task<YSDispatchInfo> LoadDispatchInfo()
        {
            YSDispatchInfo DispatcherDataInfo;
            using (MemoryStream s = new MemoryStream())
            {
#if DEBUG
                // DEBUG ONLY: Show URL of Proto
                string dFormat = string.Format("URL for Proto Response:\r\n{0}", DispatchBaseURL);
                Console.WriteLine(dFormat);
                Logger.WriteLog(dFormat, LogType.Default);
#endif
                await Download(DispatchBaseURL, s, null, null, cancelToken);
                s.Position = 0;
                DispatcherDataInfo = (YSDispatchInfo)JsonSerializer.Deserialize(s, typeof(YSDispatchInfo), YSDispatchInfoContext.Default);
            }

            return DispatcherDataInfo;
        }

        public async Task LoadDispatch(byte[] CustomDispatchData = null)
        {
            string stringdata = Encoding.UTF8.GetString(await LoadRemoteDispatch());
            byte[] ProtoData = CustomDispatchData == null
                ? FromBase64String(stringdata)
                : CustomDispatchData;

            DispatchProto = QueryProto.Parser.ParseFrom(ProtoData);
            returnValProp = new QueryProperty()
            {
                GameVoiceLangID = DispatchProto.Dispatcher.GameAudiolang,
                ClientGameResURL = string.Format("{0}/output_{1}_{2}/client",
                                    DispatchProto.Dispatcher.ClientGameResurl,
                                    DispatchProto.Dispatcher.DispatcherInternal.ClientGameResnum,
                                    DispatchProto.Dispatcher.DispatcherInternal.ClientGameReshash),
                ClientDesignDataURL = string.Format("{0}/output_{1}_{2}/client/General",
                                    DispatchProto.Dispatcher.ClientDesignDataurl,
                                    DispatchProto.Dispatcher.ClientDesignDatanum,
                                    DispatchProto.Dispatcher.ClientDesignDatahash),
                ClientDesignDataSilURL = string.Format("{0}/output_{1}_{2}/client_silence/General",
                                        DispatchProto.Dispatcher.ClientDesignDataurl,
                                        DispatchProto.Dispatcher.ClientDesignDatanumSlnt,
                                        DispatchProto.Dispatcher.ClientDesignDatahashSlnt),
                DataRevisionNum = DispatchProto.Dispatcher.ClientDesignDatanum,
                SilenceRevisionNum = DispatchProto.Dispatcher.ClientDesignDatanumSlnt,
                ResRevisionNum = DispatchProto.Dispatcher.DispatcherInternal.ClientGameResnum,
                ChannelName = this.ChannelName,
                GameVersion = this.Version
            };

            ParseGameResPkgProp(ref returnValProp);
            ParseDesignDataURL(ref returnValProp);
            await ParseAudioAssetsURL(returnValProp);
        }

        private void ParseDesignDataURL(ref QueryProperty ValProp)
        {
            IEnumerable<string> DataList = DispatchProto.Dispatcher.DispatcherInternal.ClientGameReslist.Split("\r\n");
            ValProp.ClientGameRes = new List<PkgVersionProperties>();
            foreach (string Data in DataList)
            {
                (ValProp.ClientGameRes as List<PkgVersionProperties>)
                    .Add(
                        (PkgVersionProperties)JsonSerializer.Deserialize(Data, typeof(PkgVersionProperties), PkgVersionPropertiesContext.Default)
                    );
            }
        }

        private void ParseGameResPkgProp(ref QueryProperty ValProp)
        {
            ValProp.ClientDesignData = (PkgVersionProperties)JsonSerializer.Deserialize(DispatchProto.Dispatcher.ClientDesignDatalist, typeof(PkgVersionProperties), PkgVersionPropertiesContext.Default);
            ValProp.ClientDesignDataSil = (PkgVersionProperties)JsonSerializer.Deserialize(DispatchProto.Dispatcher.ClientDesignDatalistSlnt, typeof(PkgVersionProperties), PkgVersionPropertiesContext.Default);
        }

        private async Task ParseAudioAssetsURL(QueryProperty ValProp)
        {
            using (MemoryStream response = new MemoryStream())
            {
                await Download(ValProp.ClientGameResURL + "/StandaloneWindows64/base_revision", response, null, null, cancelToken);
                string[] responseData = Encoding.UTF8.GetString(response.ToArray()).Split(' ');

                ValProp.ClientAudioAssetsURL = string.Format("{0}/output_{1}_{2}/client",
                                                DispatchProto.Dispatcher.ClientGameResurl,
                                                responseData[0],
                                                responseData[1]);
                ValProp.AudioRevisionNum = int.Parse(responseData[0]);
            }
        }

        public QueryProperty GetResult() => returnValProp;

        private string GetSubdomainByRegionID(int RegionID)
        {
            switch (RegionID)
            {
                /*
                 * Region ID:
                 * 0 = USA
                 * 1 = Europe
                 * 2 = Asia
                 * 3 = TW/HK/MO
                 */
                case 0:
                    return "osusadispatch";
                case 1:
                    return "oseurodispatch";
                case 2:
                    return "osasiadispatch";
                case 3:
                    return "oschtdispatch";
                default:
                    throw new FormatException("Unknown region ID!");
            }
        }
    }
}
