using CollapseLauncher.Helper.StreamUtility;
using System;
using System.IO;
using System.Text.Json.Serialization.Metadata;
using System.Threading.Tasks;

#pragma warning disable IDE0130

#nullable enable
namespace CollapseLauncher.Helper.Metadata;

internal static partial class MetadataHelper
{
    [Flags]
    private enum ConfigFileStatus : uint
    {
        Ok,
        FileModified,
        FileMissing
    }

    private static partial class Util
    {
        public static ConfigFileStatus GetConfigFileStatus(Stamp stamp)
        {
            if (stamp.MetadataType == MetadataType.PresetConfigPlugin) // Skip for plugins
            {
                return ConfigFileStatus.Ok;
            }

            string filePath = Path.Combine(LauncherMetadataDirectory, stamp.MetadataPath);
            if (!File.Exists(filePath))
            {
                return ConfigFileStatus.FileMissing;
            }

            DateTime lastTimeDate    = stamp.LastModifiedTimeUtc;
            DateTime currentTimeDate = stamp.GetCurrentFileModifiedTime(LauncherMetadataDirectory);

            return lastTimeDate == currentTimeDate
                ? ConfigFileStatus.Ok
                : ConfigFileStatus.FileModified;
        }

        public static async Task<T> LoadFileAsync<T>(Stamp stamp, JsonTypeInfo<T?> deserializerType)
            where T : class
        {
            bool isRetry = false;

        Load:
            try
            {
                string filePath = Path.Combine(LauncherMetadataDirectory, stamp.MetadataPath);
                if (!File.Exists(filePath))
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(filePath) ?? LauncherMetadataDirectory);

                    string fileSuffixUri = string.Format(LauncherMetadataSuffixPathTemplate,
                                                         MetadataVersion,
                                                         LauncherReleaseChannel,
                                                         stamp.MetadataPath);

                    await using Stream     remoteStream = await FallbackCDNUtil.TryGetCDNFallbackStream(fileSuffixUri);
                    await using FileStream fileStream   = File.Create(filePath);
                    await remoteStream.CopyToAsync(fileStream);
                }

                stamp.SetFileModifiedTime(LauncherMetadataDirectory);

                await using FileStream loadStream = File.Open(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                T content = await loadStream.DeserializeAsync(deserializerType) ?? throw new NullReferenceException($"File config: {stamp.MetadataPath} seems to be empty!");

                return content;
            }
            catch
            {
                if (isRetry)
                {
                    throw;
                }

                isRetry = true;
                goto Load;
            }
        }

        public static async Task<T> LoadRemoteStreamAsync<T>(Stamp stamp, JsonTypeInfo<T?> deserializerType)
            where T : class
        {
            string fileSuffixUri = string.Format(LauncherMetadataSuffixPathTemplate,
                                                 MetadataVersion,
                                                 LauncherReleaseChannel,
                                                 stamp.MetadataPath);
            await using Stream remoteStream = await FallbackCDNUtil.TryGetCDNFallbackStream(fileSuffixUri);
            return await remoteStream.DeserializeAsync(deserializerType) ?? throw new NullReferenceException($"File config: {stamp.MetadataPath} seems to be empty!");
        }

        public static void RemoveFile(Stamp stamp)
        {
            string   filePath = Path.Combine(LauncherMetadataDirectory, stamp.MetadataPath);
            FileInfo fileInfo = new(filePath);
            if (fileInfo.Exists)
            {
                fileInfo.TryDeleteFile();
            }
        }
    }
}
