using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using static CollapseLauncher.FileDialogNative;
using static Hi3Helper.Locale;
using static Hi3Helper.Logger;
using static Hi3Helper.Preset.ConfigV2Store;
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
                LogWriteLine($"Migration process is cancelled for Game {CurrentConfigV2.ZoneFullname}");
                MigrationWatcher.IsMigrationRunning = false;
                MainFrameChanger.ChangeWindowFrame(typeof(MainPage));
                return;
            }
            catch (Exception ex)
            {
                LogWriteLine($"Error occured while migrating Game {CurrentConfigV2.ZoneFullname}\r\nTraceback: {ex}");
                MigrationWatcher.IsMigrationRunning = false;
                MainFrameChanger.ChangeWindowFrame(typeof(MainPage));
                return;
            }

            await StartMigrationProcess();
        }

        private async Task<string> AskMigrationTarget()
        {
            // StorageFolder folder;
            string folder = "";
            // string returnFolder = "";

            var cd = new ContentDialog
            {
                Title = Lang._Dialogs.MigrationTitle,
                Content = Lang._Dialogs.MigrationSubtitle,
                CloseButtonText = Lang._Misc.Cancel,
                PrimaryButtonText = CurrentConfigV2.MigrateFromBetterHi3Launcher ? Lang._Misc.UseCurrentDir : Lang._Misc.UseDefaultDir,
                SecondaryButtonText = Lang._Misc.YesChangeLocation,
                DefaultButton = ContentDialogButton.Primary,
                Background = (Brush)Application.Current.Resources["DialogAcrylicBrush"]
            };

            cd.XamlRoot = Content.XamlRoot;

            switch (await cd.ShowAsync())
            {
                case ContentDialogResult.Secondary:
                    folder = await GetFolderPicker();

                    if (folder == null)
                        await MigrationCancelled(3000);
                    break;
                case ContentDialogResult.Primary:
                    if (CurrentConfigV2.MigrateFromBetterHi3Launcher)
                    {
                        folder = CurrentConfigV2.BetterHi3LauncherConfig.game_info.install_path;
                        UseCurrentBHI3LFolder = true;
                    }
                    else
                        folder = Path.Combine(AppGameFolder, CurrentConfigV2.ProfileName);
                    break;
                case ContentDialogResult.None:
                    await MigrationCancelled(3000);
                    break;
            }

            return folder;
        }

        private async Task MigrationCancelled(int delay, bool noException = false)
        {
            DispatcherQueue.TryEnqueue(() =>
            {
                MigrationTextStatus.Visibility = Visibility.Visible;

                MigrationSubtextStatus.Visibility = Visibility.Collapsed;
                MigrationTextStatus.Text = Lang._InstallMigrate.StepCancelledTitle;
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
                MigrationTextStatus.Text = Lang._InstallMigrate.Step1Title;
                MigrationTextStatus.Visibility = Visibility.Visible;
            });
            await Task.Delay(5000);

            DispatcherQueue.TryEnqueue(() =>
            {
                MigrationTextStatus.Text = Lang._InstallMigrate.Step2Title;
            });

            try
            {
                Process proc = new Process();
                proc.StartInfo.FileName = Path.Combine(AppFolder, "CollapseLauncher.exe");
                proc.StartInfo.UseShellExecute = true;
                if (CurrentConfigV2.MigrateFromBetterHi3Launcher)
                    proc.StartInfo.Arguments = $"migratebhi3l --gamever {CurrentConfigV2.BetterHi3LauncherConfig.game_info.version} --regloc {CurrentConfigV2.BetterHi3LauncherVerInfoReg} --input \"{CurrentConfigV2.BetterHi3LauncherConfig.game_info.install_path}\" --output \"{targetPath}\"";
                else
                    proc.StartInfo.Arguments = $"migrate --input \"{CurrentConfigV2.ActualGameLocation}\" --output \"{targetPath}\"";
                proc.StartInfo.Verb = "runas";

                LogWriteLine($"Launching Invoker with Argument:\r\n\t{proc.StartInfo.Arguments}");

                proc.Start();
                await Task.Run(() => proc.WaitForExit());

                DispatcherQueue.TryEnqueue(() =>
                {
                    MigrationTextStatus.Text = Lang._InstallMigrate.Step3Title;
                    MigrationSubtextStatus.Visibility = Visibility.Visible;
                });

                if (UseCurrentBHI3LFolder)
                    gameIni.Profile["launcher"]["game_install_path"] = Path.Combine(targetPath).Replace('\\', '/');
                else
                    gameIni.Profile["launcher"]["game_install_path"] = Path.Combine(targetPath, CurrentConfigV2.GameDirectoryName).Replace('\\', '/');

                gameIni.Profile.Save(gameIni.ProfilePath);
            }
            catch (Exception)
            {
                await MigrationCancelled(10, true);
            }

            await Task.Delay(3000);
            MigrationWatcher.IsMigrationRunning = false;
            MainFrameChanger.ChangeWindowFrame(typeof(MainPage));
        }
    }
}