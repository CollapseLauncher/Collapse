﻿using Hi3Helper;
using Hi3Helper.Data;
using Hi3Helper.SentryHelper;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
// ReSharper disable UnusedMember.Global
// ReSharper disable CommentTypo

#nullable enable
namespace CollapseLauncher.Helper.StreamUtility
{
    internal static partial class StreamExtension
    {
        internal const int DefaultBufferSize = 64 << 10;

        internal static readonly FileStreamOptions FileStreamOpenReadOpt = new()
        {
            Mode = FileMode.Open,
            Access = FileAccess.Read,
            Share = FileShare.Read
        };

        internal static readonly FileStreamOptions FileStreamCreateReadWriteOpt = new()
        {
            Mode = FileMode.Create,
            Access = FileAccess.ReadWrite,
            Share = FileShare.ReadWrite
        };

        /*
         * TODO: Create GetFileStream helper
        internal static FileStream GetFileStream(this string? path, FileMode fileMode, FileAccess fileAccess, FileShare fileShare)
        {
            EnsureFilePathExist(path);
            return new FileStream(path, fileMode, fileAccess, fileShare, DefaultBufferSize, true);
        }

        private static void EnsureFilePathExist([NotNull] string? path)
        {
            ArgumentException.ThrowIfNullOrEmpty(path, "path");
            if (File.Exists(path))
                throw new FileNotFoundException($"File: {path} is not found!");
        }
        */

        internal static FileInfo EnsureNoReadOnly(this FileInfo fileInfo)
            => fileInfo.EnsureNoReadOnly(out _);

        internal static FileInfo EnsureNoReadOnly(this FileInfo fileInfo, out bool isFileExist)
        {
            try
            {
                if (!(isFileExist = fileInfo.Exists))
                    return fileInfo;

                fileInfo.IsReadOnly = false;

                return fileInfo;
            }
            finally
            {
                fileInfo.Refresh();
            }
        }

        internal static DirectoryInfo EnsureNoReadOnly(this DirectoryInfo dirInfo)
            => dirInfo.EnsureNoReadOnly(out _);

        internal static DirectoryInfo EnsureNoReadOnly(this DirectoryInfo directoryInfo, out bool isDirExist)
        {
            try
            {
                if (!(isDirExist = directoryInfo.Exists))
                    return directoryInfo;

                directoryInfo.Attributes &= ~FileAttributes.ReadOnly;

                return directoryInfo;
            }
            finally
            {
                directoryInfo.Refresh();
            }
        }

        internal static IEnumerable<FileInfo> EnumerateNoReadOnly(this IEnumerable<FileInfo> enumeratedFileInfo)
            => enumeratedFileInfo.Select(x => x.StripAlternateDataStream().EnsureNoReadOnly());

        /// <summary>
        /// IDK what Microsoft is smoking but for some reason, the file were throwing IO_SharingViolation_File error,
        /// or sometimes "File is being used by another process" error even though the file HAS NEVER BEEN OPENED LIKE, WTFFF????!>!!!!!!
        /// </summary>
        internal static async ValueTask<FileStream> NaivelyOpenFileStreamAsync(this FileInfo info,
            FileMode fileMode,
            FileAccess fileAccess,
            FileShare fileShare,
            FileOptions fileOption = FileOptions.None)
        {
            const int maxTry = 10;
            int currentTry = 1;
            while (true)
            {
                try
                {
                    return info.Open(new FileStreamOptions
                    {
                        Mode = fileMode,
                        Access = fileAccess,
                        Share = fileShare,
                        Options = fileOption,
                        BufferSize = DefaultBufferSize
                    });
                }
                catch
                {
                    if (currentTry > maxTry)
                    {
                        throw; // Throw this MFs
                    }

                    Logger.LogWriteLine($"Failed while trying to open: {info.FullName}. Retry attempt: {++currentTry} / {maxTry}", LogType.Warning, true);
                    await Task.Delay(50); // Adding 50ms delay
                }
            }
        }

        internal static FileInfo EnsureCreationOfDirectory(string filePath)
            => new FileInfo(filePath).EnsureCreationOfDirectory();

        internal static FileInfo EnsureCreationOfDirectory(this FileInfo filePath)
        {
            ArgumentNullException.ThrowIfNull(filePath, nameof(filePath));
            DirectoryInfo? directoryInfo = filePath.Directory;

            try
            {
                if (directoryInfo is { Exists: false })
                    directoryInfo.Create();

                return filePath;
            }
            finally
            {
                filePath.Refresh();
            }
        }

        internal static bool TryDeleteFile(this FileInfo filePath, bool throwIfFailed = false)
        {
            try
            {
                if (filePath.Exists)
                    filePath.Delete();

                return true;
            }
            catch (Exception ex)
            {
                if (throwIfFailed)
                    throw;

                Logger.LogWriteLine($"Failed to delete file: {filePath.FullName}\r\n{ex}", LogType.Error, true);
                return false;
            }
        }

        internal static string NormalizePath(this string path)
            => ConverterTool.NormalizePath(path);

        internal static bool TryMoveTo(this FileInfo filePath, string toTarget, bool overwrite = true, bool throwIfFailed = false)
            => filePath.TryMoveTo(new FileInfo(toTarget), overwrite, throwIfFailed);

        internal static bool TryMoveTo(this FileInfo filePath, FileInfo toTarget, bool overwrite = true, bool throwIfFailed = false)
        {
            try
            {
                if (filePath.Exists)
                    filePath.MoveTo(toTarget.FullName, overwrite);

                return true;
            }
            catch (Exception ex)
            {
                if (throwIfFailed)
                    throw;

                Logger.LogWriteLine($"Failed to move file: {filePath.FullName} to: {toTarget.FullName}\r\n{ex}", LogType.Error, true);
                return false;
            }
        }

        internal static async Task<string> ReadAsStringAsync(this FileInfo fileInfo, CancellationToken token = default)
        {
            using StreamReader reader = fileInfo.OpenText();
            return await reader.ReadToEndAsync(token);
        }

        internal static async Task<string> ReadAsStringAsync<T>(this T stream, CancellationToken token = default)
            where T : Stream
        {
            using StreamReader reader = new StreamReader(stream, null, true, -1, false);
            return await reader.ReadToEndAsync(token);
        }

        internal static async Task<string> ReadAsStringAsync<T>(this T stream, bool disposeStream = true, CancellationToken token = default)
            where T : Stream
        {
            using StreamReader reader = new StreamReader(stream, null, true, -1, disposeStream);
            return await reader.ReadToEndAsync(token);
        }
        
        [GeneratedRegex("""^(?<path>[a-zA-Z]:\\(?:[^\\/:*?"<>|\r\n]+\\)*[^\\/:*?"<>|\r\n]+)(:[^\\/:*?"<>|\r\n]+)$""", RegexOptions.Compiled, 10000)]
        private static partial Regex AlternateDataStreamRegex();
        
        private static string StripAlternateDataStream(string path)
        {
            var match = AlternateDataStreamRegex().Match(path);
            return match.Success ? match.Groups["path"].Value : path;
        }

        
        /// <summary>
        /// Strips the alternate data stream from a file path.
        /// <para> e.g. "C:\path\to\file.txt:stream" becomes "C:\path\to\file.txt". </para>
        /// </summary>
        /// <param name="fileInfo">FileInfo to be stripped</param>
        /// <returns>FileInfo instance with ADS stripped if detected, returns original if the input doesn't use ADS or there's an error with the process.</returns>
        public static FileInfo StripAlternateDataStream(this FileInfo fileInfo)
        {
            try
            {
                ArgumentNullException.ThrowIfNull(fileInfo);

                var strippedPath = StripAlternateDataStream(fileInfo.FullName);
                return strippedPath.Equals(fileInfo.FullName, StringComparison.OrdinalIgnoreCase) ? fileInfo : // No alternate data stream, return original
                    new FileInfo(strippedPath); // Return new FileInfo without alternate data stream
            }
            catch (Exception ex)
            {
                Logger.LogWriteLine($"[StreamExtension] Failed when trying to strip Alternate Data Stream in path.\r\n{ex}",
                                    LogType.Error, true);
                SentryHelper.ExceptionHandler(ex);
                return fileInfo; // Return original FileInfo if any error occurs
            }
        }

        
        /// <summary>
        /// Resolves the symlink of a FileInfo instance.
        /// <para>Detects if the file has ReparsePoint attribute and resolve the target file path.</para>
        /// </summary>
        /// <param name="fileInfo">FileInfo to be resolved.</param>
        /// <returns>Instance of FileInfo to the resolved symlink file.</returns>
        /// <exception cref="FileNotFoundException">Target file of the symlink does not exist.</exception>
        public static FileInfo ResolveSymlink(this FileInfo fileInfo)
        {
            if (!fileInfo.Attributes.HasFlag(FileAttributes.ReparsePoint))
                return fileInfo;

            try
            {
                var target = new FileInfo(fileInfo.LinkTarget!);
                if (!target.Exists)
                {
                    throw new
                        FileNotFoundException($"[StreamExtension] Target symlink {target.FullName} for {fileInfo.FullName} does not exist.");
                }

                Logger.LogWriteLine($"[StreamExtension] Resolved symlink: {fileInfo.FullName} -> {target.FullName}",
                                    LogType.Default, true);
                return target;
            }
            catch (Exception ex)
            {
                Logger.LogWriteLine($"[StreamExtension] Failed to resolve symlink for {fileInfo.FullName}\r\n{ex}",
                                    LogType.Error, true);
                SentryHelper.ExceptionHandler(ex);
                throw;
            }
        }

        /// <summary>
        /// Deletes the directory if it is empty.
        /// </summary>
        /// <param name="dir">The directory to remove.</param>
        /// <param name="recursive">Whether to remove all possibly empty directories recursively.</param>
        public static void DeleteEmptyDirectory(this string dir, bool recursive = false)
            => new DirectoryInfo(dir).DeleteEmptyDirectory(recursive);

        /// <summary>
        /// Deletes the directory if it is empty.
        /// </summary>
        /// <param name="dir">The directory to remove.</param>
        /// <param name="recursive">Whether to remove all possibly empty directories recursively.</param>
        public static void DeleteEmptyDirectory(this DirectoryInfo dir, bool recursive = false)
        {
            if (recursive)
            {
                foreach (DirectoryInfo childDir in dir.EnumerateDirectories("*", SearchOption.TopDirectoryOnly))
                {
                    DeleteEmptyDirectory(childDir);
                }
            }

            FindFiles.TryIsDirectoryEmpty(dir.FullName, out bool isEmpty);
            if (isEmpty)
            {
                dir.Delete(true);
            }
        }

        public static FileStream Open(this FileInfo fileInfo,
                                      FileMode      fileMode,
                                      FileAccess    fileAccess,
                                      FileShare     fileShare,
                                      int           bufferSize)
        {
            return fileInfo.Open(new FileStreamOptions
            {
                Mode       = fileMode,
                Access     = fileAccess,
                Share      = fileShare,
                BufferSize = bufferSize
            });
        }

        public static FileStream Open(this FileInfo fileInfo,
                                      FileMode      fileMode,
                                      FileAccess    fileAccess,
                                      FileShare     fileShare,
                                      FileOptions   fileOptions,
                                      int           bufferSize)
        {
            return fileInfo.Open(new FileStreamOptions
            {
                Mode       = fileMode,
                Access     = fileAccess,
                Share      = fileShare,
                BufferSize = bufferSize
            });
        }
    }
}
