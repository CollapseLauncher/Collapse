using CollapseLauncher.GameVersioning;
using CollapseLauncher.Helper.Metadata;
using CollapseLauncher.Helper.StreamUtility;
using CollapseLauncher.InstallManager.Base;
using CollapseLauncher.Interfaces;
using CollapseLauncher.Interfaces.Class;
using CollapseLauncher.Statics;
using Hi3Helper;
using Hi3Helper.Data;
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
    private readonly List<PkgVersionProperties>               _repairAssetIndex     = [];
    private readonly Dictionary<string, PkgVersionProperties> _repairAssetIndexDict = [];

    protected override async Task<string> DownloadPkgVersion(DownloadClient     downloadClient,
                                                             GamePackageResult? _)
    {
        string pkgVersionPath = Path.Combine(GamePath, "pkg_version");

        // Build asset index by borrowing methods from GenshinRepair instance
        // If game repair is not available, then skip.
        GamePresetProperty presetProperty = GamePropertyVault.GetCurrentGameProperty();
        if (presetProperty.GameRepair is not GenshinRepair genshinRepairInstance)
        {
            return pkgVersionPath;
        }

        // Clear last asset index list
        _repairAssetIndex.Clear();
        _repairAssetIndexDict.Clear();
        
        // Initialize asset index and load it from GenshinRepair instance
        await genshinRepairInstance.BuildPrimaryManifest(downloadClient,
                                                         _repairAssetIndex,
                                                         _repairAssetIndexDict,
                                                         CancellationToken.None);

        /*
        // Call and borrow persistent manifest method from GenshinRepair instance
        await genshinRepairInstance.BuildPersistentManifest(downloadClient,
                                                            null!,
                                                            _repairAssetIndex,
                                                            _repairAssetIndexDict,
                                                            CancellationToken.None);
        */

        return pkgVersionPath;
    }

    protected override ValueTask ParsePkgVersions2FileInfo(List<LocalFileInfo> pkgFileInfo,
                                                           HashSet<string>     pkgFileInfoHashSet,
                                                           CancellationToken   token)
    {
        foreach (PkgVersionProperties asset in _repairAssetIndex)
        {
            string relativePath = asset.remoteName;
            ConverterTool.NormalizePathInplaceNoTrim(relativePath);

            if (!pkgFileInfoHashSet.Add(relativePath))
            {
                continue;
            }

            LocalFileInfo assetLocalInfo = new LocalFileInfo
            {
                FileName     = Path.GetFileName(asset.remoteName),
                RelativePath = relativePath,
                FileSize     = asset.fileSize,
                FullPath     = Path.Combine(GamePath, asset.remoteName)
            };
            assetLocalInfo.IsFileExist = File.Exists(assetLocalInfo.FullPath);

            pkgFileInfo.Add(assetLocalInfo);
        }

        string? execPrefix = Path.GetFileNameWithoutExtension(GameVersionManager.GamePreset.GameExecutableName);
        if (string.IsNullOrEmpty(execPrefix))
        {
            return ValueTask.CompletedTask;
        }

        string        basePersistentPath = $"{execPrefix}_Data\\Persistent";
        List<string>? audioLangList      = (GameVersionManager as GameTypeGenshinVersion)?.AudioVoiceLanguageList;
        string        audioLangListPath  = Path.Combine(GamePath, basePersistentPath, "audio_lang_14");

        // Filter unnecessary files
        if (audioLangList != null)
        {
            GenshinRepair.EliminateUnnecessaryAssetIndex(audioLangListPath, audioLangList, pkgFileInfo, '\\', x => x.FullPath);
        }

        return ValueTask.CompletedTask;
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
                           .StripAlternateDataStream()
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
