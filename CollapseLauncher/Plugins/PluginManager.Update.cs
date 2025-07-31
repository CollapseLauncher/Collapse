using CollapseLauncher.Extension;
using CollapseLauncher.Helper;
using CollapseLauncher.Helper.StreamUtility;
using Hi3Helper;
using Hi3Helper.Data;
using Hi3Helper.Plugin.Core;
using Hi3Helper.Plugin.Core.Update;
using Hi3Helper.Plugin.Core.Utility;
using Hi3Helper.Shared.Region;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices.Marshalling;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;

#nullable enable
namespace CollapseLauncher.Plugins;

internal static partial class PluginManager
{
    private unsafe delegate void GetPluginUpdateCdnListDelegate(int* count, ushort*** ptr);

    internal static async Task<(List<(string, SelfUpdateReturnInfo)>, bool)> StartUpdateBackgroundRoutine()
    {
        int updated = 0;
        string rootPluginDir = LauncherConfig.AppPluginFolder;
        List<(string, SelfUpdateReturnInfo)> pluginUpdateNameList = [];

        await Parallel.ForEachAsync(PluginInstances, Impl);
        return (pluginUpdateNameList, updated > 0);

        async ValueTask Impl(KeyValuePair<string, PluginInfo> pluginInfo, CancellationToken cancelToken)
        {
            if (!pluginInfo.Value.IsLoaded)
            {
                return;
            }

            string pluginUpdateOutputPath = Path.Combine(rootPluginDir, pluginInfo.Key, PluginInfo.MarkPendingUpdateFileName);

            if (IsHasManagedUpdate(pluginInfo.Value, out string[]? cdnList))
            {
                using HttpClient httpClient = new HttpClientBuilder()
                                             .UseLauncherConfig()
                                             .Create();

                (string? reachableCdn, PluginManifest? managedUpdateManifest) =
                    await TryGetCdnFromList(httpClient, cdnList, cancelToken);

                if (reachableCdn != null &&
                    managedUpdateManifest != null &&
                    pluginInfo.Value.Version != managedUpdateManifest.PluginVersion &&
                    await TryPerformManagedUpdateAsync(pluginUpdateOutputPath,
                                                       httpClient,
                                                       reachableCdn,
                                                       managedUpdateManifest,
                                                       cancelToken))
                {
                    lock (pluginUpdateNameList)
                    {
                        nint dummyResultPtr = SelfUpdateReturnInfo
                           .CreateToNativeMemory(SelfUpdateReturnCode.UpdateSuccess,
                                                 managedUpdateManifest.MainPluginName,
                                                 managedUpdateManifest.MainPluginAuthor,
                                                 managedUpdateManifest.MainPluginDescription,
                                                 managedUpdateManifest.PluginVersion,
                                                 managedUpdateManifest.PluginStandardVersion,
                                                 managedUpdateManifest.PluginCreationDate,
                                                 managedUpdateManifest.ManifestDate);

                        SelfUpdateReturnInfo dummyResult = ReplicateFromPtr(dummyResultPtr);
                        pluginUpdateNameList.Add((pluginInfo.Key, dummyResult));
                    }
                    Interlocked.Increment(ref updated);

                    string updateCompletedStampPath = Path.Combine(pluginUpdateOutputPath, PluginInfo.MarkPendingUpdateApplyFileName);
                    await File.WriteAllTextAsync(updateCompletedStampPath, "Update Completed!", cancelToken);
                    return;
                }
            }

            bool isDisposeReturnInfo = true;
            IPlugin? pluginInstance = pluginInfo.Value.Instance;
            if (pluginInstance == null)
            {
                throw new NullReferenceException("Plugin Instance cannot be null!");
            }

            pluginInstance.GetPluginSelfUpdater(out IPluginSelfUpdate? selfUpdateInstance);
            if (selfUpdateInstance == null)
            {
                return;
            }

            selfUpdateInstance.TryPerformUpdateAsync(pluginUpdateOutputPath,
                                                     true,
                                                     null,
                                                     pluginInstance.RegisterCancelToken(cancelToken),
                                                     out nint asyncResult);

            nint checkUpdateStatusP = await asyncResult.AsTask<nint>();
            SelfUpdateReturnInfo selfUpdateReturnInfo = ReplicateFromPtr(checkUpdateStatusP);
            SelfUpdateReturnInfo updateRoutineStatusInfo = default;
            SelfUpdateReturnCode checkUpdateStatus = selfUpdateReturnInfo.ReturnCode;

            try
            {
                if (checkUpdateStatus.HasFlag(SelfUpdateReturnCode.Error))
                {
                    Logger.LogWriteLine($"Cannot check update status for plugin: {pluginInfo.Key} due to an error with return code: {checkUpdateStatus}", LogType.Error, true);
                    return;
                }

                if (checkUpdateStatus == SelfUpdateReturnCode.NoAvailableUpdate)
                {
                    return;
                }
                Logger.LogWriteLine($"Update is available for: {pluginInfo.Key}! Starting update routine...", LogType.Default, true);

                selfUpdateInstance.TryPerformUpdateAsync(pluginUpdateOutputPath,
                                                         false,
                                                         null,
                                                         pluginInstance.RegisterCancelToken(cancelToken),
                                                         out asyncResult);

                nint updateRoutineStatusP = await asyncResult.AsTask<nint>();
                updateRoutineStatusInfo = ReplicateFromPtr(updateRoutineStatusP);

                SelfUpdateReturnCode updateRoutineStatus = updateRoutineStatusInfo.ReturnCode;

                // Increase the count if update is successful
                if (updateRoutineStatus is SelfUpdateReturnCode.UpdateSuccess or SelfUpdateReturnCode.RollingBackSuccess)
                {
                    Logger.LogWriteLine($"Update for: {pluginInfo.Key} is successful! Return code: {updateRoutineStatus}", LogType.Default, true);
                    string updateCompletedStampPath = Path.Combine(pluginUpdateOutputPath, PluginInfo.MarkPendingUpdateApplyFileName);
                    await File.WriteAllTextAsync(updateCompletedStampPath, "Update Completed!", cancelToken);

                    lock (pluginUpdateNameList)
                    {
                        pluginUpdateNameList.Add((pluginInfo.Key, selfUpdateReturnInfo));
                    }
                    Interlocked.Increment(ref updated);
                    Interlocked.Exchange(ref isDisposeReturnInfo, false);
                    return;
                }

                Logger.LogWriteLine($"Failed while trying to update plugin: {pluginInfo.Key}, Rolling Back! Return code: {updateRoutineStatus}", LogType.Error, true);
                DirectoryInfo dirInfo = new DirectoryInfo(pluginUpdateOutputPath);
                if (dirInfo.Exists)
                {
                    dirInfo.TryDeleteDirectory(true);
                }
            }
            finally
            {
                if (isDisposeReturnInfo)
                {
                    selfUpdateReturnInfo.Dispose();
                    updateRoutineStatusInfo.Dispose();
                }
            }
        }
    }

    private static async Task<bool> TryPerformManagedUpdateAsync(string            outputDir,
                                                                 HttpClient        client,
                                                                 string            cdnUrl,
                                                                 PluginManifest    manifest,
                                                                 CancellationToken token)
    {
        bool hasManifestIncluded =
            manifest
               .Assets
               .FirstOrDefault(x => x.FilePath.Equals(ManifestPrefix, StringComparison.OrdinalIgnoreCase)) != null;

        if (!hasManifestIncluded)
        {
            manifest.Assets.Add(new PluginManifestAssetInfo
            {
                FileHash = [],
                FilePath = ManifestPrefix,
                Size     = 0
            });
        }

        try
        {
            await Parallel.ForEachAsync(manifest.Assets, new ParallelOptions
            {
                MaxDegreeOfParallelism = LauncherConfig.AppCurrentDownloadThread,
                CancellationToken      = token
            }, Impl);

            return true;
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
            return false;
        }

        async ValueTask Impl(PluginManifestAssetInfo asset, CancellationToken innerToken)
        {
            string  filePath = Path.Combine(outputDir, asset.FilePath);
            string? fileDir  = Path.GetDirectoryName(filePath);
            string  fileUrl  = cdnUrl.CombineUrlFromString(asset.FilePath);

            if (!string.IsNullOrEmpty(fileDir))
            {
                Directory.CreateDirectory(fileDir);
            }

            byte[] buffer = ArrayPool<byte>.Shared.Rent(4 << 10);

            MD5 hasher = MD5.Create();
            int read;

            using HttpResponseMessage response =
                await client.GetAsync(fileUrl, HttpCompletionOption.ResponseHeadersRead, innerToken);
            await using Stream     responseStream = await response.Content.ReadAsStreamAsync(innerToken);
            await using FileStream fileStream     = File.Create(filePath);

            while ((read = await responseStream.ReadAsync(buffer, innerToken)) > 0)
            {
                hasher.TransformBlock(buffer, 0, read, buffer, 0);
                await fileStream.WriteAsync(buffer.AsMemory(0, read), innerToken);
            }

            hasher.TransformFinalBlock(buffer, 0, read);

            if (filePath.EndsWith(ManifestPrefix, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            byte[] hash = hasher.Hash!;

            if (asset.FileHash.SequenceEqual(hash))
            {
                return;
            }

            Array.Reverse(hash);
            if (asset.FileHash.SequenceEqual(hash))
            {
                return;
            }

            Array.Reverse(hash);
            throw new InvalidDataException($"!Cannot update as the data hash isn't matching! {fileUrl} ({HexTool.BytesToHexUnsafe(hash)} Local != {HexTool.BytesToHexUnsafe(asset.FileHash)} Remote)");
        }
    }

    private static async Task<(string?, PluginManifest?)> TryGetCdnFromList(HttpClient client, string[] cdnList, CancellationToken token)
    {
        string[] randomizedUrls = new string[cdnList.Length];
        Random.Shared.GetItems(cdnList, randomizedUrls.AsSpan());

        foreach (string currentCdnUrl in randomizedUrls)
        {
            string manifestUrl = currentCdnUrl.CombineUrlFromString(ManifestPrefix);
            using HttpResponseMessage response =
                await client.GetAsync(manifestUrl, HttpCompletionOption.ResponseHeadersRead, token);

            if (!response.IsSuccessStatusCode)
            {
                continue;
            }

            try
            {
                PluginManifest? manifest =
                    await (await response.Content.ReadAsStreamAsync(token))
                       .DeserializeAsync(PluginManifestContext.Default.PluginManifest, token: token);

                if (manifest == null)
                {
                    throw new NullReferenceException("Manifest returns a null response!");
                }

                string logMessage = "  + Manifest Parsed!\r\n" +
                                    $" + MainLibraryName: {manifest.MainLibraryName}\r\n" +
                                    $" + MainPluginName: {manifest.MainPluginName}\r\n" +
                                    $" + MainPluginAuthor: {manifest.MainPluginAuthor}\r\n" +
                                    $" + MainPluginDescription: {manifest.MainPluginDescription}\r\n" +
                                    $" + PluginStandardVersion: {manifest.PluginStandardVersion}\r\n" +
                                    $" + PluginVersion: {manifest.PluginVersion}\r\n" +
                                    $" + PluginCreationDate: {manifest.PluginCreationDate}\r\n" +
                                    $" + ManifestDate: {manifest.ManifestDate}\r\n" +
                                    $" + Assets: {manifest.Assets.Count} assets found";
                Logger.LogWriteLine(logMessage, LogType.Debug, true);
                return (currentCdnUrl, manifest);
            }
            catch (Exception e)
            {
                Logger.LogWriteLine($"Failed while trying to parse manifest from {manifestUrl}\r\n{e}", LogType.Error, true);
            }
        }

        return (null, null);
    }

    private static unsafe bool IsHasManagedUpdate(PluginInfo pluginInfo, [NotNullWhen(true)] out string[]? cdnList)
    {
        Unsafe.SkipInit(out cdnList);

        try
        {
            if (!PluginInfo.TryGetExport(pluginInfo.Handle, "GetPluginUpdateCdnList",
                                         out GetPluginUpdateCdnListDelegate callback))
            {
                return false;
            }

            ushort** urlsPtr = null;
            int      count   = 0;
            callback(&count, &urlsPtr);

            if (count == 0 || urlsPtr == null)
            {
                return false;
            }

            string[] urlList = GC.AllocateUninitializedArray<string>(count);
            for (int i = 0; i < count; i++)
            {
                ushort* ptr = urlsPtr[i];
                urlList[i] = Utf16StringMarshaller.ConvertToManaged(ptr) ?? "";
                Utf16StringMarshaller.Free(ptr);
            }

            cdnList = urlList;
            return true;
        }
        catch (Exception e)
        {
            Logger.LogWriteLine($"Failed while trying to obtain managed Update CDN URL list for plugin: {pluginInfo.Name}\r\n{e}", LogType.Error, true);
            return false;
        }
    }

    private static bool IsValidReturnCode(SelfUpdateReturnCode retCode)
    {
        uint asUint = (uint)retCode;
        if (retCode.HasFlag(SelfUpdateReturnCode.Ok) &&
            asUint is >= 0b_00000001_00000000_00000000_00000000 and <= 0b_10000000_00000000_00000000_00000000)
        {
            return true;
        }

        if (retCode.HasFlag(SelfUpdateReturnCode.Error) &&
            asUint is >= 0b_00000000_00000000_00000001_00000000 and <= 0b_00000000_10000000_00000000_00000000)
        {
            return true;
        }

        return false;
    }

    private static unsafe SelfUpdateReturnInfo ReplicateFromPtr(nint ptr)
    {
        // Fallback if the result is actually the enum.
        SelfUpdateReturnCode asRetCode = (SelfUpdateReturnCode)(uint)ptr;
        if (IsValidReturnCode(asRetCode))
        {
            return new SelfUpdateReturnInfo((SelfUpdateReturnCode)(uint)ptr);
        }

        // Return the struct if it returns it.
        SelfUpdateReturnInfo* selfUpdateReturnInfo = ptr.AsPointer<SelfUpdateReturnInfo>();
        SelfUpdateReturnInfo  ret                  = *selfUpdateReturnInfo; // basically copy the fields.

        // Free the struct from native memory (but not the pointer inside of it since it's already copied above).
        Mem.Free(selfUpdateReturnInfo);
        return ret;
    }

    private static void ApplyPendingUpdateRoutine(DirectoryInfo pluginDir)
    {
        DirectoryInfo tempUpdateDir = new DirectoryInfo(Path.Combine(pluginDir.FullName, PluginInfo.MarkPendingUpdateFileName));
        DirectoryInfo? targetUpdateDir = tempUpdateDir.Parent;

        FileInfo stampCompletedFileInfo = new FileInfo(Path.Combine(tempUpdateDir.FullName, PluginInfo.MarkPendingUpdateApplyFileName));

        try
        {
            if (targetUpdateDir == null)
            {
                return;
            }

            if (!tempUpdateDir.Exists)
            {
                return;
            }

            if (!stampCompletedFileInfo.Exists)
            {
                return;
            }

            stampCompletedFileInfo.TryDeleteFile();

            // Cleanup old files
            foreach (FileInfo oldFileInfo in targetUpdateDir
                .EnumerateFiles("*", SearchOption.AllDirectories)
                .Where(x => x.FullName.IndexOf(PluginInfo.MarkPendingUpdateFileName, StringComparison.OrdinalIgnoreCase) < 0))
            {
                oldFileInfo.IsReadOnly = false;
                oldFileInfo.TryDeleteFile();

                string? currentOldFileDir = oldFileInfo.DirectoryName;
                if (currentOldFileDir == null)
                {
                    return;
                }

                oldFileInfo.Refresh();
                oldFileInfo.Delete();
            }

            string tempUpdateDirPath = tempUpdateDir.FullName;
            string targetUpdateDirPath = targetUpdateDir.FullName;

            foreach (FileInfo tempFileInfo in tempUpdateDir
                        .EnumerateFiles("*", SearchOption.AllDirectories))
            {
                ReadOnlySpan<char> baseName = tempFileInfo.FullName.AsSpan(tempUpdateDirPath.Length).TrimStart("/\\");
                string newFilePath = Path.Combine(targetUpdateDirPath, baseName.ToString());
                FileInfo newFileInfo = new FileInfo(newFilePath);

                newFileInfo.Directory?.Create();
                tempFileInfo.TryMoveTo(newFilePath);
            }

            pluginDir.DeleteEmptyDirectory();
        }
        catch (Exception ex)
        {
            Logger.LogWriteLine($"Cannot apply the update to plugin: {pluginDir.Name} due to an error: {ex}", LogType.Error, true);
        }
        finally
        {
            if (tempUpdateDir.Exists)
            {
                tempUpdateDir.TryDeleteDirectory(true);
            }
        }
    }
}
