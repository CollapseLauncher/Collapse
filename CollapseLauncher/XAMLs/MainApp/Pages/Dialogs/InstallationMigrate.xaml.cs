using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using Windows.Storage;
using static CollapseLauncher.InnerLauncherConfig;
using static Hi3Helper.Locale;
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
                MainFrameChanger.ChangeWindowFrame(typeof(MainPage));
                return;
            }
            catch (Exception ex)
            {
                LogWriteLine($"Error occured while migrating Game Region: {CurrentRegion.ZoneName}\r\nTraceback: {ex}");
                MigrationWatcher.IsMigrationRunning = false;
                MainFrameChanger.ChangeWindowFrame(typeof(MainPage));
                return;
            }

            await StartMigrationProcess();
        }

        private async Task<string> AskMigrationTarget()
        {
            StorageFolder folder;
            string returnFolder = "";

            var cd = new ContentDialog
            {
                Title = Lang._Dialogs.MigrationTitle,
                Content = Lang._Dialogs.MigrationSubtitle,
                CloseButtonText = Lang._Misc.Cancel,
                PrimaryButtonText = CurrentRegion.MigrateFromBetterHi3Launcher ? Lang._Misc.UseCurrentDir : Lang._Misc.UseDefaultDir,
                SecondaryButtonText = Lang._Misc.YesChangeLocation,
                DefaultButton = ContentDialogButton.Primary,
                Background = (Brush)Application.Current.Resources["DialogAcrylicBrush"]
            };

            cd.XamlRoot = Content.XamlRoot;

            switch (await cd.ShowAsync())
            {
                case ContentDialogResult.Secondary:
                    folder = await (m_window as MainWindow).GetFolderPicker();

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
                if (CurrentRegion.MigrateFromBetterHi3Launcher)
                    proc.StartInfo.Arguments = $"migratebhi3l --gamever {CurrentRegion.BetterHi3LauncherConfig.game_info.version} --regloc {CurrentRegion.BetterHi3LauncherVerInfoReg} --input \"{CurrentRegion.BetterHi3LauncherConfig.game_info.install_path}\" --output \"{targetPath}\"";
                else
                    proc.StartInfo.Arguments = $"migrate --input \"{CurrentRegion.ActualGameLocation}\" --output \"{targetPath}\"";
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
                    gameIni.Profile["launcher"]["game_install_path"] = Path.Combine(targetPath, CurrentRegion.GameDirectoryName).Replace('\\', '/');

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