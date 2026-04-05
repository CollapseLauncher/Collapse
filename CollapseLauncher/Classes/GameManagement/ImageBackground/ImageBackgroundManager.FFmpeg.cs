using CollapseLauncher.Helper;
using CollapseLauncher.Helper.StreamUtility;
using Hi3Helper;
using Hi3Helper.Shared.Region;
using System;
using System.Collections;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Runtime.CompilerServices;
// ReSharper disable StringLiteralTypo
// ReSharper disable IdentifierTypo
// ReSharper disable CommentTypo
#pragma warning disable IDE0130
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
    #region Shared/Static Properties and Fields

    private const string GlobalIsUseFFmpegConfigKey      = "GlobalIsUseFFmpeg";
    private const string GlobalFFmpegCustomPathConfigKey = "GlobalFFmpegCustomPath";

    public bool GlobalIsUseFFmpeg
    {
        get => LauncherConfig.GetAppConfigValue(GlobalIsUseFFmpegConfigKey);
        set
        {
            LauncherConfig.SetAndSaveConfigValue(GlobalIsUseFFmpegConfigKey, value);
            OnPropertyChanged();
        }
    }

    public string? GlobalCustomFFmpegPath
    {
        get => LauncherConfig.GetAppConfigValue(GlobalFFmpegCustomPathConfigKey);
        set
        {
            LauncherConfig.SetAndSaveConfigValue(GlobalFFmpegCustomPathConfigKey, value);
            OnPropertyChanged();
        }
    }

    public bool GlobalIsFFmpegAvailable => IsFFmpegAvailable(Directory.GetCurrentDirectory(), out _);

    public bool GlobalIsFFmpegCurrentlyUsed
    {
        get;
        set
        {
            field = value;
            OnPropertyChanged();
        }
    }

    #endregion

    public void RefreshFFmpegBinding()
    {
        TryRelinkFFmpegPath();
        OnPropertyChanged(nameof(GlobalIsFFmpegAvailable));
    }

    public bool TryRelinkFFmpegPath()
    {
        Unsafe.SkipInit(out Exception? exception);

        bool result = false;
        try
        {
            // If FFmpeg usage is disabled, skip the check and linking process.
            if (!GlobalIsUseFFmpeg)
            {
                return false;
            }

            string  curDir              = Directory.GetCurrentDirectory();
            bool    isFFmpegAvailable   = IsFFmpegAvailable(curDir, out exception);
            string? customFFmpegDirPath = GlobalCustomFFmpegPath;

            if (isFFmpegAvailable)
            {
                return false;
            }

            // -- If custom FFmpeg path is set but FFmpeg is not available,
            //    Try to resolve the symbolic link path again.
            // -- Check for custom FFmpeg path availability first. If not available, skip.
            result = (IsFFmpegAvailable(customFFmpegDirPath, out exception) &&
                      // -- Re-link FFmpeg symbolic link
                      TryLinkFFmpegLibrary(customFFmpegDirPath, curDir, out exception)) ||
                     // -- If custom FFmpeg path is not avail, then try to find one from EnvVar (this might be a bit expensive).
                     //    If found, the GlobalCustomFFmpegPath will be updated to the found path.
                     (TryFindFFmpegInstallFromEnvVar(out string? envVarPath, out exception) && TryLinkFFmpegLibrary(envVarPath, curDir, out exception));
            
            return result;
        }
        finally
        {
            if (!result && exception != null)
            {
                Logger.LogWriteLine($"[ImageBackgroundManager::TryRelinkFFmpegPath()] {exception}",
                                    LogType.Error,
                                    true);
            }
        }
    }

    internal bool TryFindFFmpegInstallFromEnvVar([NotNullWhen(true)] out string? path, out Exception? exception)
    {
        return FindIn(EnvironmentVariableTarget.User,    out path, out exception) ||
               FindIn(EnvironmentVariableTarget.Machine, out path, out exception);

        bool FindIn(EnvironmentVariableTarget target, [NotNullWhen(true)] out string? innerPath, out Exception? exception)
        {
            const string separators = ";,";
            Unsafe.SkipInit(out innerPath);
            Unsafe.SkipInit(out exception);

            foreach (object? varValue in Environment.GetEnvironmentVariables(target))
            {
                if (varValue is not DictionaryEntry { Value: string varValueStr })
                {
                    continue;
                }

                ReadOnlySpan<char> envVar = varValueStr;
                foreach (Range envVarRange in envVar.SplitAny(separators))
                {
                    ReadOnlySpan<char> envVarPath = envVar[envVarRange].Trim(" '\"");
                    if (envVarPath.IsEmpty)
                    {
                        continue;
                    }

                    string thisPath = envVarPath.ToString();

                    if (!Path.IsPathFullyQualified(thisPath) ||
                        !IsFFmpegAvailable(thisPath, out exception)) continue;

                    innerPath              = thisPath;
                    GlobalCustomFFmpegPath = thisPath; // Set as custom path
                    return true;
                }
            }

            return false;
        }
    }

    internal static bool IsFFmpegAvailable(string? checkOnDirectory,
                                           [NotNullWhen(false)]
                                           out Exception? exception)
    {
        if (string.IsNullOrEmpty(checkOnDirectory))
        {
            exception = new NullReferenceException($"Argument: {nameof(checkOnDirectory)} is null!");
            return false;
        }

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

    internal static string[] GetFFmpegRequiredDllFilenames() =>
    [
        Fields.DllNameAvcodec,
        Fields.DllNameAvdevice,
        Fields.DllNameAvfilter,
        Fields.DllNameAvformat,
        Fields.DllNameAvutil,
        Fields.DllNameSwresample,
        Fields.DllNameSwscale
    ];

    internal static string? FindFFmpegInstallFolder(string checkOnDirectory)
    {
        try
        {
            if (IsFFmpegAvailable(checkOnDirectory, out _))
            {
                return checkOnDirectory;
            }

            foreach (string dirPath in FileUtility.EnumerateDirectoryRecursive(checkOnDirectory))
            {
                if (IsFFmpegAvailable(dirPath, out _))
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

    public static bool TryLinkFFmpegLibrary(
        string? sourceDir,
        string? targetDir,
        [NotNullWhen(false)]
        out Exception? exception)
    {
        if (string.IsNullOrEmpty(sourceDir) || string.IsNullOrEmpty(targetDir))
        {
            exception = new NullReferenceException($"Argument: {nameof(sourceDir)} or {nameof(targetDir)} are null!");
            return false;
        }

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

        static bool CreateSymbolLink(string filePath,
                                     string targetDirectory,
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
                string targetPath = Path.Combine(targetDirectory, fileInfo.Name);
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
}
