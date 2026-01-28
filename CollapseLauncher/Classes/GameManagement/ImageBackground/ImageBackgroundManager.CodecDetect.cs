using CollapseLauncher.CustomControls;
using CollapseLauncher.Dialogs;
using CollapseLauncher.Extension;
using CollapseLauncher.Helper;
using CollapseLauncher.Helper.StreamUtility;
using CollapseLauncher.XAMLs.Theme.CustomControls.LayeredBackgroundImage;
using Hi3Helper;
using Hi3Helper.Win32.WinRT.WindowsCodec;
using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
// ReSharper disable IdentifierTypo
// ReSharper disable StringLiteralTypo
// ReSharper disable CheckNamespace

#nullable enable
namespace CollapseLauncher.GameManagement.ImageBackground;

#region File Exclusive Fields
file static class Fields
{
    public const string DllNameAvcodec    = "avcodec-61.dll";
    public const string DllNameAvdevice   = "avdevice-61.dll";
    public const string DllNameAvfilter   = "avfilter-10.dll";
    public const string DllNameAvformat   = "avformat-61.dll";
    public const string DllNameAvutil     = "avutil-59.dll";
    public const string DllNameSwresample = "swresample-5.dll";
    public const string DllNameSwscale    = "swscale-8.dll";
}
#endregion

public partial class ImageBackgroundManager
{
    #region Codec Checks

    private static async Task<bool> CheckCodecOrSpawnDialog(Uri? fileUri)
    {
        // -- Cancel if null
        if (fileUri == null)
        {
            return false;
        }

        string filePath = fileUri.IsFile ? fileUri.LocalPath : fileUri.ToString();

        // -- Check for supported extension first
        if (!IsMediaFileExtensionSupported(filePath))
        {
            await SimpleDialogs.Dialog_SpawnMediaExtensionNotSupportedDialog(filePath);
            return false;
        }

        // -- Check for supported image codec
        if (IsImageMediaFileExtensionSupported(filePath))
        {
            if (WindowsCodecHelper.IsFileSupportedImage(filePath))
            {
                return true;
            }

            await SimpleDialogs.Dialog_SpawnImageNotSupportedDialog(filePath);
            return false;
        }

        // -- Check for supported video codec
        if (IsVideoMediaFileExtensionSupported(filePath))
        {
            if (WindowsCodecHelper.IsFileSupportedVideo(filePath,
                                                        out bool canPlayVideo,
                                                        out bool canPlayAudio,
                                                        out Guid videoCodecGuid,
                                                        out Guid audioCodecGuid) ||
                IsUseFfmpeg)
            {
                return true;
            }

            return await SimpleDialogs
               .Dialog_SpawnVideoNotSupportedDialog(filePath,
                                                    canPlayVideo,
                                                    canPlayAudio,
                                                    videoCodecGuid,
                                                    audioCodecGuid);
        }

        return true;
    }

    #endregion

    internal static bool IsFfmpegAvailable(string checkOnDirectory,
                                           [NotNullWhen(false)]
                                           out Exception? exception)
    {
        checkOnDirectory = FileUtility.GetFullyQualifiedPath(checkOnDirectory);

        string dllPathAvcodec    = Path.Combine(checkOnDirectory, Fields.DllNameAvcodec);
        string dllPathAvdevice   = Path.Combine(checkOnDirectory, Fields.DllNameAvdevice);
        string dllPathAvfilter   = Path.Combine(checkOnDirectory, Fields.DllNameAvfilter);
        string dllPathAvformat   = Path.Combine(checkOnDirectory, Fields.DllNameAvformat);
        string dllPathAvutil     = Path.Combine(checkOnDirectory, Fields.DllNameAvutil);
        string dllPathSwresample = Path.Combine(checkOnDirectory, Fields.DllNameSwresample);
        string dllPathSwscale    = Path.Combine(checkOnDirectory, Fields.DllNameSwscale);

        return FileUtility.IsFileExistOrSymbolicLinkResolved(dllPathAvcodec,    out _, out exception) &&
               FileUtility.IsFileExistOrSymbolicLinkResolved(dllPathAvdevice,   out _, out exception) &&
               FileUtility.IsFileExistOrSymbolicLinkResolved(dllPathAvfilter,   out _, out exception) &&
               FileUtility.IsFileExistOrSymbolicLinkResolved(dllPathAvformat,   out _, out exception) &&
               FileUtility.IsFileExistOrSymbolicLinkResolved(dllPathAvutil,     out _, out exception) &&
               FileUtility.IsFileExistOrSymbolicLinkResolved(dllPathSwresample, out _, out exception) &&
               FileUtility.IsFileExistOrSymbolicLinkResolved(dllPathSwscale,    out _, out exception);
    }

    internal static string[] GetFfmpegRequiredDllFilenames() =>
    [
        Fields.DllNameAvcodec,
        Fields.DllNameAvdevice,
        Fields.DllNameAvfilter,
        Fields.DllNameAvformat,
        Fields.DllNameAvutil,
        Fields.DllNameSwresample,
        Fields.DllNameSwscale
    ];

    internal static string? FindFfmpegInstallFolder(string checkOnDirectory)
    {
        try
        {
            if (IsFfmpegAvailable(checkOnDirectory, out _))
            {
                return checkOnDirectory;
            }

            foreach (string dirPath in FileUtility.EnumerateDirectoryRecursive(checkOnDirectory))
            {
                if (IsFfmpegAvailable(dirPath, out _))
                {
                    return dirPath;
                }
            }
        }
        catch
        {
            // ignored
        }

        return null;
    }

    public static bool TryLinkFfmpegLibrary(
        string sourceDir,
        string targetDir,
        [NotNullWhen(false)]
        out Exception? exception)
    {
        sourceDir = FileUtility.GetFullyQualifiedPath(sourceDir);
        targetDir = FileUtility.GetFullyQualifiedPath(targetDir);

        if (!Directory.Exists(sourceDir) ||
            !Directory.Exists(targetDir))
        {
            exception = new DirectoryNotFoundException("Source or Target directory for symbolic link creation not found.");
            return false;
        }

        string dllPathAvcodec    = Path.Combine(sourceDir, Fields.DllNameAvcodec);
        string dllPathAvdevice   = Path.Combine(sourceDir, Fields.DllNameAvdevice);
        string dllPathAvfilter   = Path.Combine(sourceDir, Fields.DllNameAvfilter);
        string dllPathAvformat   = Path.Combine(sourceDir, Fields.DllNameAvformat);
        string dllPathAvutil     = Path.Combine(sourceDir, Fields.DllNameAvutil);
        string dllPathSwresample = Path.Combine(sourceDir, Fields.DllNameSwresample);
        string dllPathSwscale    = Path.Combine(sourceDir, Fields.DllNameSwscale);

        return CreateSymbolLink(dllPathAvcodec,    targetDir, out exception) &&
               CreateSymbolLink(dllPathAvdevice,   targetDir, out exception) &&
               CreateSymbolLink(dllPathAvfilter,   targetDir, out exception) &&
               CreateSymbolLink(dllPathAvformat,   targetDir, out exception) &&
               CreateSymbolLink(dllPathAvutil,     targetDir, out exception) &&
               CreateSymbolLink(dllPathSwresample, targetDir, out exception) &&
               CreateSymbolLink(dllPathSwscale,    targetDir, out exception);

        static bool CreateSymbolLink(string         filePath,
                                     string         targetDirectory,
                                     [NotNullWhen(false)]
                                     out Exception? exception)
        {
            Unsafe.SkipInit(out exception);

            FileInfo fileInfo = new(filePath);
            if (!fileInfo.Exists)
            {
                exception = new FileNotFoundException("Source file for symbolic link creation not found.", filePath);
                return false;
            }

            try
            {
                string   targetPath    = Path.Combine(targetDirectory, fileInfo.Name);
                FileInfo targetSymLink = new(targetPath);
                if (targetSymLink.Exists)
                {
                    targetSymLink.TryDeleteFile();
                }

                targetSymLink.CreateAsSymbolicLink(filePath);
                return true;
            }
            catch (Exception e)
            {
                exception = e;
                return false;
            }
        }
    }

    private static bool IsMediaFileExtensionSupported(ReadOnlySpan<char> filePath)
    {
        return IsImageMediaFileExtensionSupported(filePath) ||
               IsVideoMediaFileExtensionSupported(filePath);
    }

    private static bool IsImageMediaFileExtensionSupported(ReadOnlySpan<char> filePath)
    {
        ReadOnlySpan<char> extension = Path.GetExtension(filePath);
        return LayeredBackgroundImage.SupportedImageBitmapExtensionsLookup.Contains(extension) ||
               LayeredBackgroundImage.SupportedImageBitmapExternalCodecExtensionsLookup.Contains(extension) ||
               LayeredBackgroundImage.SupportedImageVectorExtensionsLookup.Contains(extension);
    }

    private static bool IsVideoMediaFileExtensionSupported(ReadOnlySpan<char> filePath)
    {
        ReadOnlySpan<char> extension = Path.GetExtension(filePath);
        return LayeredBackgroundImage.SupportedVideoExtensionsLookup.Contains(extension);
    }
}
