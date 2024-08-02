using CommunityToolkit.Labs.WinUI.Labs.MarkdownTextBlock;
using Hi3Helper;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using System;
using System.Diagnostics;
using System.Net.Http;
using System.Text;
using System.Threading;
using static Hi3Helper.Logger;
using static Hi3Helper.Shared.Region.LauncherConfig;

#nullable enable
namespace CollapseLauncher
{
    public sealed partial class MarkdownFramePage : Page
    {
        /// <summary>
        /// Parameters for the MarkdownFramePage.
        /// Use either MarkdownUri, MarkdownText, or MarkdownUriCdn.
        /// <param name="MarkdownUri">URL of the markdown you want to load</param>
        /// <param name="WebUri">(Optional) URL of the web page you want to open by pressing the "Open in External Browser" button.</param>
        /// <param name="MarkdownText">Raw markdown you want to show</param>
        /// <param name="MarkdownUriCdn">Relative path of Collapse's CDN you want to use for the markdown</param>
        /// </summary>
        public class MarkdownFramePageParams
        {
            public string? MarkdownUri { get; set; }
            public string? WebUri { get; set; }
            public string? MarkdownText { get; set; }
            public string? MarkdownUriCdn { get; set; }
        }

        private string? WebUri;

        private          HttpClient?        _client;
        private readonly MarkdownConfig     _markdownConfig = new();
        internal static  MarkdownFramePage? Current { get; set; }

        public MarkdownFramePage()
        {
            InitializeComponent();
            MarkdownContainer.Text = $"{Locale.Lang._FileCleanupPage.LoadingTitle}...";
            Current                = this;
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);

            if (e.Parameter is MarkdownFramePageParams parameters)
            {
                Initialize(parameters);
            }
        }

        private void Initialize(MarkdownFramePageParams parameters)
        {
            if (parameters.MarkdownText == null && parameters.MarkdownUri == null && parameters.MarkdownUriCdn == null)
                throw new
                    NullReferenceException("[MarkdownFramePage] Either MarkdownUri, MarkdownUriCdn or MarkdownText needs to be filled!");
            
            if (parameters.WebUri != null)
            {
                MarkdownOpenExternalBtn.Visibility = Visibility.Visible;
                WebUri                             = parameters.WebUri;
            }
            SpawnMarkdownPanel(parameters.MarkdownUri,
                               parameters.WebUri,
                               parameters.MarkdownText,
                               parameters.MarkdownUriCdn);
        }

        private async void SpawnMarkdownPanel(string? markdownUri,
                                              string? webUri = null,
                                              string? markdownText = null,
                                              string? markdownUriCdn = null)
        {
            try
            {
                MarkdownPanel.Visibility  =  Visibility.Visible;
                MarkdownPanel.Translation += Shadow32;

                ChangeTitleDragArea.Change(DragAreaTemplate.Full);

                if (markdownText != null)
                {
                    MarkdownContainer.Text = markdownText;
                }
                else if (markdownUri != null)
                {
                    _client             = new HttpClient();
                    _client.BaseAddress = new Uri(markdownUri);
                    _client.DefaultRequestHeaders.Add("User-Agent", $"{GetAppConfigValue("UserAgent").ToString()}");
                    using HttpResponseMessage response = await _client.GetAsync(markdownUri);

                    LogWriteLine($"[MarkdownFramePage] Loading Markdown from URL {markdownUri}\r\n" +
                                 $"{response.EnsureSuccessStatusCode()}", LogType.Scheme);

                    MarkdownContainer.Text = await response.Content.ReadAsStringAsync();
                }
                else if (markdownUriCdn != null)
                {
                    await using BridgedNetworkStream netStream =
                        await FallbackCDNUtil.TryGetCDNFallbackStream(markdownUriCdn,
                                                                      new CancellationToken(), true);
                    byte[] buffer = new byte[netStream.Length];
                    await netStream.ReadExactlyAsync(buffer);

                    MarkdownContainer.Text = Encoding.UTF8.GetString(buffer);
                }
            }
            catch (Exception ex)
            {
                if (webUri != null)
                {
                    LogWriteLine($"Error while initialize Markdown. Open it to browser instead!\r\n{ex}", LogType.Error,
                                 true);
                    new Process
                    {
                        StartInfo = new ProcessStartInfo
                        {
                            UseShellExecute = true,
                            FileName        = webUri
                        }
                    }.Start();
                }
                else
                {
                    LogWriteLine($"Error while initialize Markdown\r\n{ex}", LogType.Error, true);
                    var exFormat = new Exception("Error while initialize Markdown", ex);
                    ErrorSender.SendException(exFormat);
                }
            }
        }

        private void MarkdownOpenExternalBtn_Click(object sender, RoutedEventArgs e)
        {
            new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    UseShellExecute = true,
                    FileName = WebUri
                }
            }.Start();
        }
    }
}
