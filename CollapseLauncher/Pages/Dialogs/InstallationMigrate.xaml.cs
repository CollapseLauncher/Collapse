using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using Windows.Storage;
using Windows.Storage.Pickers;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Runtime.InteropServices;
using Windows.Foundation;
using Windows.Foundation.Collections;

using static Hi3Helper.Shared.Region.LauncherConfig;
using static Hi3Helper.Shared.Region.InstallationManagement;
using static Hi3Helper.Logger;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace CollapseLauncher.Dialogs
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public static class MigrationWatcher
    {
        public static bool IsMigrationRunning;
    }

    public partial class InstallationMigrate : Page
    {
        string targetPath;
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
                PrimaryButtonText = "Use default directory",
                SecondaryButtonText = "Yes, Change location",
                DefaultButton = ContentDialogButton.Primary,
                Background = (Brush)Application.Current.Resources["DialogAcrylicBrush"]
            };

            cd.XamlRoot = Content.XamlRoot;
            
                switch (await cd.ShowAsync())
                {
                    case ContentDialogResult.Secondary:
                        folderPicker.FileTypeFilter.Add("*");
                        WinRT.Interop.InitializeWithWindow.Initialize(folderPicker, InvokeProp.m_windowHandle);
                        folder = await folderPicker.PickSingleFolderAsync();

                        if (folder == null)
                            await MigrationCancelled(3000);

                        returnFolder = folder.Path;
                        break;
                    case ContentDialogResult.Primary:
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
