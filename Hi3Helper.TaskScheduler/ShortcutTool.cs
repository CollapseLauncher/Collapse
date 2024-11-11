using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Velopack;
using Velopack.Locators;
using Velopack.NuGet;
using Velopack.Windows;

namespace Hi3Helper.TaskScheduler
{
    public class ShortcutTool
    {
        /// <summary> Log for diagnostic messages. </summary>
        protected ILogger Log { get; }

        /// <summary> Locator to use for finding important application paths. </summary>
        protected IVelopackLocator Locator { get; }

        /// <inheritdoc cref="ShortcutTool"/>
        public ShortcutTool(ILogger? logger = null, IVelopackLocator? locator = null)
        {
            Log = logger ?? NullLogger.Instance;
            Locator = locator ?? VelopackLocator.GetDefault(Log);
        }

        /// <summary>
        /// Create a shortcut to the currently running executable at the specified locations. 
        /// See <see cref="CreateShortcut"/> to create a shortcut to a different program
        /// </summary>
        public void CreateShortcutForThisExe(ShortcutLocation location = ShortcutLocation.Desktop | ShortcutLocation.StartMenuRoot)
        {
            CreateShortcut(
                Locator.ThisExeRelativePath,
                location,
                false,
                null,  // shortcut arguments 
                null); // shortcut icon
        }

        /// <summary>
        /// Removes a shortcut for the currently running executable at the specified locations
        /// </summary>
        public void RemoveShortcutForThisExe(ShortcutLocation location = ShortcutLocation.Desktop | ShortcutLocation.StartMenu | ShortcutLocation.StartMenuRoot)
        {
            DeleteShortcuts(
                Locator.ThisExeRelativePath,
                location);
        }

        /// <summary>
        /// Searches for existing shortcuts to an executable inside the current package.
        /// </summary>
        /// <param name="relativeExeName">The relative path or filename of the executable (from the current app dir).</param>
        /// <param name="locations">The locations to search.</param>
        public Dictionary<ShortcutLocation, ShellLink> FindShortcuts(string relativeExeName, ShortcutLocation locations)
        {
            var release = Locator.GetLatestLocalFullPackage();
            var pkgDir = Locator.PackagesDir;
            var currentDir = Locator.AppContentDir;
            var rootAppDirectory = Locator.RootAppDir;

            var ret = new Dictionary<ShortcutLocation, ShellLink>();
            var pkgPath = Path.Combine(pkgDir, release.FileName);
            var zf = new ZipPackage(pkgPath);
            var exePath = Path.Combine(currentDir, relativeExeName);
            if (!File.Exists(exePath))
                return ret;

            var fileVerInfo = FileVersionInfo.GetVersionInfo(exePath);

            foreach (var f in GetLocations(locations))
            {
                var file = LinkPathForVersionInfo(f, zf, fileVerInfo, rootAppDirectory);
                if (File.Exists(file))
                {
                    Log.LogInformation($"Opening existing shortcut for {relativeExeName} ({file})");
                    ret.Add(f, new ShellLink(file));
                }
            }

            return ret;
        }

        /// <summary>
        /// Creates new shortcuts to the specified executable at the specified locations.
        /// </summary>
        /// <param name="relativeExeName">The relative path or filename of the executable (from the current app dir).</param>
        /// <param name="locations">The locations to create shortcuts.</param>
        /// <param name="updateOnly">If true, shortcuts will be updated instead of created.</param>
        /// <param name="programArguments">The arguments the application should be launched with</param>
        /// <param name="icon">Path to a specific icon to use instead of the exe icon.</param>
        public void CreateShortcut(string? relativeExeName, ShortcutLocation locations, bool updateOnly, string? programArguments = null, string? icon = null)
        {
            VelopackAsset? release = Locator.GetLatestLocalFullPackage();
            string? pkgDir = Locator.PackagesDir;
            string? currentDir = Locator.AppContentDir;
            string? rootAppDirectory = Locator.RootAppDir;
            Log.LogInformation($"About to create shortcuts for {relativeExeName}, rootAppDir {rootAppDirectory}");

            string pkgPath = Path.Combine(pkgDir, release.FileName);
            ZipPackage zf = new ZipPackage(pkgPath);
            string exePath = Path.Combine(currentDir, relativeExeName);
            if (!File.Exists(exePath))
                throw new FileNotFoundException($"Could not find: {exePath}");

            var fileVerInfo = FileVersionInfo.GetVersionInfo(exePath);

            foreach (var f in GetLocations(locations))
            {
                string file = LinkPathForVersionInfo(f, zf, fileVerInfo, rootAppDirectory);
                bool fileExists = File.Exists(file);

                // NB: If we've already installed the app, but the shortcut
                // is no longer there, we have to assume that the user didn't
                // want it there and explicitly deleted it, so we shouldn't
                // annoy them by recreating it.
                if (!fileExists && updateOnly)
                {
                    Log.LogWarning($"Wanted to update shortcut {file} but it appears user deleted it");
                    continue;
                }

                Log.LogInformation($"Creating shortcut for {relativeExeName} => {file}");

                ShellLink sl;
                File.Delete(file);

                string target = Path.Combine(currentDir, relativeExeName);
                sl = new ShellLink
                {
                    Target = target,
                    IconPath = icon ?? target,
                    IconIndex = 0,
                    WorkingDirectory = Path.GetDirectoryName(exePath),
                    Description = zf.ProductDescription,
                };

                if (!string.IsNullOrWhiteSpace(programArguments))
                {
                    sl.Arguments += string.Format(" -a \"{0}\"", programArguments);
                }

                //var appUserModelId = Utility.GetAppUserModelId(zf.Id, exeName);
                //var toastActivatorCLSID = Utility.CreateGuidFromHash(appUserModelId).ToString();
                //sl.SetAppUserModelId(appUserModelId);
                //sl.SetToastActivatorCLSID(toastActivatorCLSID);

                Log.LogInformation($"About to save shortcut: {file} (target {sl.Target}, workingDir {sl.WorkingDirectory}, args {sl.Arguments})");
                sl.Save(file);
            }
        }

        /// <summary>
        /// Delete all the shortcuts for the specified executable in the specified locations.
        /// </summary>
        /// <param name="relativeExeName">The relative path or filename of the executable (from the current app dir).</param>
        /// <param name="locations">The locations to create shortcuts.</param>
        public void DeleteShortcuts(string? relativeExeName, ShortcutLocation locations)
        {
            var release = Locator.GetLatestLocalFullPackage();
            var pkgDir = Locator.PackagesDir;
            var currentDir = Locator.AppContentDir;
            var rootAppDirectory = Locator.RootAppDir;
            Log.LogInformation($"About to delete shortcuts for {relativeExeName}, rootAppDir {rootAppDirectory}");

            var pkgPath = Path.Combine(pkgDir, release.FileName);
            var zf = new ZipPackage(pkgPath);
            var exePath = Path.Combine(currentDir, relativeExeName);
            if (!File.Exists(exePath)) return;

            var fileVerInfo = FileVersionInfo.GetVersionInfo(exePath);

            foreach (var f in GetLocations(locations))
            {
                var file = LinkPathForVersionInfo(f, zf, fileVerInfo, rootAppDirectory);
                Log.LogInformation($"Removing shortcut for {relativeExeName} => {file}");
                try
                {
                    if (File.Exists(file)) File.Delete(file);
                }
                catch (Exception ex)
                {
                    Log.LogError(ex, "Couldn't delete shortcut: " + file);
                }
            }
        }

        /// <summary>
        /// Given an <see cref="ZipPackage"/> and <see cref="FileVersionInfo"/> return the target shortcut path.
        /// </summary>
        protected virtual string LinkPathForVersionInfo(ShortcutLocation location, ZipPackage package, FileVersionInfo versionInfo, string? rootdir)
        {
            var possibleProductNames = new[] {
                    versionInfo.ProductName,
                    package.ProductName,
                    versionInfo.FileDescription,
                    Path.GetFileNameWithoutExtension(versionInfo.FileName)
                };

            var possibleCompanyNames = new[] {
                    versionInfo.CompanyName,
                    package.ProductCompany,
                };

            var prodName = possibleCompanyNames.First(x => !string.IsNullOrWhiteSpace(x));
            var pkgName = possibleProductNames.First(x => !string.IsNullOrWhiteSpace(x));

            return GetLinkPath(location, pkgName, prodName, rootdir);
        }

        /// <summary>
        /// Given the application info, return the shortcut target path.
        /// </summary>
        protected virtual string GetLinkPath(ShortcutLocation location, string? title, string? applicationName, string? rootdir, bool createDirectoryIfNecessary = true)
        {
            var dir = default(string);

            switch (location)
            {
                case ShortcutLocation.Desktop:
                    dir = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
                    break;
                case ShortcutLocation.StartMenu:
                    dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.StartMenu), "Programs", applicationName);
                    break;
                case ShortcutLocation.StartMenuRoot:
                    dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.StartMenu), "Programs");
                    break;
                case ShortcutLocation.Startup:
                    dir = Environment.GetFolderPath(Environment.SpecialFolder.Startup);
                    break;
                case ShortcutLocation.AppRoot:
                    dir = rootdir;
                    break;
            }

            if (createDirectoryIfNecessary && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            return Path.Combine(dir, title + ".lnk");
        }

        private ShortcutLocation[] GetLocations(ShortcutLocation flag)
        {
            var locations = Enum.GetValues(typeof(ShortcutLocation)).Cast<ShortcutLocation>().ToArray();
            return locations
                .Where(x => x != ShortcutLocation.None)
                .Where(x => flag.HasFlag(x))
                .ToArray();
        }
    }
}
