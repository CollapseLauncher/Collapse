using System;
using System.Reflection;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using System.IO;
using System.Text;

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

using Hi3Helper.Data;
using Hi3Helper.Shared.Region;

namespace CollapseLauncher.Pages
{
    public sealed partial class UpdatePage : Page
    {
        public UpdatePage()
        {
            this.InitializeComponent();
            RunAsyncTasks();
        }

        public async void RunAsyncTasks()
        {
            DispatcherQueue.TryEnqueue(() =>
            {
                CurrentVersionLabel.Text = $"{LauncherConfig.AppCurrentVersion}";
                NewVersionLabel.Text = LauncherUpdateWatcher.UpdateProperty.ver;
                UpdateChannelLabel.Text = AppConfig.IsPreview ? "Preview" : "Stable";
                AskUpdateCheckbox.IsChecked = LauncherConfig.GetAppConfigValue("DontAskUpdate").ToBoolNullable() ?? false;
                BuildTimestampLabel.Text = new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc)
                                            .AddSeconds(LauncherUpdateWatcher.UpdateProperty.time)
                                            .ToLocalTime().ToString("f");
            });

            await GetReleaseNote();
        }

        public async Task GetReleaseNote()
        {
            DispatcherQueue.TryEnqueue(() => ReleaseNotesBox.Text = "Loading Release Notes...");

            MemoryStream ResponseStream = new MemoryStream();
            string ReleaseNoteURL = string.Format(LauncherConfig.UpdateRepoChannel + "changelog_{0}", AppConfig.IsPreview ? "preview" : "stable");

            try
            {
                await new HttpClientHelper().DownloadFileAsync(ReleaseNoteURL, ResponseStream, new CancellationToken());
                string Content = Encoding.UTF8.GetString(ResponseStream.ToArray());

                DispatcherQueue.TryEnqueue(() => ReleaseNotesBox.Text = Content);
            }
            catch (Exception ex)
            {
                DispatcherQueue.TryEnqueue(() => ReleaseNotesBox.Text = $"Error while fetching Release Notes.\r\n{ex}");
            }
        }

        private void AskUpdateToggle(object sender, RoutedEventArgs e)
        {
            bool AskForUpdateLater = (sender as CheckBox).IsChecked ?? false;
            LauncherConfig.SetAppConfigValue("DontAskUpdate", AskForUpdateLater);
        }

        private void RemindMeClick(object sender, RoutedEventArgs e)
        {
            LauncherConfig.ForceInvokeUpdate = true;
            LauncherUpdateWatcher.GetStatus(new LauncherUpdateProperty { QuitFromUpdateMenu = true });
        }

        private void DoUpdateClick(object sender, RoutedEventArgs e)
        {
            string ExecutableLocation = Path.GetDirectoryName(Process.GetCurrentProcess().MainModule.FileName);
            string UpdateArgument = $"elevateupdate \"{ExecutableLocation.Replace('\\', '/')}\" {(AppConfig.IsPreview ? "preview" : "stable")}";
            Console.WriteLine(UpdateArgument);
            try
            {
                new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        UseShellExecute = true,
                        FileName = Path.Combine(ExecutableLocation, "CollapseLauncher.Updater.exe"),
                        Arguments = UpdateArgument,
                        Verb = "runas"
                    }
                }.Start();
            }
            catch
            {
                return;
            }
            App.Current.Exit();
        }
    }
}
