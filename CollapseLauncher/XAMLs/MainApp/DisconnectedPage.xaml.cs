using CollapseLauncher.Dialogs;
using CollapseLauncher.Helper;
using Hi3Helper;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media.Animation;
using System;
using static Hi3Helper.Logger;

namespace CollapseLauncher
{
    public sealed partial class DisconnectedPage : Page
    {
        public DisconnectedPage()
        {
            try
            {
                this.InitializeComponent();
                WindowUtility.SetWindowBackdrop(WindowBackdropKind.Mica);
            }
            catch (Exception ex)
            {
                LogWriteLine($"FATAL CRASH!!!\r\n{ex}", LogType.Error, true);
                ErrorSender.SendException(ex);
            }
        }

        private void PaimonClicked(object sender, PointerRoutedEventArgs e)
        {
            WindowUtility.SetWindowBackdrop(WindowBackdropKind.None);
            MainFrameChanger.ChangeWindowFrame(typeof(MainPage), new DrillInNavigationTransitionInfo());
        }

        private async void ShowError(object sender, RoutedEventArgs e)
        {
            await SimpleDialogs.Dialog_ShowUnhandledExceptionMenu(this);
            // MainFrameChanger.ChangeWindowFrame(typeof(UnhandledExceptionPage));
        }
    }
}