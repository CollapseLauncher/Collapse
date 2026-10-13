using CollapseLauncher.Helper;
using CollapseLauncher.Helper.InternalPInvoke;
using CollapseLauncher.Helper.InternalPInvoke.FFmpeg;
using CollapseLauncher.Helper.StreamUtility;
using FFmpegInteropX;
using Hi3Helper;
using Hi3Helper.Shared.Region;
using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;
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
    private const string GlobalFFmpegDecodingModeConfigKey = "GlobalFFmpegDecodingMode";

    public VideoDecoderMode[] AvailableFFmpegDecodingModes => field ??= Enum.GetValues<VideoDecoderMode>();

    public int GlobalFFmpegVersionToUse
    {
        get => FFmpegPInvoke.FFmpegVersionToUse;
        set
        {
            FFmpegPInvoke.FFmpegVersionToUse = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(GlobalFFmpegLibraryNames)); // Notify FFmpeg library names update too.
        }
    }

    public FFmpegLibraryNames GlobalFFmpegLibraryNames => FFmpegPInvoke.FFmpegLibraryNamings;

    public string? GlobalCustomFFmpegPath
    {
        get => FFmpegPInvoke.CustomFFmpegPath;
        set
        {
            FFmpegPInvoke.CustomFFmpegPath = value;
            OnPropertyChanged();
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

    public bool GlobalIsFFmpegAvailable => FFmpegPInvoke.IsFFmpegAvailable(Directory.GetCurrentDirectory(), GlobalFFmpegLibraryNames, out _);

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
            if (Enum.TryParse(value, out VideoDecoderMode result))
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

            FFmpegLibraryNames names = GlobalFFmpegLibraryNames;

            string  curDir       = LauncherConfig.AppExecutableDir;
            string  stockDir     = Path.Combine(curDir, "Lib");
            string? stockFindDir = FFmpegPInvoke.FindFFmpegInstallFolder(stockDir, names);

            string? customFFmpegDirPath = GlobalCustomFFmpegPath;

            // -- 1. Check from custom path first. If it exists, then pass.
            if (!string.IsNullOrEmpty(customFFmpegDirPath) &&
                FFmpegPInvoke.IsFFmpegAvailable(customFFmpegDirPath, names, out exception) &&
                TryLinkFFmpegLibrary(customFFmpegDirPath, curDir, names, out exception))
            {
                return result = true;
            }

            // -- 2. Link stock library to the root directory
            if (!string.IsNullOrEmpty(stockFindDir) &&
                FFmpegPInvoke.IsFFmpegAvailable(stockFindDir, names, out exception) &&
                TryLinkFFmpegLibrary(stockFindDir, curDir, names, out exception))
            {
                return result = true;
            }

            // -- 3. Find one from environment variables. If it exists, then pass.
            //       Otherwise, return false.
            if (!FFmpegPInvoke.TryFindFFmpegInstallFromEnvVar(names, out string? envVarPath, out exception) ||
                !TryLinkFFmpegLibrary(envVarPath, curDir, names, out exception))
            {
                return false;
            }

            GlobalCustomFFmpegPath = envVarPath; // Set as custom path
            return true;
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

    internal static string[] GetFFmpegRequiredDllFilenames()
    {
        FFmpegLibraryNames names = Shared.GlobalFFmpegLibraryNames;
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

    public static bool TryLinkFFmpegLibrary(
        string?            sourceDir,
        string?            targetDir,
        FFmpegLibraryNames libraries,
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
