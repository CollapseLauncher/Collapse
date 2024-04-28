using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Text;
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
            Share = FileShare.Read,
            BufferSize = DefaultBufferSize,
            Options = FileOptions.Asynchronous | FileOptions.RandomAccess
        };

        internal static readonly FileStreamOptions FileStreamCreateWriteOpt = new FileStreamOptions
        {
            Mode = FileMode.Create,
            Access = FileAccess.Write,
            Share = FileShare.Write,
            BufferSize = DefaultBufferSize,
            Options = FileOptions.Asynchronous | FileOptions.RandomAccess
        };

        internal static readonly FileStreamOptions FileStreamCreateReadWriteOpt = new FileStreamOptions
        {
            Mode = FileMode.Create,
            Access = FileAccess.ReadWrite,
            Share = FileShare.ReadWrite,
            BufferSize = DefaultBufferSize,
            Options = FileOptions.Asynchronous | FileOptions.RandomAccess
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
    }
}
