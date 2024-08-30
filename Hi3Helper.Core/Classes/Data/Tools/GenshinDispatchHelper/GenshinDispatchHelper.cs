#nullable enable
    using Hi3Helper.EncTool.Parser.AssetIndex;
    using Hi3Helper.EncTool.Proto.Genshin;
    using Hi3Helper.Shared.ClassStruct;
    using System;
    using System.Collections.Generic;
    using System.Net.Http;
    using System.Net.Http.Json;
    using System.Text;
    using System.Text.Json;
    using System.Threading;
    using System.Threading.Tasks;

    // ReSharper disable IdentifierTypo
    // ReSharper disable CheckNamespace
    // ReSharper disable StringLiteralTypo
    // ReSharper disable CommentTypo

    namespace Hi3Helper.Data
    {
        public class GenshinDispatchHelper
        {
            private readonly CancellationToken _cancelToken;
            private readonly string            _channelName = "OSRELWin";

            private          GenshinGateway? _gateway;
            private readonly HttpClient      _httpClient;
            private          QueryProperty?  _returnValProp;

            public GenshinDispatchHelper(HttpClient httpClient, int regionID, string dispatchKey, string dispatchURLPrefix,
                                         string versionString = "2.6.0", CancellationToken cancelToken = new())
            {
                if (regionID >= 4)
                {
                    _channelName = "CNRELWin";
                }

                _httpClient     = httpClient;
                RegionSubdomain = GetSubdomainByRegionID(regionID);
                Version         = versionString;
                DispatchBaseURL = string.Format(dispatchURLPrefix, RegionSubdomain, $"{_channelName}{versionString}",
                                                dispatchKey);
                _cancelToken = cancelToken;
            }

            private string DispatchBaseURL { get; }
            private string RegionSubdomain { get; }
            private string Version         { get; }

            public async Task<YSDispatchInfo?> LoadDispatchInfo()
            {
            #if DEBUG
                // DEBUG ONLY: Show URL of Proto
                string dFormat = $"URL for Proto Response:\r\n{DispatchBaseURL}";
                Logger.LogWriteLine(dFormat, LogType.Default, true);
            #endif

                return await _httpClient.GetFromJsonAsync(DispatchBaseURL, CoreLibraryJSONContext.Default.YSDispatchInfo,
                                                          _cancelToken);
            }

            public async Task LoadDispatch(byte[] customDispatchData)
            {
                _gateway = GenshinGateway.Parser!.ParseFrom(customDispatchData);
                _returnValProp = new QueryProperty
                {
                    GameServerName = _gateway!.GatewayProperties!.ServerName,
                    ClientGameResURL =
                        $"{_gateway.GatewayProperties.RepoResVersionURL}/output_{_gateway.GatewayProperties.RepoResVersionProperties!.ResVersionNumber}_{_gateway.GatewayProperties.RepoResVersionProperties.ResVersionHash}/client",
                    ClientDesignDataURL =
                        $"{_gateway.GatewayProperties.RepoDesignDataURL}/output_{_gateway.GatewayProperties.RepoDesignDataNumber}_{_gateway.GatewayProperties.RepoDesignDataHash}/client/General",
                    ClientDesignDataSilURL =
                        $"{_gateway.GatewayProperties.RepoDesignDataURL}/output_{_gateway.GatewayProperties.RepoDesignDataSilenceNumber}_{_gateway.GatewayProperties.RepoDesignDataSilenceHash}/client_silence/General",
                    DataRevisionNum    = _gateway.GatewayProperties.RepoDesignDataNumber,
                    SilenceRevisionNum = _gateway.GatewayProperties.RepoDesignDataSilenceNumber,
                    ResRevisionNum     = _gateway.GatewayProperties.RepoResVersionProperties.ResVersionNumber,
                    ChannelName        = _channelName,
                    GameVersion        = Version
                };

                ParseGameResPkgProp(_returnValProp);
                ParseDesignDataURL(_returnValProp);
                await ParseAudioAssetsURL(_returnValProp);
            }

            private void ParseDesignDataURL(QueryProperty valProp)
            {
                string[] dataList = _gateway!.GatewayProperties!.RepoResVersionProperties!.ResVersionMapJSON!.Split("\r\n");
                valProp.ClientGameRes = new List<PkgVersionProperties?>();
                foreach (string data in dataList)
                {
                    (valProp.ClientGameRes as List<PkgVersionProperties?>)?
                       .Add(
                            (PkgVersionProperties?)JsonSerializer.Deserialize(data, typeof(PkgVersionProperties),
                                                                              CoreLibraryJSONContext.Default)
                           );
                }
            }

            private void ParseGameResPkgProp(QueryProperty valProp)
            {
                var jsonDesignData    = _gateway!.GatewayProperties!.RepoDesignDataJSON;
                var jsonDesignDataSil = _gateway!.GatewayProperties!.RepoDesignDataSilenceJSON;
            #if DEBUG
                Logger.LogWriteLine($"[GenshinDispatchHelper::ParseGameResPkgProp] DesignData Response:" +
                                    $"\r\n\tDesignData:\r\n{jsonDesignData}" +
                                    $"\r\n\tDesignData_Silence:\r\n{jsonDesignDataSil}", LogType.Debug, true);
            #endif

                if (!string.IsNullOrEmpty(jsonDesignData))
                {
                    string[] designDataArr = jsonDesignData.Split(["\r\n", "\r", "\n"], StringSplitOptions.None);

                    foreach (string designData in designDataArr)
                    {
                        var designDataSer =
                            JsonSerializer.Deserialize(designData, CoreLibraryJSONContext.Default.PkgVersionProperties);
                        // Only serialize data_versions
                        if (designDataSer is { remoteName: "data_versions" })
                        {
                            valProp.ClientDesignData = designDataSer;
                        }

                        if (designDataSer == null)
                        {
                            Logger.LogWriteLine("[GenshinDispatchHelper::ParseGameResPkgProp] DesignData is null!",
                                                LogType.Warning, true);
                        }
                    }
                }

                if (jsonDesignDataSil != null)
                {
                    valProp.ClientDesignDataSil =
                        JsonSerializer.Deserialize(jsonDesignDataSil, typeof(PkgVersionProperties),
                                                   CoreLibraryJSONContext.Default) as PkgVersionProperties;
                }
                else
                {
                    Logger.LogWriteLine("[GenshinDispatchHelper::ParseGameResPkgProp] DesignData_Silence is null!",
                                        LogType.Warning, true);
                }
            }

            private async Task ParseAudioAssetsURL(QueryProperty valProp)
            {
                byte[] byteData = await _httpClient
                   .GetByteArrayAsync(
                                      ConverterTool.CombineURLFromString(valProp.ClientGameResURL,
                                                                         "/StandaloneWindows64/base_revision"),
                                      _cancelToken);
                string[] responseData = Encoding.UTF8.GetString(byteData).Split(' ');

                valProp.ClientAudioAssetsURL =
                    $"{_gateway!.GatewayProperties!.RepoResVersionURL}/output_{responseData[0]}_{responseData[1]}/client";
                valProp.AudioRevisionNum = uint.Parse(responseData[0]);
            }

            public QueryProperty? GetResult()
            {
                return _returnValProp;
            }

            private static string GetSubdomainByRegionID(int regionID)
            {
                return regionID switch
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
    }