using CollapseLauncher.Helper;
using CollapseLauncher.Helper.FFmpegPInvoke;
using CollapseLauncher.Helper.StreamUtility;
using FFmpegInteropX;
using Hi3Helper;
using Hi3Helper.Shared.Region;
using System;
using System.Collections;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
// ReSharper disable StringLiteralTypo
// ReSharper disable IdentifierTypo
// ReSharper disable CommentTypo
#pragma warning disable IDE0130
#nullable enable

namespace CollapseLauncher.GameManagement.ImageBackground;

public partial class ImageBackgroundManager
{
    #region Shared/Static Properties and Fields

    private const string GlobalIsUseFFmpegConfigKey        = "GlobalIsUseFFmpeg";
    private const string GlobalFFmpegVersionToUseConfigKey = "GlobalFFmpegVersionToUse";
    private const string GlobalFFmpegCustomPathConfigKey   = "GlobalFFmpegCustomPath";
    private const string GlobalFFmpegDecodingModeConfigKey = "GlobalFFmpegDecodingMode";

    public VideoDecoderMode[] AvailableFFmpegDecodingModes => field ??= Enum.GetValues<VideoDecoderMode>();

    public int GlobalFFmpegVersionToUse
    {
        get
        {
            int version = LauncherConfig.GetAppConfigValue(GlobalFFmpegVersionToUseConfigKey);
            return FFmpegPInvoke.FFmpegVersionLibNames.ContainsKey(version) ?
                version :
                FFmpegPInvoke.FFmpegVersionLibNames.Keys.FirstOrDefault();
        }
        set
        {
            if (!FFmpegPInvoke.FFmpegVersionLibNames.ContainsKey(value))
            {
                return;
            }

            LauncherConfig.SetAndSaveConfigValue(GlobalFFmpegVersionToUseConfigKey, value);
            OnPropertyChanged();
            OnPropertyChanged(nameof(GlobalFFmpegLibraryNames)); // Notify FFmpeg library names update too.
        }
    }

    public FFmpegPInvoke.FFmpegLibraryNames GlobalFFmpegLibraryNames
    {
        get
        {
            if (FFmpegPInvoke.FFmpegVersionLibNames.TryGetValue(GlobalFFmpegVersionToUse, out var names))
            {
                return names;
            }

            return FFmpegPInvoke.FFmpegVersionLibNames.Values.FirstOrDefault();
        }
    }

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

    public bool GlobalIsFFmpegAvailable => IsFFmpegAvailable(Directory.GetCurrentDirectory(), GlobalFFmpegLibraryNames, out _);

    public bool GlobalIsFFmpegCurrentlyUsed
    {
        get;
        set
        {
            field = value;
            OnPropertyChanged();
        }
    }

    public VideoDecoderMode GlobalFFmpegDecodingMode
    {
        get
        {
            string? value = LauncherConfig.GetAppConfigValue(GlobalFFmpegDecodingModeConfigKey);
            if (Enum.TryParse<VideoDecoderMode>(value, out var result))
            {
                return result;
            }

            return default;
        }
        set
        {
            if (!Enum.IsDefined(value))
            {
                value = default;
            }

            string valueStr = value.ToString();
            LauncherConfig.SetAndSaveConfigValue(GlobalFFmpegDecodingModeConfigKey, valueStr);
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

            var names = GlobalFFmpegLibraryNames;

            string  curDir              = Directory.GetCurrentDirectory();
            bool    isFFmpegAvailable   = IsFFmpegAvailable(curDir, names, out exception);
            string? customFFmpegDirPath = GlobalCustomFFmpegPath;

            if (isFFmpegAvailable)
            {
                return false;
            }

            // -- 1. Check from custom path first. If it exists, then pass.
            if (!string.IsNullOrEmpty(customFFmpegDirPath) &&
                IsFFmpegAvailable(customFFmpegDirPath, names, out exception) &&
                TryLinkFFmpegLibrary(customFFmpegDirPath, curDir, names, out exception))
            {
                return result = true;
            }

            // -- 2. Find one from environment variables. If it exists, then pass.
            //       Otherwise, return false.
            return result = TryFindFFmpegInstallFromEnvVar(names, out string ? envVarPath, out exception) &&
                            TryLinkFFmpegLibrary(envVarPath, curDir, names, out exception);
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

    internal bool TryFindFFmpegInstallFromEnvVar(FFmpegPInvoke.FFmpegLibraryNames libraries, [NotNullWhen(true)] out string? path, out Exception? exception)
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
                        !IsFFmpegAvailable(thisPath, libraries, out exception)) continue;

                    innerPath              = thisPath;
                    GlobalCustomFFmpegPath = thisPath; // Set as custom path
                    return true;
                }
            }

            return false;
        }
    }

    internal static bool IsFFmpegAvailable(string? checkOnDirectory,
                                           FFmpegPInvoke.FFmpegLibraryNames libraries,
                                           [NotNullWhen(false)]
                                           out Exception? exception)
    {
        if (string.IsNullOrEmpty(checkOnDirectory))
        {
            exception = new NullReferenceException($"Argument: {nameof(checkOnDirectory)} is null!");
            return false;
        }

        checkOnDirectory = FileUtility.GetFullyQualifiedPath(checkOnDirectory);

        string dllPathAvcodec    = Path.Combine(checkOnDirectory, libraries.Codec);
        string dllPathAvdevice   = Path.Combine(checkOnDirectory, libraries.Device);
        string dllPathAvfilter   = Path.Combine(checkOnDirectory, libraries.Filter);
        string dllPathAvformat   = Path.Combine(checkOnDirectory, libraries.Format);
        string dllPathAvutil     = Path.Combine(checkOnDirectory, libraries.Util);
        string dllPathSwresample = Path.Combine(checkOnDirectory, libraries.Resample);
        string dllPathSwscale    = Path.Combine(checkOnDirectory, libraries.Scale);

        return FileUtility.IsFileExistOrSymbolicLinkResolved(dllPathAvcodec,    out _, out exception) &&
               FileUtility.IsFileExistOrSymbolicLinkResolved(dllPathAvdevice,   out _, out exception) &&
               FileUtility.IsFileExistOrSymbolicLinkResolved(dllPathAvfilter,   out _, out exception) &&
               FileUtility.IsFileExistOrSymbolicLinkResolved(dllPathAvformat,   out _, out exception) &&
               FileUtility.IsFileExistOrSymbolicLinkResolved(dllPathAvutil,     out _, out exception) &&
               FileUtility.IsFileExistOrSymbolicLinkResolved(dllPathSwresample, out _, out exception) &&
               FileUtility.IsFileExistOrSymbolicLinkResolved(dllPathSwscale,    out _, out exception);
    }

    internal static string[] GetFFmpegRequiredDllFilenames()
    {
        var names = Shared.GlobalFFmpegLibraryNames;
        return [
            names.Codec,
            names.Device,
            names.Filter,
            names.Format,
            names.Util,
            names.Resample,
            names.Scale
        ];
    }

    internal static string? FindFFmpegInstallFolder(string checkOnDirectory, FFmpegPInvoke.FFmpegLibraryNames libraries)
    {
        try
        {
            if (IsFFmpegAvailable(checkOnDirectory, libraries, out _))
            {
                return checkOnDirectory;
            }

            foreach (string dirPath in FileUtility.EnumerateDirectoryRecursive(checkOnDirectory))
            {
                if (IsFFmpegAvailable(dirPath, libraries, out _))
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
        FFmpegPInvoke.FFmpegLibraryNames libraries,
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

        string dllPathAvcodec    = Path.Combine(sourceDir, libraries.Codec);
        string dllPathAvdevice   = Path.Combine(sourceDir, libraries.Device);
        string dllPathAvfilter   = Path.Combine(sourceDir, libraries.Filter);
        string dllPathAvformat   = Path.Combine(sourceDir, libraries.Format);
        string dllPathAvutil     = Path.Combine(sourceDir, libraries.Util);
        string dllPathPostproc   = Path.Combine(sourceDir, libraries.PostProc);
        string dllPathSwresample = Path.Combine(sourceDir, libraries.Resample);
        string dllPathSwscale    = Path.Combine(sourceDir, libraries.Scale);

        bool result =
            CreateSymbolLink(dllPathAvcodec,    targetDir, out exception) &&
            CreateSymbolLink(dllPathAvdevice,   targetDir, out exception) &&
            CreateSymbolLink(dllPathAvfilter,   targetDir, out exception) &&
            CreateSymbolLink(dllPathAvformat,   targetDir, out exception) &&
            CreateSymbolLink(dllPathAvutil,     targetDir, out exception) &&
            CreateSymbolLink(dllPathSwresample, targetDir, out exception) &&
            CreateSymbolLink(dllPathSwscale,    targetDir, out exception);

        // Additionally, link postproc if it exists.
        // Since some non-free/GPL custom build (if used by the user) still requires postproc library to exist if enabled on build.
        // Without it, some build might fail to run.
        if (result && FileUtility.IsFileExistOrSymbolicLinkResolved(dllPathPostproc, out string? resolvedOptDllPostproc, out exception))
        {
            return result && CreateSymbolLink(resolvedOptDllPostproc, targetDir, out exception);
        }

        return result;

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
