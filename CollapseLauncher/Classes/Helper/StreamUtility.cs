using Hi3Helper;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

#nullable enable
namespace CollapseLauncher.Helper
{
    internal static class StreamUtility
    {
        internal const int DefaultBufferSize = 64 << 10;

        internal static readonly FileStreamOptions FileStreamOpenReadOpt = new FileStreamOptions
        {
            Mode = FileMode.Open,
            Access = FileAccess.Read,
            Share = FileShare.Read
        };

        internal static readonly FileStreamOptions FileStreamCreateWriteOpt = new FileStreamOptions
        {
            Mode = FileMode.Create,
            Access = FileAccess.Write,
            Share = FileShare.Write
        };

        internal static readonly FileStreamOptions FileStreamCreateReadWriteOpt = new FileStreamOptions
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
        {
            if (!fileInfo.Exists)
                return fileInfo;

            fileInfo.IsReadOnly = false;
            fileInfo.Refresh();

            return fileInfo;
        }

        internal static IEnumerable<FileInfo> EnumerateNoReadOnly(this IEnumerable<FileInfo> enumeratedFileInfo)
            => enumeratedFileInfo.Select(x => x.EnsureNoReadOnly());

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
            const int maxTry     = 10;
            int       currentTry = 1;
            while (true)
            {
                try
                {
                    return info.Open(new FileStreamOptions
                    {
                        Mode       = fileMode,
                        Access     = fileAccess,
                        Share      = fileShare,
                        Options    = fileOption,
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

            if (directoryInfo is { Exists: false })
                directoryInfo.Create();

            return filePath;
        }
    }
}
