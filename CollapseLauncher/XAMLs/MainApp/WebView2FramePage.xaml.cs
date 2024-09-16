using Hi3Helper;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Data;
using Microsoft.Web.WebView2.Core;
using System;
using System.Diagnostics;
using System.IO;
using static Hi3Helper.Logger;
using static Hi3Helper.Shared.Region.LauncherConfig;

namespace CollapseLauncher
{
    public sealed partial class WebView2FramePage : Page
    {
        public static Uri WebView2URL;
        private WebView2 WebView2Runtime;

        public WebView2FramePage()
        {
            this.InitializeComponent();
            SpawnWebView2Panel(WebView2URL);
        }

        private async void SpawnWebView2Panel(Uri URL)
        {
            try
            {
                Environment.SetEnvironmentVariable("WEBVIEW2_USER_DATA_FOLDER", Path.Combine(AppGameFolder, "_webView2"));

                WebView2Runtime = new WebView2()
                                  {
                                      HorizontalAlignment = HorizontalAlignment.Stretch,
                                      VerticalAlignment   = VerticalAlignment.Stretch
                                  };
                WebViewWindowTitle.Text = string.Empty;

                WebView2Runtime.CoreWebView2Initialized += WebView2Window_CoreWebView2Initialized;
                WebView2Runtime.NavigationStarting      += WebView2Window_PageLoading;
                WebView2Runtime.NavigationCompleted     += WebView2Window_PageLoaded;

                WebView2WindowContainer.Children.Clear();
                WebView2WindowContainer.Children.Add(WebView2Runtime);

                WebView2Panel.Visibility  =  Visibility.Visible;
                WebView2Panel.Translation += Shadow32;
                await WebView2Runtime.EnsureCoreWebView2Async();

                SetWebView2Bindings();
                WebView2Runtime.Source = URL;

                ChangeTitleDragArea.Change(DragAreaTemplate.Full);
            }
            catch (Exception ex)
            {
                LogWriteLine($"Error while initialize WebView2. Open it to browser instead!\r\n{ex}", LogType.Error, true);
                new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        UseShellExecute = true,
                        FileName = URL.ToString()
                    }
                }.Start();

                WebView2Runtime?.Close();
                SpawnWebView2.SpawnWebView2Window(null, this.Content);
            }
        }

        private void SetWebView2Bindings()
        {
            BindingOperations.SetBinding(WebView2BackBtn, IsEnabledProperty, new Binding()
            {
                Source = WebView2Runtime,
                Path = new PropertyPath("CanGoBack"),
                Mode = BindingMode.OneWay,
                UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged
            });
            BindingOperations.SetBinding(WebView2ForwardBtn, IsEnabledProperty, new Binding()
            {
                Source = WebView2Runtime,
                Path = new PropertyPath("CanGoForward"),
                Mode = BindingMode.OneWay,
                UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged
            });
            BindingOperations.SetBinding(WebView2URLBox, TextBox.TextProperty, new Binding()
            {
                Source = WebView2Runtime,
                Path = new PropertyPath("Source"),
                Mode = BindingMode.OneWay,
                UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged
            });
        }

        private void CoreWebView2_DocumentTitleChanged(CoreWebView2 sender, object args) => WebViewWindowTitle.Text = sender.DocumentTitle;
        private void WebView2Window_CoreWebView2Initialized(WebView2 sender, CoreWebView2InitializedEventArgs args)
        {
            try
            {
                sender.CoreWebView2.DocumentTitleChanged += CoreWebView2_DocumentTitleChanged;
            }
            catch (Exception ex)
            {
                if (ex is NotSupportedException nsEx)
                {
                    LogWriteLine($"Half-baked NativeAOT Bug (nice MSFT!) :) https://github.com/MicrosoftEdge/WebView2Feedback/issues/4783\r\n{nsEx}", LogType.Error, true);
                }
            }
        }
        private void WebView2Window_PageLoaded(WebView2 sender, CoreWebView2NavigationCompletedEventArgs args) => WebView2LoadingStatus.IsIndeterminate = false;
        private void WebView2Window_PageLoading(WebView2 sender, CoreWebView2NavigationStartingEventArgs args) => WebView2LoadingStatus.IsIndeterminate = true;
        private void WebView2BackBtn_Click(object sender, RoutedEventArgs e) => WebView2Runtime.GoBack();
        private void WebView2ForwardBtn_Click(object sender, RoutedEventArgs e) => WebView2Runtime.GoForward();
        private void WebView2ReloadBtn_Click(object sender, RoutedEventArgs e) => WebView2Runtime.Reload();
        private void WebView2OpenExternalBtn_Click(object sender, RoutedEventArgs e)
        {
            new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    UseShellExecute = true,
                    FileName = WebView2Runtime.Source.ToString()
                }
            }.Start();
        }
        private void WebView2CloseBtn_Click(object sender, RoutedEventArgs e) => SpawnWebView2.SpawnWebView2Window(null, this.Content);

        private void WebView2Unload(object sender, RoutedEventArgs e)
        {
            ChangeTitleDragArea.Change(DragAreaTemplate.Default);
            WebView2Runtime.Visibility = Visibility.Collapsed;
            WebView2Runtime.Close();
            WebView2Panel.Visibility = Visibility.Collapsed;
            WebView2Panel.Translation -= Shadow32;

            WebView2Runtime.CoreWebView2Initialized -= WebView2Window_CoreWebView2Initialized;
            WebView2Runtime.NavigationStarting -= WebView2Window_PageLoading;
            WebView2Runtime.NavigationCompleted -= WebView2Window_PageLoaded;
        }
    }
}
