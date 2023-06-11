using CollapseLauncher.GameVersioning;
using CollapseLauncher.InstallManager.Base;
using CollapseLauncher.Interfaces;
using Hi3Helper;
using Hi3Helper.Data;
using Hi3Helper.Preset;
using Hi3Helper.Shared.ClassStruct;
using Microsoft.UI.Xaml;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using static CollapseLauncher.Dialogs.SimpleDialogs;
using static Hi3Helper.Locale;
using static Hi3Helper.Logger;

namespace CollapseLauncher.InstallManager.StarRail
{
    internal class StarRailInstall : InstallManagerBase<GameTypeStarRailVersion>, IGameInstallManager
    {
        #region Override Properties
        protected override int _gameVoiceLanguageID { get => _gamePreset.GetVoiceLanguageID(); }
        #endregion

        public StarRailInstall(UIElement parentUI)
            : base(parentUI)
        {

        }

        #region Public Methods
        public override async Task StartPackageInstallation()
        {
            // Run the base installation process
            await base.StartPackageInstallation();

            // Then start on processing hdifffiles list and deletefiles list
            await ApplyHdiffListPatch();
            ApplyDeleteFileAction();
        }

        private void ApplyDeleteFileAction()
        {
            foreach (string path in Directory.EnumerateFiles(_gamePath, "deletefiles_*", SearchOption.TopDirectoryOnly))
            {
                using (StreamReader sw = new StreamReader(path,
                    new FileStreamOptions { Mode = FileMode.Open, Access = FileAccess.Read, Options = FileOptions.DeleteOnClose }))
                {
                    while (!sw.EndOfStream)
                    {
                        string deleteFile = Path.Combine(_gamePath, sw.ReadLine());
                        FileInfo fileInfo = new FileInfo(deleteFile);

                        try
                        {
                            if (fileInfo.Exists)
                            {
                                fileInfo.IsReadOnly = false;
                                fileInfo.Delete();
                                LogWriteLine($"Deleting old file: {deleteFile}");
                            }
                        }
                        catch (Exception ex)
                        {
                            LogWriteLine($"Failed deleting old file: {deleteFile}\r\n{ex}", LogType.Warning, true);
                        }
                    }
                }
            }
        }

        private async Task ApplyHdiffListPatch()
        {
            HPatchUtil patcher = new HPatchUtil();
            List<PkgVersionProperties> hdiffEntry = TryGetHDiffList();

            _progress.ProgressTotalSizeToDownload = hdiffEntry.Sum(x =>
            {
                FileInfo file = new FileInfo(Path.Combine(_gamePath, ConverterTool.NormalizePath(x.remoteName) + ".hdiff"));
                return file.Exists ? file.Length : 0;
            });
            _progress.ProgressTotalDownload = 0;
            _status.IsIncludePerFileIndicator = false;
            RestartStopwatch();

            _progressTotalCount = 1;
            _progressTotalCountFound = hdiffEntry.Count;

            foreach (PkgVersionProperties entry in hdiffEntry)
            {
                _status.ActivityStatus = string.Format("{0}: {1}", Lang._Misc.Patching, string.Format(Lang._Misc.PerFromTo, _progressTotalCount, _progressTotalCountFound));

                string sourcePath = Path.Combine(_gamePath, ConverterTool.NormalizePath(entry.remoteName));
                string patchPath = sourcePath + ".hdiff";
                string destPath = sourcePath + "_tmp";

                try
                {
                    if (File.Exists(sourcePath) && File.Exists(patchPath))
                    {
                        LogWriteLine($"Patching file {entry.remoteName}...", LogType.Default, true);
                        UpdateProgressBase();
                        UpdateStatus();

                        await Task.Run(() => patcher.HPatchDir(sourcePath, patchPath, destPath));
                        File.Move(destPath, sourcePath, true);
                    }
                }
                catch (Exception ex)
                {
                    LogWriteLine($"Error while patching file: {entry.remoteName}. Skipping!\r\n{ex}", LogType.Warning, true);
                }
                finally
                {
                    _progressTotalCount++;
                    FileInfo patchFile = new FileInfo(patchPath);
                    _progress.ProgressTotalDownload += patchFile.Length;
                    _progress.ProgressTotalPercentage = Math.Round(((double)_progress.ProgressTotalDownload / _progress.ProgressTotalSizeToDownload) * 100, 2);
                    _progress.ProgressTotalSpeed = _progress.ProgressTotalDownload / _stopwatch.Elapsed.TotalSeconds;

                    _progress.ProgressTotalTimeLeft = TimeSpan.FromSeconds((_progress.ProgressTotalSizeToDownload - _progress.ProgressTotalDownload) / ConverterTool.Unzeroed(_progress.ProgressTotalSpeed));
                    UpdateProgress();

                    patchFile.IsReadOnly = false;
                    patchFile.Delete();
                }
            }
        }

        private List<PkgVersionProperties> TryGetHDiffList()
        {
            List<PkgVersionProperties> _out = new List<PkgVersionProperties>();
            PkgVersionProperties prop;
            foreach (string listFile in Directory.EnumerateFiles(_gamePath, "*hdifffiles*", SearchOption.TopDirectoryOnly))
            {
                LogWriteLine($"hdiff File list path: {listFile}", LogType.Default, true);

                try
                {
                    using (StreamReader listReader = new StreamReader(listFile,
                        new FileStreamOptions { Mode = FileMode.Open, Access = FileAccess.Read, Options = FileOptions.DeleteOnClose }))
                    {
                        while (!listReader.EndOfStream)
                        {
                            _out.Add(prop = (PkgVersionProperties)JsonSerializer
                                .Deserialize(
                                    listReader.ReadLine(),
                                    typeof(PkgVersionProperties),
                                    PkgVersionPropertiesContext.Default));
                            LogWriteLine($"hdiff entry: {prop.remoteName}", LogType.Default, true);
                        }
                    }
                }
                catch (Exception ex)
                {
                    LogWriteLine($"Failed while trying to read hdiff file list: {listFile}\r\n{ex}", LogType.Warning, true);
                }
            }

            return _out;
        }
        #endregion

        #region Override Methods - GetInstallationPath
        protected override async Task TryAddResourceVersionList(RegionResourceVersion asset, List<GameInstallPackage> packageList)
        {
            // Do action from base method first
            await base.TryAddResourceVersionList(asset, packageList);

            // Initialize langID
            int langID;

            // Get available language names
            List<string> langStrings = EnumerateAudioLanguageString();

            // If the game is not installed or broken, then try set Voice language ID to registry
            if (_gameInstallationStatus == GameInstallStateEnum.NotInstalled
                  || _gameInstallationStatus == GameInstallStateEnum.GameBroken)
            {
                langID = await Dialog_ChooseAudioLanguage(_parentUI, langStrings);

                // Set the voice language ID to value given
                _gamePreset.SetVoiceLanguageID(langID);
            }

            LogWriteLine($"Setting audio ID to: {_gamePreset.GetStarRailVoiceLanguageFullNameByID(_gameVoiceLanguageID)}", LogType.Default, true);
        }

        private List<string> EnumerateAudioLanguageString()
        {
            return new List<string>
            {
                Lang._Misc.LangNameCN,
                Lang._Misc.LangNameENUS,
                Lang._Misc.LangNameJP,
                Lang._Misc.LangNameKR
            };
        }
        #endregion
    }
}
