using Hi3Helper.Data;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using static Hi3Helper.Shared.Region.LauncherConfig;

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
                CurrentVersionLabel.Text = $"{AppCurrentVersion}";
                NewVersionLabel.Text = LauncherUpdateWatcher.UpdateProperty.ver;
                UpdateChannelLabel.Text = IsPreview ? "Preview" : "Stable";
                AskUpdateCheckbox.IsChecked = GetAppConfigValue("DontAskUpdate").ToBoolNullable() ?? false;
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
            string ReleaseNoteURL = string.Format(UpdateRepoChannel + "changelog_{0}", IsPreview ? "preview" : "stable");

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
            SetAndSaveConfigValue("DontAskUpdate", AskForUpdateLater);
        }

        private void RemindMeClick(object sender, RoutedEventArgs e)
        {
            ForceInvokeUpdate = true;
            LauncherUpdateWatcher.GetStatus(new LauncherUpdateProperty { QuitFromUpdateMenu = true });
        }

        private void DoUpdateClick(object sender, RoutedEventArgs e)
        {
            string ExecutableLocation = Path.GetDirectoryName(Process.GetCurrentProcess().MainModule.FileName);
            string UpdateArgument = $"elevateupdate --input \"{ExecutableLocation.Replace('\\', '/')}\" --channel {(IsPreview ? "Preview" : "Stable")}";
            Console.WriteLine(UpdateArgument);
            try
            {
                new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        UseShellExecute = true,
                        FileName = Path.Combine(ExecutableLocation, "CollapseLauncher.exe"),
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
