using Hi3Helper;
using Hi3Helper.Shared.Region;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
// ReSharper disable StringLiteralTypo
// ReSharper disable IdentifierTypo
// ReSharper disable InconsistentNaming
#pragma warning disable IDE0130

namespace CollapseLauncher.Helper.InternalPInvoke;

internal static class CollapsePInvoke
{
    internal const string LibraryExtension = ".dll";

    public static Dictionary<string, DllImportResolver> CustomResolvers = new(StringComparer.OrdinalIgnoreCase);

    internal static nint DllImportResolver(string libraryName, Assembly assembly, DllImportSearchPath? searchPath)
    {
        // Try to resolve the library from the Custom Resolvers first.
        if (CustomResolvers.TryGetValue(libraryName, out DllImportResolver customResolver) &&
            customResolver(libraryName, assembly, searchPath) is var customResolverResult &&
            customResolverResult != nint.Zero)
        {
            return customResolverResult;
        }

        bool retryFirst = false;
        string pathToLoad = libraryName;
    LoadFirst:
        if (LoadInternal(pathToLoad, assembly, searchPath) is var dllHandle &&
            dllHandle != nint.Zero)
        {
            return dllHandle;
        }

        // Try to append extension in case loading was failed due to it. Try to load it once again.
        if (!pathToLoad.EndsWith(LibraryExtension, StringComparison.OrdinalIgnoreCase) && !retryFirst)
        {
            retryFirst = true;
            pathToLoad = Path.Combine(Path.GetDirectoryName(libraryName) ?? "",
                                      Path.GetFileNameWithoutExtension(libraryName) + LibraryExtension);
            goto LoadFirst;
        }

        if (TryLoadFromDir(assembly, "Dir", libraryName, out dllHandle) ||
            TryLoadFromDir(assembly, LauncherConfig.AppExecutableDir, libraryName, out dllHandle))
        {
            return dllHandle;
        }

        throw new FileLoadException($"Cannot resolve library handle (.dll has failed to load) from this path: {libraryName} with Search Path: {searchPath}\r\nMake sure that the library/.dll is a valid Win32 library, has correct architecture and not corrupted!");
    }

    [SkipLocalsInit]
    private static bool TryLoadFromDir(Assembly assembly, string dirPath, string libraryName, out nint handle)
    {
        Unsafe.SkipInit(out handle);

        bool retry = false;
        string dllFileName = Path.GetFileName(libraryName);
        string dllPathOnDir = Path.Combine(dirPath, dllFileName);
    Load:
        if (LoadInternal(dllPathOnDir, assembly) is var dllHandle &&
            dllHandle != nint.Zero)
        {
            handle = dllHandle;
            return true;
        }

        // If the second attempt by loading from specific dir also failed,
        // Try to append extension in case loading was failed due to it. Then try to load it once again :fingercrossed:
        if (dllPathOnDir.EndsWith(LibraryExtension, StringComparison.OrdinalIgnoreCase) || retry)
        {
            return false;
        }

        retry = true;
        dllPathOnDir = Path.Combine(Path.GetDirectoryName(dllPathOnDir) ?? "",
                                    Path.GetFileNameWithoutExtension(dllPathOnDir) + LibraryExtension);
        goto Load;

    }

    private static nint LoadInternal(string path, Assembly assembly, DllImportSearchPath? searchPath = null)
    {
        bool isLoadSuccessful = NativeLibrary.TryLoad(path, assembly, searchPath, out nint pResult);
        if (isLoadSuccessful)
        {
            Logger.LogWriteLine($"Successfully loaded dll: {path} using SearchPath: {searchPath} at handlePtr: {pResult}",
                                LogType.Debug,
                                true);
        }

        return pResult;
    }
}
