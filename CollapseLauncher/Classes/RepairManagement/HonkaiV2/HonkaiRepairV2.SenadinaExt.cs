using CollapseLauncher.Extension;
using CollapseLauncher.Interfaces;
using Hi3Helper;
using Hi3Helper.Data;
using Hi3Helper.EncTool;
using Hi3Helper.EncTool.Parser.AssetIndex;
using Hi3Helper.EncTool.Parser.Senadina;
using Hi3Helper.Plugin.Core.Management;
using Hi3Helper.Preset;
using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
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
                string.Format(Locale.Lang._CachesPage.CachesStatusFetchingType, "Senadina Files");
            progressibleInstance.Status.IsProgressAllIndetermined = true;
            progressibleInstance.Status.IsIncludePerFileIndicator = false;
            progressibleInstance.UpdateStatus();
        }

        // Get the Senadina File Identifier Dictionary and its file references
        var senadinaManifest
            = await client.GetSenadinaManifestAsync(mainUrl,
                                                    secondaryUrl,
                                                    false,
                                                    token);

        SenadinaFileIdentifier? senadinaAudioManifestIdentity  = null;
        SenadinaFileIdentifier? senadinaXmfMetaIdentity        = null;
        SenadinaFileIdentifier? senadinaXmfInfoBaseIdentity    = null;
        SenadinaFileIdentifier? senadinaXmfInfoCurrentIdentity = null;
        SenadinaFileIdentifier? senadinaXmfPatchIdentity       = null;

        Task senadinaAudioManifestTask =
            client.GetSenadinaIdentifierKind(senadinaManifest,
                                             SenadinaKind.chiptunesCurrent,
                                             gameVersion,
                                             mainUrl,
                                             secondaryUrl,
                                             false,
                                             token)
                  .GetResultFromAction(result => senadinaAudioManifestIdentity = result);

        Task senadinaXmfMetaTask =
            client.GetSenadinaIdentifierKind(senadinaManifest,
                                             SenadinaKind.platformBase,
                                             gameVersion,
                                             mainUrl,
                                             secondaryUrl,
                                             false,
                                             token)
                  .GetResultFromAction(result => senadinaXmfMetaIdentity = result);

        Task senadinaXmfInfoBaseTask =
            client.GetSenadinaIdentifierKind(senadinaManifest,
                                             SenadinaKind.bricksBase,
                                             gameVersion,
                                             mainUrl,
                                             secondaryUrl,
                                             false,
                                             token)
                  .GetResultFromAction(result => senadinaXmfInfoBaseIdentity = result);

        Task senadinaXmfInfoCurrentTask =
            client.GetSenadinaIdentifierKind(senadinaManifest,
                                             SenadinaKind.bricksCurrent,
                                             gameVersion,
                                             mainUrl,
                                             secondaryUrl,
                                             false,
                                             token)
                  .GetResultFromAction(result => senadinaXmfInfoCurrentIdentity = result);

        Task senadinaXmfPatchTask =
            client.GetSenadinaIdentifierKind(senadinaManifest,
                                             SenadinaKind.wandCurrent,
                                             gameVersion,
                                             mainUrl,
                                             secondaryUrl,
                                             false,
                                             token)
                  .GetResultFromAction(result => senadinaXmfPatchIdentity = result);

        await Task.WhenAll(senadinaAudioManifestTask,
                           senadinaXmfMetaTask,
                           senadinaXmfInfoBaseTask,
                           senadinaXmfInfoCurrentTask,
                           senadinaXmfPatchTask);

        if (throwIfFail &&
            (senadinaAudioManifestIdentity == null ||
             senadinaXmfMetaIdentity == null ||
             senadinaXmfInfoBaseIdentity == null ||
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
            XmfInfoBase    = senadinaXmfInfoBaseIdentity,
            XmfInfoCurrent = senadinaXmfInfoCurrentIdentity,
            XmfPatch       = senadinaXmfPatchIdentity
        };
    }

    private static async Task ThrowIfFileIsNotSenadina(this Stream stream, CancellationToken token)
    {
        ArgumentNullException.ThrowIfNull(stream);

        Memory<byte> header = new byte[AssetBundleReference.CollapseHeaderBytes.Length];
        _ = await stream.ReadAsync(header, token);
        if (!header.Span.SequenceEqual(AssetBundleReference.CollapseHeaderBytes))
            throw new InvalidDataException($"Daftar pustaka file is corrupted! Expecting header: 0x{BinaryPrimitives.ReadInt64LittleEndian(AssetBundleReference.CollapseHeaderBytes):x8} but got: 0x{BinaryPrimitives.ReadInt64LittleEndian(header.Span):x8} instead!");
    }

    private static async Task<SenadinaFileIdentifier?>
        GetSenadinaIdentifierKind(
            this HttpClient                             client,
            Dictionary<string, SenadinaFileIdentifier>? dict,
            SenadinaKind                                kind,
            GameVersion                                 gameVersion,
            string?                                     mainUrl,
            string?                                     secondaryUrl,
            bool                                        skipThrow,
            CancellationToken                           token)
    {
        mainUrl ??= secondaryUrl;
        ArgumentNullException.ThrowIfNull(dict);

        try
        {
            string origFileRelativePath = $"{gameVersion.Major}_{gameVersion.Minor}_{kind.ToString().ToLower()}";
            string hashedRelativePath = SenadinaFileIdentifier.GetHashedString(origFileRelativePath);

            string fileUrl = mainUrl.CombineURLFromString(hashedRelativePath);
            if (!dict.TryGetValue(origFileRelativePath, out SenadinaFileIdentifier? identifier))
            {
                Logger.LogWriteLine($"Key reference to the pustaka file: {hashedRelativePath} is not found for game version: {gameVersion}. Please contact us on our Discord Server to report this issue.", LogType.Error, true);
                if (skipThrow) return null;
                throw new
                    FileNotFoundException("Assets reference for repair is not found. " +
                                          "Please contact us in GitHub issues or Discord to let us know about this issue.");
            }

            CDNCacheResult result = await client.TryGetCachedStreamFrom(fileUrl, token: token);
            Stream networkStream = result.Stream;

            await ThrowIfFileIsNotSenadina(networkStream, token);
            identifier.fileStream = SenadinaFileIdentifier.CreateKangBakso(networkStream, identifier.lastIdentifier!, origFileRelativePath, (int)identifier.fileTime);
            identifier.relativePath = origFileRelativePath;

            return identifier;
        }
        catch (Exception ex)
        {
            Logger.LogWriteLine($"[Senadina::Identifier] Failed while fetching Senadina's identifier kind: {kind} from URL: {mainUrl}\r\n{ex}", LogType.Error, true);
            if (skipThrow)
            {
                throw;
            }
            Logger.LogWriteLine($"[Senadina::Identifier] Trying to get Senadina's identifier kind: {kind} from secondary URL: {secondaryUrl}", LogType.Warning, true);
            return await GetSenadinaIdentifierKind(client, dict, kind, gameVersion, null, secondaryUrl, true, token);
        }
    }

    private static async Task<Dictionary<string, SenadinaFileIdentifier>?>
        GetSenadinaManifestAsync(this HttpClient   client,
                                 string?           mainUrl,
                                 string?           secondaryUrl,
                                 bool              throwIfFail = false,
                                 CancellationToken token       = default)
    {
        mainUrl ??= secondaryUrl;
        try
        {
            string identifierUrl = mainUrl.CombineURLFromString("daftar-pustaka");
            await using Stream fileIdentifierStream = (await client.TryGetCachedStreamFrom(identifierUrl, token: token)).Stream;
            await using Stream fileIdentifierStreamDecoder = new BrotliStream(fileIdentifierStream, CompressionMode.Decompress, true);

            await ThrowIfFileIsNotSenadina(fileIdentifierStream, token);
#if DEBUG
            using StreamReader rd = new StreamReader(fileIdentifierStreamDecoder);
            string response = await rd.ReadToEndAsync(token);
            Logger.LogWriteLine($"[HonkaiRepair::GetSenadinaIdentifierDictionary() Dictionary Response:\r\n{response}", LogType.Debug, true);
            return response.Deserialize(SenadinaJsonContext.Default.DictionaryStringSenadinaFileIdentifier);
#else
            return await fileIdentifierStreamDecoder.DeserializeAsync(SenadinaJsonContext.Default.DictionaryStringSenadinaFileIdentifier, token: token);
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
