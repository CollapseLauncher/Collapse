using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;

using Windows.Storage;
using Windows.Storage.Pickers;

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

using static Hi3Helper.InvokeProp;
using static Hi3Helper.Logger;
using static Hi3Helper.Shared.Region.InstallationManagement;
using static Hi3Helper.Shared.Region.LauncherConfig;

namespace CollapseLauncher.Dialogs
{
    public static class MigrationWatcher
    {
        public static bool IsMigrationRunning;
    }

    public partial class InstallationMigrate : Page
    {
        string targetPath;
        bool UseCurrentBHI3LFolder = false;
        public InstallationMigrate()
        {
            this.InitializeComponent();
        }

        private async void Page_Loaded(object sender, RoutedEventArgs e)
        {

            try
            {
                targetPath = await AskMigrationTarget();
            }
            catch (ArgumentNullException)
            {
                LogWriteLine($"Migration process is cancelled for Game Region: {CurrentRegion.ZoneName}");
                MigrationWatcher.IsMigrationRunning = false;
                return;
            }
            catch (Exception ex)
            {
                LogWriteLine($"Error occured while migrating Game Region: {CurrentRegion.ZoneName}\r\nTraceback: {ex}");
                MigrationWatcher.IsMigrationRunning = false;
                return;
            }

            await StartMigrationProcess();
        }

        private async Task<string> AskMigrationTarget()
        {
            FolderPicker folderPicker = new FolderPicker();
            StorageFolder folder;
            string returnFolder = "";

            var cd = new ContentDialog
            {
                Title = "Locating Target Folder",
                Content = $"Before starting this process, Do you want to specify the location of the game?",
                CloseButtonText = "Cancel",
                PrimaryButtonText = CurrentRegion.MigrateFromBetterHi3Launcher ? "Use current directory" : "Use default directory",
                SecondaryButtonText = "Yes, Change location",
                DefaultButton = ContentDialogButton.Primary,
                Background = (Brush)Application.Current.Resources["DialogAcrylicBrush"]
            };

            cd.XamlRoot = Content.XamlRoot;

            switch (await cd.ShowAsync())
            {
                case ContentDialogResult.Secondary:
                    folderPicker.FileTypeFilter.Add("*");
                    WinRT.Interop.InitializeWithWindow.Initialize(folderPicker, m_windowHandle);
                    folder = await folderPicker.PickSingleFolderAsync();

                    if (folder == null)
                        await MigrationCancelled(3000);

                    returnFolder = folder.Path;
                    break;
                case ContentDialogResult.Primary:
                    if (CurrentRegion.MigrateFromBetterHi3Launcher)
                    {
                        returnFolder = CurrentRegion.BetterHi3LauncherConfig.game_info.install_path;
                        UseCurrentBHI3LFolder = true;
                    }
                    else
                        returnFolder = Path.Combine(AppGameFolder, CurrentRegion.ProfileName);
                    break;
                case ContentDialogResult.None:
                    await MigrationCancelled(3000);
                    break;
            }

            return returnFolder;
        }

        private async Task MigrationCancelled(int delay, bool noException = false)
        {
            DispatcherQueue.TryEnqueue(() =>
            {
                MigrationTextStatus.Visibility = Visibility.Visible;

                MigrationSubtextStatus.Visibility = Visibility.Collapsed;
                MigrationTextStatus.Text = "Migration cancelled! Will be return in a moment...";
                MigrationProgressBar.ShowError = true;
            });
            await Task.Delay(delay);

            if (!noException)
                throw new ArgumentNullException();
        }

        private async Task StartMigrationProcess()
        {
            DispatcherQueue.TryEnqueue(() =>
            {
                MigrationTextStatus.Text = "Process will start in 5 seconds. Please accept the UAC dialog to start Migration process";
                MigrationTextStatus.Visibility = Visibility.Visible;
            });
            await Task.Delay(5000);

            DispatcherQueue.TryEnqueue(() =>
            {
                MigrationTextStatus.Text = "Migration is in progress. You may see a console window pops up. Please wait until the process is completed";
            });

            try
            {
                Process proc = new Process();
                proc.StartInfo.FileName = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "CollapseLauncher.Invoker.exe");
                proc.StartInfo.UseShellExecute = true;
                if (CurrentRegion.MigrateFromBetterHi3Launcher)
                    proc.StartInfo.Arguments = $"migratebhi3l {CurrentRegion.BetterHi3LauncherConfig.game_info.version} {CurrentRegion.BetterHi3LauncherVerInfoReg} \"{CurrentRegion.BetterHi3LauncherConfig.game_info.install_path}\" \"{targetPath}\"";
                else
                    proc.StartInfo.Arguments = $"migrate \"{CurrentRegion.ActualGameLocation}\" \"{targetPath}\"";
                proc.StartInfo.Verb = "runas";

                LogWriteLine($"Launching Invoker with Argument:\r\n\t{proc.StartInfo.Arguments}");

                proc.Start();
                await Task.Run(() => proc.WaitForExit());

                DispatcherQueue.TryEnqueue(() =>
                {
                    MigrationTextStatus.Text = "Migration process is done! Will be return in a moment...";
                    MigrationSubtextStatus.Visibility = Visibility.Visible;
                });

                if (UseCurrentBHI3LFolder)
                    gameIni.Profile["launcher"]["game_install_path"] = Path.Combine(targetPath).Replace('\\', '/');
                else
                    gameIni.Profile["launcher"]["game_install_path"] = Path.Combine(targetPath, CurrentRegion.GameDirectoryName).Replace('\\', '/');

                gameIni.Profile.Save(gameIni.ProfilePath);
            }
            catch (Exception)
            {
                await MigrationCancelled(3000, true);
                MigrationWatcher.IsMigrationRunning = false;
            }

            await Task.Delay(3000);
            MigrationWatcher.IsMigrationRunning = false;

            LogWriteLine("After Process");
        }
    }
}