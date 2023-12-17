﻿using Hi3Helper;
using Hi3Helper.Data;
using Hi3Helper.EncTool;
using Hi3Helper.Http;
using Hi3Helper.Preset;
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

            if (packageProperty.segments != null && packageProperty.segments.Count > 0)
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

        public bool IsReadStreamExist(int count)
        {
            // Check if the single file exist or not
            FileInfo fileInfo = new FileInfo(PathOutput);
            if (fileInfo.Exists)
                return true;

            // Check for the chunk files
            return Enumerable.Range(0, count).All(chunkID =>
            {
                // Get the hash number
                long ID = Http.GetHashNumber(count, chunkID);
                // Append the hash number to the path
                string path = $"{PathOutput}.{ID}";
                // Get the file info
                FileInfo fileInfo = new FileInfo(path);
                // Check if the file exist
                return fileInfo.Exists;
            });
        }

        public Stream GetReadStream(int count)
        {
            // Get the file info of the single file
            FileInfo fileInfo = new FileInfo(PathOutput);
            // Check if the file exist and the length is equal to the size
            if (fileInfo.Exists && fileInfo.Length == Size)
            {
                // Return the stream for read
                return fileInfo.Open(new FileStreamOptions
                {
                    Access = FileAccess.Read,
                    BufferSize = 4 << 10,
                    Mode = FileMode.Open,
                    Options = FileOptions.None,
                    Share = FileShare.Read
                });
            }

            // If the single file doesn't exist, then try getting chunk stream
            return GetCombinedStreamFromPackageAsset(count);
        }

        public long GetStreamLength(int count)
        {
            // Get the file info of the single file
            FileInfo fileInfo = new FileInfo(PathOutput);
            // Check if the file exist and the length is equal to the size
            if (fileInfo.Exists && fileInfo.Length == Size)
            {
                // Return the stream for read
                return fileInfo.Length;
            }

            // If the single file doesn't exist, then try getting chunk stream
            return GetCombinedLengthFromPackageAsset(count);
        }

        private CombinedStream GetCombinedStreamFromPackageAsset(int count)
        {
            // Set the array
            FileStream[] streamList = new FileStream[count];
            // Enumerate the ID
            for (int i = 0; i < streamList.Length; i++)
            {
                // Get the hash ID
                long ID = Http.GetHashNumber(count, i);
                // Append hash ID to the path
                string path = $"{PathOutput}.{ID}";
                // Get the file info and check if the file exist
                FileInfo fileInfo = new FileInfo(path);
                if (fileInfo.Exists)
                {
                    // Allocate to the array and open the stream
                    streamList[i] = fileInfo.Open(new FileStreamOptions
                    {
                        Access = FileAccess.Read,
                        BufferSize = 4 << 10,
                        Mode = FileMode.Open,
                        Options = FileOptions.None,
                        Share = FileShare.Read
                    });
                    // Then go back to the loop routine
                    continue;
                }

                // If not found, then throw
                throw new FileNotFoundException($"File chunk doesn't exist in this path! -> {path}");
            }

            // Assign the array and initiate it as a combined stream
            return new CombinedStream(streamList);
        }

        private long GetCombinedLengthFromPackageAsset(int count)
        {
            // Initialize length
            long length = 0;
            // Enumerate the ID
            for (int i = 0; i < count; i++)
            {
                // Get the hash ID
                long ID = Http.GetHashNumber(count, i);
                // Append hash ID to the path
                string path = $"{PathOutput}.{ID}";
                // Get the file info and check if the file exist
                FileInfo fileInfo = new FileInfo(path);
                if (fileInfo.Exists)
                {
                    // Add length to the existing one
                    length += fileInfo.Length;
                    // Then go back to the loop routine
                    continue;
                }
            }

            // Return the length
            return length;
        }

        public void DeleteFile(int count)
        {
            string lastFile = PathOutput;
            try
            {
                FileInfo fileInfo = new FileInfo(PathOutput);
                if (fileInfo.Exists && fileInfo.Length == Size)
                {
                    fileInfo.Delete();
                }

                for (int i = 0; i < count; i++)
                {
                    long ID = Http.GetHashNumber(count, i);
                    string path = $"{PathOutput}.{ID}";
                    lastFile = path;
                    fileInfo = new FileInfo(path);
                    if (fileInfo.Exists)
                    {
                        fileInfo.Delete();
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.LogWriteLine($"Failed while deleting file: {lastFile}. Skipping!\r\n{ex}", LogType.Warning, true);
            }
        }

        public string PrintSummary() => $"File [T: {PackageType}]: {URL}\t{ConverterTool.SummarizeSizeSimple(Size)} ({Size} bytes)";
        public long GetAssetSize() => Size;
        public string GetRemoteURL() => URL;
        public void SetRemoteURL(string url) => URL = url;
    }
}
