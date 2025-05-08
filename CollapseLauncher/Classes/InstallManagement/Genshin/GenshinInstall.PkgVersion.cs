using CollapseLauncher.Helper.Metadata;
using CollapseLauncher.Helper.StreamUtility;
using CollapseLauncher.Interfaces;
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
    protected override async Task<string> DownloadPkgVersion(DownloadClient downloadClient, RegionResourceVersion? packageLatestBase)
    {
        const string errorRaiseMsg = "Please raise this issue to our Github Repo or Official Discord";

        // Note:
        // Starting from Genshin 5.6 update, HoYo removed ScatteredFiles alongside Zip packages.
        // This cause the pkg_version to be unavailable. As for alternative, we are replacing it
        // by making a fake JSON response of pkg_version by fetching references from Sophon manifest.

        // Get GameVersionManager and GamePreset
        IGameVersion     gameVersion     = GameVersionManager;
        PresetConfig     gamePreset      = gameVersion.GamePreset;
        SophonChunkUrls? branchResources = gamePreset.LauncherResourceChunksURL;

        // If branchResources is null, throw
        if (branchResources == null)
        {
            throw new InvalidOperationException("Branch resources are unavailable! " + errorRaiseMsg);
        }

        // Try to get client and manifest info pair
        HttpClient client = downloadClient.GetHttpClient();
        SophonChunkManifestInfoPair manifestInfoPair = await SophonManifest
            .CreateSophonChunkManifestInfoPair(client,
                                               branchResources.MainUrl,
                                               branchResources.MainBranchMatchingField,
                                               Token.Token);

        // Throw if main manifest pair is not found
        if (!manifestInfoPair.IsFound)
        {
            throw new InvalidOperationException($"Sophon main manifest info pair is not found! " + errorRaiseMsg);
        }

        // Get the existing voice-over matching fields
        List<string> availableVaMatchingFields = [];
        await GetVoiceOverPkgVersionMatchingFields(availableVaMatchingFields);

        // If any, then create them
        if (availableVaMatchingFields.Count != 0)
        {
            foreach (var field in availableVaMatchingFields)
            {
                SophonChunkManifestInfoPair voManifestInfoPair = manifestInfoPair.GetOtherManifestInfoPair(field);
                if (!voManifestInfoPair.IsFound)
                {
                    throw new InvalidOperationException($"Sophon voice-over manifest info pair for field: {field} is not found! " + errorRaiseMsg);
                }

                string languageString = GetLanguageStringByLocaleCode(field);
                await CreateFakePkgVersionFromSophon(client,
                                                     voManifestInfoPair,
                                                     $"Audio_{languageString}_pkg_version",
                                                     Token.Token);
            }
        }

        // Create the main pkg_version
        await CreateFakePkgVersionFromSophon(client,
                                             manifestInfoPair,
                                             "pkg_version",
                                             Token.Token);

        // Return the main pkg_version path
        return Path.Combine(GamePath, "pkg_version");
    }

    private async Task CreateFakePkgVersionFromSophon(HttpClient                  client,
                                                      SophonChunkManifestInfoPair manifestPair,
                                                      string                      pkgVersionFilename,
                                                      CancellationToken           token)
    {
        // Ensure and try to create examine FileInfo
        string filePath = Path.Combine(GamePath, pkgVersionFilename);
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
        }
    }

    private async Task GetVoiceOverPkgVersionMatchingFields(List<string> matchingFields)
    {
        // Try to get audio lang list file and if null, ignore
        string audioLangListPath = _gameAudioLangListPathStatic;
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
        while (await reader.ReadLineAsync(Token.Token) is { } line)
        {
            // Get locale code to be used as Sophon Manifest field later
            string? currentLocaleId = GetLanguageLocaleCodeByLanguageString(line
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
