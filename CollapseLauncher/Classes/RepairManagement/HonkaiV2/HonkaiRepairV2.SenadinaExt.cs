using CollapseLauncher.Extension;
using CollapseLauncher.Helper;
using CollapseLauncher.Helper.DaftarPustaka;
using CollapseLauncher.Interfaces;
using Hi3Helper;
using Hi3Helper.Data;
using Hi3Helper.EncTool;
using Hi3Helper.Plugin.Core.Management;
using Hi3Helper.Preset;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Hashing;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

using static CollapseLauncher.HonkaiRepairV2;

// ReSharper disable CheckNamespace
// ReSharper disable IdentifierTypo
// ReSharper disable StringLiteralTypo
// ReSharper disable CommentTypo
#pragma warning disable IDE0031
#pragma warning disable IDE0130

#nullable enable
namespace CollapseLauncher.RepairManagement;

internal static class SenadinaExtension
{
    internal static async Task<SenadinaFileResult>
        GetSenadinaPropertyAsync<T>(
            this HttpClient   client,
            string?           mainUrl,
            string?           secondaryUrl,
            GameVersion       gameVersion,
            bool              throwIfFail          = false,
            ProgressBase<T>?  progressibleInstance = null,
            CancellationToken token                = default)
        where T : IAssetIndexSummary
    {
        // Update Progress
        if (progressibleInstance != null)
        {
            progressibleInstance.Status.ActivityStatus =
                string.Format(Locale.Current.Lang?._CachesPage?.CachesStatusFetchingType ?? "", "Senadina Files");
            progressibleInstance.Status.IsProgressAllIndetermined = true;
            progressibleInstance.Status.IsIncludePerFileIndicator = false;
            progressibleInstance.UpdateStatus();
        }

        // Get the Senadina File Identifier Dictionary and its file references
        Dictionary<string, ServeV3FileContext>? senadinaManifest
            = await client.GetSenadinaManifestAsync(mainUrl,
                                                    secondaryUrl,
                                                    false,
                                                    token);

        ServeV3FileContext? senadinaAudioManifestIdentity  = null;
        ServeV3FileContext? senadinaXmfMetaIdentity        = null;
        ServeV3FileContext? senadinaXmfInfoCurrentIdentity = null;
        ServeV3FileContext? senadinaXmfPatchIdentity       = null;

        Task senadinaAudioManifestTask =
            client.GetSenadinaIdentifierKind(senadinaManifest,
                                             "current/manifest.m",
                                             gameVersion,
                                             mainUrl,
                                             secondaryUrl,
                                             false,
                                             token)
                  .GetResultFromAction(result => senadinaAudioManifestIdentity = result);

        Task senadinaXmfMetaTask =
            client.GetSenadinaIdentifierKind(senadinaManifest,
                                             "BlocksMeta.xmf",
                                             gameVersion,
                                             mainUrl,
                                             secondaryUrl,
                                             false,
                                             token)
                  .GetResultFromAction(result => senadinaXmfMetaIdentity = result);

        Task senadinaXmfInfoCurrentTask =
            client.GetSenadinaIdentifierKind(senadinaManifest,
                                             "current/Blocks.xmf",
                                             gameVersion,
                                             mainUrl,
                                             secondaryUrl,
                                             false,
                                             token)
                  .GetResultFromAction(result => senadinaXmfInfoCurrentIdentity = result);

        Task senadinaXmfPatchTask =
            client.GetSenadinaIdentifierKind(senadinaManifest,
                                             "PatchConfig.xmf",
                                             gameVersion,
                                             mainUrl,
                                             secondaryUrl,
                                             false,
                                             token)
                  .GetResultFromAction(result => senadinaXmfPatchIdentity = result);

        await Task.WhenAll(senadinaAudioManifestTask,
                           senadinaXmfMetaTask,
                           senadinaXmfInfoCurrentTask,
                           senadinaXmfPatchTask);

        if (throwIfFail &&
            (senadinaAudioManifestIdentity == null ||
             senadinaXmfMetaIdentity == null ||
             senadinaXmfInfoCurrentIdentity == null ||
             senadinaXmfPatchIdentity == null))
        {
            throw new
                NullReferenceException("Cannot fetch a complete set of Senadina Identity file due to some result returns a null");
        }

        return new SenadinaFileResult
        {
            Audio          = senadinaAudioManifestIdentity,
            XmfMeta        = senadinaXmfMetaIdentity,
            XmfInfoCurrent = senadinaXmfInfoCurrentIdentity,
            XmfPatch       = senadinaXmfPatchIdentity
        };
    }

    private static async Task<ServeV3FileContext?>
        GetSenadinaIdentifierKind(
            this HttpClient                         client,
            Dictionary<string, ServeV3FileContext>? dict,
            string                                  indexFile,
            GameVersion                             gameVersion,
            string?                                 mainUrl,
            string?                                 secondaryUrl,
            bool                                    skipThrow,
            CancellationToken                       token)
    {
        mainUrl ??= secondaryUrl;
        ArgumentNullException.ThrowIfNull(dict);

        try
        {
            if (!dict.TryGetValue(indexFile, out ServeV3FileContext? identifier))
            {
                Logger.LogWriteLine($"Key reference to the pustaka file: {indexFile} is not found for game version: {gameVersion}. Please contact us on our Discord Server to report this issue.", LogType.Error, true);
                if (skipThrow) return null;
                throw new FileNotFoundException("Assets reference for repair is not found. Please contact us in GitHub issues or Discord to let us know about this issue.");
            }

            string fileDictKey = ServeV3FileContext.GetHashStringAuto(indexFile, identifier);
            string fileUrl     = mainUrl.CombineURLFromString(fileDictKey);

            CDNCacheResult result = await client.TryGetCachedStreamFrom(fileUrl, token: token);
            identifier.Stream = DaftarPustakaUtil.Reader.CreateCryptoStream(result.Stream, identifier);

            return identifier;
        }
        catch (Exception ex)
        {
            Logger.LogWriteLine($"[Senadina::Identifier] Failed while fetching Senadina's identifier kind: {indexFile} from URL: {mainUrl}\r\n{ex}", LogType.Error, true);
            if (!skipThrow) throw;

            Logger.LogWriteLine($"[Senadina::Identifier] Trying to get Senadina's identifier kind: {indexFile} from secondary URL: {secondaryUrl}", LogType.Warning, true);
            return await client.GetSenadinaIdentifierKind(dict, indexFile, gameVersion, null, secondaryUrl, true, token);
        }
    }

    private static async Task<Dictionary<string, ServeV3FileContext>?>
        GetSenadinaManifestAsync(this HttpClient   client,
                                 string?           mainUrl,
                                 string?           secondaryUrl,
                                 bool              throwIfFail = false,
                                 CancellationToken token       = default)
    {
        mainUrl ??= secondaryUrl;
        try
        {
            string             identifierUrl = mainUrl.CombineURLFromString("index.v3");
            await using Stream remoteStream = (await client.TryGetCachedStreamFrom(identifierUrl, token: token)).Stream;
            await using Stream remoteStreamDecrypt = DaftarPustakaUtil.Reader.CreateCryptoStream(remoteStream);

#if DEBUG
            using StreamReader rd       = new(remoteStreamDecrypt);
            string             response = await rd.ReadToEndAsync(token);
            Logger.LogWriteLine($"[HonkaiRepair::GetSenadinaIdentifierDictionary() Dictionary Response:\r\n{response}", LogType.Debug, true);
            return response.Deserialize(ServeV3FileContextContext.Default.DictionaryStringServeV3FileContext);
#else
            return await remoteStreamDecrypt.DeserializeAsync(ServeV3FileContextContext.Default.DictionaryStringServeV3FileContext, token: token);
#endif
        }
        catch (Exception ex)
        {
            Logger.LogWriteLine($"[Senadina::DaftarPustaka] Failed while fetching Senadina's daftar-pustaka from URL: {mainUrl}\r\n{ex}", LogType.Error, true);
            if (throwIfFail)
            {
                throw;
            }
            Logger.LogWriteLine($"[Senadina::DaftarPustaka] Trying to get Senadina's daftar-pustaka from secondary URL: {secondaryUrl}", LogType.Warning, true);
            return await GetSenadinaManifestAsync(client, null, secondaryUrl, true, token);
        }
    }
}
