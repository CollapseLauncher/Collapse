using Hi3Helper.EncTool.Parser.AssetIndex;
using Hi3Helper.EncTool.Proto.Genshin;
using Hi3Helper.Shared.ClassStruct;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Hi3Helper.Data
{
    public class GenshinDispatchHelper : IDisposable
    {
        private Http.Legacy.Http _httpClient;
        private string    DispatchBaseURL { get; set; }
        private string    RegionSubdomain { get; set; }
        private string    ChannelName = "OSRELWin";
        private string    Version { get; set; }

        private GenshinGateway Gateway;
        private QueryProperty returnValProp;
        private CancellationToken cancelToken;

        public GenshinDispatchHelper(int RegionID, string DispatchKey, string DispatchURLPrefix, string VersionString = "2.6.0", CancellationToken cancelToken = new CancellationToken())
        {
            if (RegionID >= 4) ChannelName = "CNRELWin";
            this._httpClient = new Http.Legacy.Http(false, 1, 1);
            this.RegionSubdomain = GetSubdomainByRegionID(RegionID);
            this.Version = VersionString;
            this.DispatchBaseURL = string.Format(DispatchURLPrefix!, RegionSubdomain, $"{ChannelName}{VersionString}", DispatchKey);
            this.cancelToken = cancelToken;
        }

        ~GenshinDispatchHelper() => Dispose();

        public void Dispose() => this._httpClient?.Dispose();

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
                await this._httpClient!.Download(DispatchBaseURL, s, null, null, cancelToken);
                s.Position = 0;
                DispatcherDataInfo = (YSDispatchInfo)JsonSerializer.Deserialize(s, typeof(YSDispatchInfo), CoreLibraryJSONContext.Default);
            }

            return DispatcherDataInfo;
        }

        public async Task LoadDispatch(byte[] CustomDispatchData = null)
        {
            Gateway = GenshinGateway.Parser!.ParseFrom(CustomDispatchData);
            returnValProp = new QueryProperty()
            {
                GameServerName = Gateway!.GatewayProperties!.ServerName,
                ClientGameResURL = string.Format("{0}/output_{1}_{2}/client",
                                    Gateway.GatewayProperties.RepoResVersionURL,
                                    Gateway.GatewayProperties.RepoResVersionProperties!.ResVersionNumber,
                                    Gateway.GatewayProperties.RepoResVersionProperties.ResVersionHash),
                ClientDesignDataURL = string.Format("{0}/output_{1}_{2}/client/General",
                                    Gateway.GatewayProperties.RepoDesignDataURL,
                                    Gateway.GatewayProperties.RepoDesignDataNumber,
                                    Gateway.GatewayProperties.RepoDesignDataHash),
                ClientDesignDataSilURL = string.Format("{0}/output_{1}_{2}/client_silence/General",
                                    Gateway.GatewayProperties.RepoDesignDataURL,
                                    Gateway.GatewayProperties.RepoDesignDataSilenceNumber,
                                    Gateway.GatewayProperties.RepoDesignDataSilenceHash),
                DataRevisionNum = Gateway.GatewayProperties.RepoDesignDataNumber,
                SilenceRevisionNum = Gateway.GatewayProperties.RepoDesignDataSilenceNumber,
                ResRevisionNum = Gateway.GatewayProperties.RepoResVersionProperties.ResVersionNumber,
                ChannelName = this.ChannelName,
                GameVersion = this.Version
            };

            ParseGameResPkgProp(ref returnValProp);
            ParseDesignDataURL(ref returnValProp);
            await ParseAudioAssetsURL(returnValProp);
        }

        private void ParseDesignDataURL(ref QueryProperty ValProp)
        {
            string[] DataList = Gateway!.GatewayProperties!.RepoResVersionProperties!.ResVersionMapJSON!.Split("\r\n");
            ValProp!.ClientGameRes = new List<PkgVersionProperties>();
            foreach (string Data in DataList)
            {
                (ValProp.ClientGameRes as List<PkgVersionProperties>)!
                    .Add(
                        (PkgVersionProperties)JsonSerializer.Deserialize(Data, typeof(PkgVersionProperties), CoreLibraryJSONContext.Default)
                    );
            }
        }

        private void ParseGameResPkgProp(ref QueryProperty ValProp)
        {
            var jsonDesignData    = Gateway!.GatewayProperties!.RepoDesignDataJSON;
            var jsonDesignDataSil = Gateway!.GatewayProperties!.RepoDesignDataSilenceJSON;
            #if DEBUG
            Logger.LogWriteLine($"[GenshinDispatchHelper::ParseGameResPkgProp] DesignData Response:" +
                                $"\r\n\tDesignData:\r\n{jsonDesignData}" +
                                $"\r\n\tDesignData_Silence:\r\n{jsonDesignDataSil}", LogType.Debug, true);
            #endif

            if (!string.IsNullOrEmpty(jsonDesignData))
            {
                string[] designDataArr = jsonDesignData.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);

                foreach (string designData in designDataArr)
                {
                    var designDataSer = (PkgVersionProperties)JsonSerializer.Deserialize(designData,
                        typeof(PkgVersionProperties), CoreLibraryJSONContext.Default); 
                    // Only serialize data_versions
                    if (designDataSer != null && designDataSer.remoteName == "data_versions")
                        ValProp!.ClientDesignData = designDataSer;
                    if (designDataSer == null)
                        Logger.LogWriteLine("[GenshinDispatchHelper::ParseGameResPkgProp] DesignData is null!", LogType.Warning, true);
                }
            }

            if (jsonDesignDataSil != null) 
                ValProp!.ClientDesignDataSil =
                    (PkgVersionProperties)JsonSerializer.Deserialize(jsonDesignDataSil, typeof(PkgVersionProperties),
                                                                     CoreLibraryJSONContext.Default);
            else Logger.LogWriteLine("[GenshinDispatchHelper::ParseGameResPkgProp] DesignData_Silence is null!", LogType.Warning, true);
        }

        private async Task ParseAudioAssetsURL(QueryProperty ValProp)
        {
            using (MemoryStream response = new MemoryStream())
            {
                await this._httpClient!.Download(ConverterTool.CombineURLFromString(ValProp!.ClientGameResURL, "/StandaloneWindows64/base_revision"), response, null, null, cancelToken);
                string[] responseData = Encoding.UTF8.GetString(response.ToArray()).Split(' ');

                ValProp.ClientAudioAssetsURL = string.Format("{0}/output_{1}_{2}/client",
                                                Gateway!.GatewayProperties!.RepoResVersionURL,
                                                responseData[0],
                                                responseData[1]);
                ValProp.AudioRevisionNum = uint.Parse(responseData[0]);
            }
        }

        public QueryProperty GetResult() => returnValProp;

        private string GetSubdomainByRegionID(int RegionID) => RegionID switch
        {
            /*
             * Region ID:
             * 0 = USA
             * 1 = Europe
             * 2 = Asia
             * 3 = TW/HK/MO
             * 4 = Mainland China
             * 5 = Mainland China (Bilibili)
             */
            0 => "osusadispatch",
            1 => "oseurodispatch",
            2 => "osasiadispatch",
            3 => "oschtdispatch",
            4 => "cngfdispatch",
            5 => "cnqddispatch",
            _ => throw new FormatException("Unknown region ID!")
        };
    }
}
