using CollapseLauncher.Helper.LauncherApiLoader.HoYoPlay;
using CollapseLauncher.Helper.StreamUtility;
using Hi3Helper;
using Hi3Helper.Data;
using Hi3Helper.EncTool;
using Hi3Helper.Plugin.Core.Management;
using Hi3Helper.Preset;
using Hi3Helper.SentryHelper;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
#pragma warning disable IDE0130

#pragma warning disable CS0618 // Type or member is obsolete
namespace CollapseLauncher.InstallManager
{
    [SuppressMessage("ReSharper", "UnusedAutoPropertyAccessor.Global")]
    internal class GameInstallPackage : IAssetIndexSummary
    {
        #region Properties
        public string                   URL                   { get; private set; }
        public string                   DecompressedURL       { get; set; }
        public string                   Name                  { get; init; }
        public string                   PathOutput            { get; init; }
        public GameInstallPackageType   PackageType           { get; init; }
        public long                     Size                  { get; set; }
        public long                     SizeRequired          { get; init; }
        public long                     SizeDownloaded        { get; set; }
        public GameVersion              Version               { get; set; }
        public byte[]                   Hash                  { get; init; }
        public string                   HashString            { get => field ??= HexTool.BytesToHexUnsafe(Hash); }
        public string                   LanguageID            { get; init; }
        public string                   RunCommand            { get; init; }
        public string                   PluginId              { get; init; }
        public bool                     IsUseLegacyDownloader { get; set; }
        public List<GameInstallPackage> ChunkList             { get; set; } = [];
        public object                   SourceObject          { get; set; }
        #endregion

        public GameInstallPackage(HypChannelSdkData packageProperty,
                                  string            pathOutput,
                                  string            uncompressedUrl = null)
        {
            ArgumentNullException.ThrowIfNull(packageProperty);
            ArgumentNullException.ThrowIfNull(packageProperty.SdkPackageDetail);
            ArgumentException.ThrowIfNullOrEmpty(pathOutput);

            SourceObject = packageProperty;

            PluginId    = "sdk";
            RunCommand  = packageProperty.SdkPackageDetail.PackageRunCommand;
            Version     = packageProperty.Version;
            PackageType = GameInstallPackageType.Plugin;

            if (packageProperty.SdkPackageDetail.FilePath != null)
            {
                URL        = packageProperty.SdkPackageDetail.FilePath;
                Name       = Path.GetFileName(packageProperty.SdkPackageDetail.FilePath);
                PathOutput = Path.Combine(pathOutput, Name);
            }
            else if (packageProperty.SdkPackageDetail.Url != null)
            {
                URL        = packageProperty.SdkPackageDetail.Url;
                Name       = Path.GetFileName(packageProperty.SdkPackageDetail.Url);
                PathOutput = Path.Combine(pathOutput, Name);
            }

            DecompressedURL = uncompressedUrl;
            Hash            = packageProperty.SdkPackageDetail.PackageMD5Hash;
            SizeRequired    = packageProperty.SdkPackageDetail.PackageSize;
            Version         = packageProperty.Version;
        }

        public GameInstallPackage(HypPluginPackageInfo packageProperty,
                                  string               pathOutput,
                                  string               uncompressedUrl = null)
        {
            ArgumentNullException.ThrowIfNull(packageProperty);
            ArgumentNullException.ThrowIfNull(packageProperty.PluginPackage);
            ArgumentException.ThrowIfNullOrEmpty(pathOutput);

            SourceObject = packageProperty;

            PluginId    = packageProperty.PluginId;
            RunCommand  = packageProperty.PluginPackage.PackageRunCommand;
            Version     = packageProperty.Version;
            PackageType = GameInstallPackageType.Plugin;

            if (packageProperty.PluginPackage.FilePath != null)
            {
                URL        = packageProperty.PluginPackage.FilePath;
                Name       = Path.GetFileName(packageProperty.PluginPackage.FilePath);
                PathOutput = Path.Combine(pathOutput, Name);
            }
            else if (packageProperty.PluginPackage.Url != null)
            {
                URL        = packageProperty.PluginPackage.Url;
                Name       = Path.GetFileName(packageProperty.PluginPackage.Url);
                PathOutput = Path.Combine(pathOutput, Name);
            }

            DecompressedURL = uncompressedUrl;
            Hash            = packageProperty.PluginPackage.PackageMD5Hash;
            SizeRequired    = packageProperty.PluginPackage.PackageSize;
            Version         = packageProperty.Version;
        }

        public GameInstallPackage(HypPackageData packageProperty,
                                  string         pathOutput,
                                  string         uncompressedUrl = null,
                                  GameVersion    version         = default)
        {
            SourceObject = packageProperty;

            if (packageProperty == null || pathOutput == null) throw new NullReferenceException();

            if (packageProperty.FilePath != null)
            {
                URL        = packageProperty.FilePath;
                Name       = Path.GetFileName(packageProperty.FilePath);
                PathOutput = Path.Combine(pathOutput, Name);
            }
            else if (packageProperty.Url != null)
            {
                URL        = packageProperty.Url;
                Name       = Path.GetFileName(packageProperty.Url);
                PathOutput = Path.Combine(pathOutput, Name);
            }

            DecompressedURL = uncompressedUrl;
            Hash            = packageProperty.PackageMD5Hash;
            SizeRequired    = packageProperty.PackageSize;
            Version         = version;

            if (packageProperty.Language != null)
            {
                LanguageID = packageProperty.Language;
            }
        }

        private GameInstallPackage() { }

        public GameInstallPackage Clone()
        {
            return SourceObject switch
            {
                HypChannelSdkData asSdkPackage => new GameInstallPackage(asSdkPackage, Path.GetDirectoryName(PathOutput), uncompressedUrl: DecompressedURL),
                HypPluginPackageInfo asPluginPackage => new GameInstallPackage(asPluginPackage, Path.GetDirectoryName(PathOutput), uncompressedUrl: DecompressedURL),
                HypPackageData asPackage => new GameInstallPackage(asPackage, Path.GetDirectoryName(PathOutput), uncompressedUrl: DecompressedURL),
                _ => new GameInstallPackage
                {
                    URL                   = URL,
                    DecompressedURL       = DecompressedURL,
                    Name                  = Name,
                    PathOutput            = Path.GetDirectoryName(PathOutput),
                    PackageType           = PackageType,
                    Size                  = Size,
                    SizeRequired          = SizeRequired,
                    SizeDownloaded        = SizeDownloaded,
                    Version               = Version,
                    Hash                  = Hash,
                    LanguageID            = LanguageID,
                    RunCommand            = RunCommand,
                    PluginId              = PluginId,
                    IsUseLegacyDownloader = IsUseLegacyDownloader,
                    ChunkList             = ChunkList,
                    SourceObject          = SourceObject
                }
            };
        }

        public bool IsReadStreamExist()
        {
            return ChunkList.Count == 0
                ? File.Exists(PathOutput)
                : ChunkList.All(x => File.Exists(x.PathOutput));
        }

        public Stream GetReadStream()
        {
            if (ChunkList.Count == 0)
            {
                FileInfo fileInfo = new FileInfo(PathOutput!)
                                   .ResolveSymlink()
                                   .StripAlternateDataStream()
                                   .EnsureNoReadOnly();
                // Return the stream for read
                return fileInfo.Open(new FileStreamOptions
                {
                    Access     = FileAccess.Read,
                    BufferSize = 4 << 10,
                    Mode       = FileMode.Open,
                    Options    = FileOptions.None,
                    Share      = FileShare.Read
                });
            }

            var streams = new FileStream[ChunkList.Count];
            for (int i = 0; i < ChunkList.Count; i++)
            {
                GameInstallPackage chunk = ChunkList[i];
                FileInfo fileInfo = new FileInfo(chunk.PathOutput)
                                   .ResolveSymlink()
                                   .StripAlternateDataStream()
                                   .EnsureNoReadOnly();

                if (!fileInfo.Exists)
                {
                    throw new FileNotFoundException($"File: {chunk.PathOutput} is missing and cannot be merged!");
                }

                streams[i] = fileInfo.Open(new FileStreamOptions
                {
                    Access     = FileAccess.Read,
                    BufferSize = 4 << 10,
                    Mode       = FileMode.Open,
                    Options    = FileOptions.None,
                    Share      = FileShare.Read
                });
            }

            return new CombinedStream(streams);
        }

        public long GetStreamLength()
        {
            if (ChunkList.Count != 0)
            {
                return ChunkList.Sum(static x =>
                {
                    FileInfo fileInfo = new(x.PathOutput);
                    return fileInfo.Exists ? fileInfo.Length : 0;
                });
            }

            FileInfo fileInfo = new(PathOutput);
            return fileInfo.Exists ? fileInfo.Length : 0;
        }

        public void DeleteFile()
        {
            string lastFile = PathOutput;
            try
            {
                if (ChunkList.Count == 0)
                {
                    FileInfo fileInfo = new(PathOutput);
                    fileInfo.TryDeleteFile(true);
                    return;
                }

                foreach (GameInstallPackage chunk in ChunkList)
                {
                    lastFile = chunk.PathOutput;
                    FileInfo fileInfo = new(chunk.PathOutput);
                    fileInfo.TryDeleteFile(true);
                    return;
                }
            }
            catch (Exception ex)
            {
                SentryHelper.ExceptionHandler(ex, SentryHelper.ExceptionType.UnhandledOther);
                Logger.LogWriteLine($"Failed while deleting file: {lastFile}. Skipping!\r\n{ex}", LogType.Warning, true);
            }
        }

        public string PrintSummary() => $"File [T: {PackageType}]: {URL}\t{ConverterTool.SummarizeSizeSimple(Size)} ({Size} bytes)";
        public long GetAssetSize() => ChunkList.Count > 0 ? ChunkList.Sum(x => x.Size) : Size;
        public string GetRemoteURL() => URL;
        public void SetRemoteURL(string url) => URL = url;
    }
}
