using CollapseLauncher.GameVersioning;
using CollapseLauncher.Helper.Metadata;
using CollapseLauncher.Helper.StreamUtility;
using CollapseLauncher.InstallManager.Base;
using CollapseLauncher.Interfaces;
using CollapseLauncher.Statics;
using Hi3Helper;
using Hi3Helper.EncTool.Parser.AssetIndex;
using Hi3Helper.Http;
using Hi3Helper.Sophon;
using Hi3Helper.Sophon.Structs;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
// ReSharper disable IdentifierTypo
// ReSharper disable CommentTypo
// ReSharper disable StringLiteralTypo

namespace CollapseLauncher.InstallManager.Genshin;

#nullable enable
internal sealed partial class GenshinInstall
{
    protected override async Task<string> DownloadPkgVersion(DownloadClient downloadClient,
                                                             RegionResourceVersion? _)
    {
        // First, build the fake pkg_version from Sophon
        string pkgVersionPath = await DownloadPkgVersionStatic(downloadClient.GetHttpClient(),
                                                               GameVersionManager,
                                                               GamePath,
                                                               _gameAudioLangListPathStatic,
                                                               token: Token.Token);

        // Second, build persistent manifest from game_res dispatcher.
        // If game repair is not available, then skip.
        GamePresetProperty presetProperty = GamePropertyVault.GetCurrentGameProperty();
        if (presetProperty.GameRepair is not GenshinRepair genshinRepairInstance)
        {
            return pkgVersionPath;
        }

        // Call and borrow persistent manifest method from GenshinRepair instance
        await genshinRepairInstance.BuildPersistentManifest(downloadClient,
                                                            null,
                                                            [],
                                                            [],
                                                            Token.Token);

        return pkgVersionPath;
    }

    protected override async ValueTask ParsePkgVersions2FileInfo(List<LocalFileInfo> pkgFileInfo,
                                                                 HashSet<string>     pkgFileInfoHashSet,
                                                                 CancellationToken   token)
    {
        // Parse primary pkg_version as main manifest
        await base.ParsePkgVersions2FileInfo(pkgFileInfo,
                                             pkgFileInfoHashSet,
                                             token);

        string? execPrefix = Path.GetFileNameWithoutExtension(GameVersionManager.GamePreset.GameExecutableName);
        if (string.IsNullOrEmpty(execPrefix))
        {
            return;
        }

        string basePersistentPath      = $"{execPrefix}_Data\\Persistent";
        string baseStreamingAssetsPath = $"{execPrefix}_Data\\StreamingAssets";
        string persistentFolder        = Path.Combine(GamePath, basePersistentPath);

        List<string>? audioLangList     = (GameVersionManager as GameTypeGenshinVersion)?.AudioVoiceLanguageList;
        string        audioLangListPath = Path.Combine(GamePath, basePersistentPath, "audio_lang_14");

        // Then add additional parsing to persistent manifests (provided by dispatcher)
        string persistentResVersions = Path.Combine(persistentFolder, "res_versions_persist");
        if (File.Exists(persistentResVersions))
        {
            await ParsePersistentManifest2FileInfo(GamePath,
                                                   asset => Path.Combine(baseStreamingAssetsPath,
                                                                                                     GenshinRepair.GetParentFromAssetRelativePath(asset.RelativePath,
                                                                                                                                                  out _)),
                                                   persistentResVersions,
                                                   pkgFileInfo,
                                                   pkgFileInfoHashSet,
                                                   token);
        }

        // Filter unnecessary files
        if (audioLangList != null)
        {
            GenshinRepair.EliminateUnnecessaryAssetIndex(audioLangListPath, audioLangList, pkgFileInfo, '\\', x => x.FullPath);
        }
    }

    private static async Task ParsePersistentManifest2FileInfo(string                        baseGamePath,
                                                               Func<LocalFileInfo, string?>? assetGetMiddlePath,
                                                               string                        pkgFilePath,
                                                               List<LocalFileInfo>           pkgFileInfo,
                                                               HashSet<string>               pkgFileInfoHashSet,
                                                               CancellationToken             token)
    {
        using StreamReader reader = File.OpenText(pkgFilePath);
        while (await reader.ReadLineAsync(token) is { } line)
        {
            LocalFileInfo? localFileInfo = line.Deserialize(LocalFileInfoJsonContext.Default.LocalFileInfo);

            // If null, then go to next line
            if (localFileInfo == null)
                continue;

            string? middlePath   = assetGetMiddlePath?.Invoke(localFileInfo);
            string  fullBasePath = string.IsNullOrEmpty(middlePath) ? baseGamePath : Path.Combine(baseGamePath, middlePath);

            localFileInfo.FullPath    = Path.Combine(fullBasePath, localFileInfo.RelativePath);
            localFileInfo.FileName    = Path.GetFileName(localFileInfo.RelativePath);
            localFileInfo.IsFileExist = File.Exists(localFileInfo.FullPath);

            string relativePathNoBase = Path.Combine(middlePath ?? "", localFileInfo.RelativePath);

            // Add it to the list and hashset (if it's not registered yet)
            if (pkgFileInfoHashSet.Add(relativePathNoBase))
            {
                pkgFileInfo.Add(localFileInfo);
            }
        }
    }

    public static async Task<string> DownloadPkgVersionStatic(HttpClient                   client,
                                                              IGameVersion                 gameVersionManager,
                                                              string                       gameBasePath,
                                                              string                       gameAudioListPath,
                                                              SophonChunkManifestInfoPair? manifestInfoPair = null,
                                                              List<SophonAsset>?           outputAssetList = null,
                                                              CancellationToken            token = default)
    {
        const string errorRaiseMsg = "Please raise this issue to our Github Repo or Official Discord";

        // Note:
        // Starting from Genshin 5.6 update, HoYo removed ScatteredFiles alongside Zip packages.
        // This cause the pkg_version to be unavailable. As for alternative, we are replacing it
        // by making a fake JSON response of pkg_version by fetching references from Sophon manifest.

        // Get GameVersionManager and GamePreset
        PresetConfig     gamePreset      = gameVersionManager.GamePreset;
        SophonChunkUrls? branchResources = gamePreset.LauncherResourceChunksURL;

        // If branchResources is null, throw
        if (branchResources == null)
        {
            throw new InvalidOperationException("Branch resources are unavailable! " + errorRaiseMsg);
        }

        // Try to get client and manifest info pair
        manifestInfoPair ??= await SophonManifest
            .CreateSophonChunkManifestInfoPair(client,
                                               branchResources.MainUrl,
                                               branchResources.MainBranchMatchingField,
                                               token);

        // Throw if main manifest pair is not found
        if (!manifestInfoPair.IsFound)
        {
            throw new InvalidOperationException("Sophon main manifest info pair is not found! " + errorRaiseMsg);
        }

        // Create the main pkg_version
        await CreateFakePkgVersionFromSophon(client,
                                             gameBasePath,
                                             manifestInfoPair,
                                             outputAssetList,
                                             "pkg_version",
                                             token);

        // Get the existing voice-over matching fields
        List<string> availableVaMatchingFields = [];
        await GetVoiceOverPkgVersionMatchingFields(availableVaMatchingFields,
                                                   gameAudioListPath,
                                                   token);

        // ReSharper disable once InvertIf
        if (availableVaMatchingFields.Count != 0) // If any, then create them
        {
            foreach (var field in availableVaMatchingFields)
            {
                SophonChunkManifestInfoPair voManifestInfoPair = manifestInfoPair.GetOtherManifestInfoPair(field);
                if (!voManifestInfoPair.IsFound)
                {
                    throw new InvalidOperationException($"Sophon voice-over manifest info pair for field: {field} is not found! " + errorRaiseMsg);
                }

                string languageString = GetLanguageStringByLocaleCodeStatic(field);
                await CreateFakePkgVersionFromSophon(client,
                                                     gameBasePath,
                                                     voManifestInfoPair,
                                                     outputAssetList,
                                                     $"Audio_{languageString}_pkg_version",
                                                     token);
            }
        }

        // Return the main pkg_version path
        return Path.Combine(gameBasePath, "pkg_version");
    }

    private static async Task CreateFakePkgVersionFromSophon(HttpClient                  client,
                                                             string                      gamePath,
                                                             SophonChunkManifestInfoPair manifestPair,
                                                             List<SophonAsset>?          outputAssetList,
                                                             string                      pkgVersionFilename,
                                                             CancellationToken           token)
    {
        // Ensure and try to create examine FileInfo
        string filePath = Path.Combine(gamePath, pkgVersionFilename);
        FileInfo fileInfo = new FileInfo(filePath)
                           .EnsureCreationOfDirectory()
                           .EnsureNoReadOnly();

        // Create file stream
        await using FileStream stream = fileInfo.Create();

        // Start enumerate the SophonAsset
        byte[] newLineBytes = "\r\n"u8.ToArray();
        await foreach (SophonAsset assetInfo in SophonManifest.EnumerateAsync(client,
                                                                              manifestPair,
                                                                              null,
                                                                              token))
        {
            // Ignore stock pkg_version
            if (assetInfo.AssetName.EndsWith("pkg_version", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            // Create pkg_version entry
            PkgVersionProperties pkgVersionEntry = new PkgVersionProperties
            {
                remoteName = assetInfo.AssetName,
                md5        = assetInfo.AssetHash,
                fileSize   = assetInfo.AssetSize
            };

            // Serialize and write to stream
            await pkgVersionEntry.SerializeAsync(stream, CoreLibraryJsonContext.Default.PkgVersionProperties, token);
            await stream.WriteAsync(newLineBytes, token);

            outputAssetList?.Add(assetInfo);
        }
    }

    private static async Task GetVoiceOverPkgVersionMatchingFields(List<string>      matchingFields,
                                                                   string            audioLangListPath,
                                                                   CancellationToken token)
    {
        // Try to get audio lang list file and if null, ignore
        if (string.IsNullOrEmpty(audioLangListPath))
        {
            return;
        }

        // Try get existing audio lang file. If it doesn't exist, ignore
        FileInfo audioLangFile = new FileInfo(audioLangListPath);
        if (!audioLangFile.Exists)
        {
            return;
        }

        // Open the file and read the content
        await using FileStream audioLangFileStream = audioLangFile.OpenRead();
        using StreamReader reader = new StreamReader(audioLangFileStream);
        while (await reader.ReadLineAsync(token) is { } line)
        {
            // Get locale code to be used as Sophon Manifest field later
            string? currentLocaleId = GetLanguageLocaleCodeByLanguageStringStatic(line
#if !DEBUG
                , false
#endif
                );

            // If it's empty or invalid, go to next line
            if (string.IsNullOrEmpty(currentLocaleId))
            {
                continue;
            }

            // Otherwise, Add the field to the list
            matchingFields.Add(currentLocaleId);
        }
    }
}
