using Hi3Helper.Data;
using Hi3Helper.Preset;
using Hi3Helper.Shared.ClassStruct;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace CollapseLauncher.InstallManager
{
    internal class GameInstallPackage : IAssetIndexSummary
    {
        #region Properties
        public string URL { get; set; }
        public string DecompressedURL { get; set; }
        public string Name { get; set; }
        public string PathOutput { get; set; }
        public GameInstallPackageType PackageType { get; set; }
        public long Size { get; set; }
        public long SizeRequired { get; set; }
        public long SizeDownloaded { get; set; }
        public GameVersion Version { get; set; }
        public byte[] Hash { get; set; }
        public string HashString { get => HexTool.BytesToHexUnsafe(Hash); }
        public int LanguageID { get; set; } = int.MinValue;
        public string LanguageName { get; set; }
        public List<GameInstallPackage> Segments { get; set; }
        #endregion

        public GameInstallPackage(RegionResourceVersion packageProperty, string pathOutput, string overrideVersion = null)
        {
            if (packageProperty.path != null)
            {
                URL = packageProperty.path;
                Name = Path.GetFileName(packageProperty.path);
                PathOutput = Path.Combine(pathOutput, Name);
            }

            DecompressedURL = packageProperty.decompressed_path;
            SizeRequired = packageProperty.size;

            if (packageProperty.version != null || overrideVersion != null)
            {
                Version = new GameVersion(overrideVersion == null ? packageProperty.version : overrideVersion);
            }

            if (!string.IsNullOrEmpty(packageProperty.md5))
            {
                Hash = HexTool.HexToBytesUnsafe(packageProperty.md5);
            }
            if (packageProperty.language != null)
            {
                LanguageID = packageProperty.languageID ?? 0;
                LanguageName = packageProperty.language;
            }

            if (packageProperty.segments != null)
            {
                Name = Path.GetFileName(packageProperty.segments.FirstOrDefault()?.path);
                PathOutput = Path.Combine(pathOutput, Name ?? "");
                Segments = new List<GameInstallPackage>();

                foreach (RegionResourceVersion segment in packageProperty.segments)
                {
                    Segments.Add(new GameInstallPackage(segment, pathOutput));
                }
            }
        }

        public string PrintSummary() => $"File [T: {PackageType}]: {URL}\t{ConverterTool.SummarizeSizeSimple(Size)} ({Size} bytes)";
        public long GetAssetSize() => Size;
    }
}
