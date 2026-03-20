using CollapseLauncher.Dialogs;
using CollapseLauncher.Helper;
using CollapseLauncher.Helper.Metadata;
using Hi3Helper;
using Hi3Helper.Shared.Region;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media.Animation;
using System;
using System.Collections.Generic;
using static Hi3Helper.Logger;
// ReSharper disable IdentifierTypo
// ReSharper disable AsyncVoidMethod
// ReSharper disable CheckNamespace

#nullable enable
namespace CollapseLauncher
{
    public sealed partial class DisconnectedPage
    {
        public DisconnectedPage()
        {
            try
            {
                InitializeComponent();
                WindowUtility.SetWindowBackdrop(WindowBackdropKind.Mica);
                OverlayFrame.Navigate(typeof(Pages.NullPage));
                InitializeRegionComboBox();
            }
            catch (Exception ex)
            {
                LogWriteLine($"FATAL CRASH!!!\r\n{ex}", LogType.Error, true);
                ErrorSender.SendException(ex);
            }
        }

        private void InitializeRegionComboBox()
        {
            string? gameTitle = LauncherConfig.GetAppConfigValue("GameCategory");

            List<string>       gameTitleList  = LauncherMetadataHelper.GetGameTitleList();
            List<PresetConfig> gameRegionList = LauncherMetadataHelper.GetGameRegionList(gameTitle ?? "");

            if (gameRegionList.Count == 0)
            {
                gameTitle      = gameTitleList[0];
                gameRegionList = LauncherMetadataHelper.GetGameRegionList(gameTitle);
            }

            ComboBoxGameTitle.ItemsSource  = gameTitleList;
            ComboBoxGameRegion.ItemsSource = gameRegionList;

            int indexCategory = gameTitleList.IndexOf(gameTitle ?? "");
            if (indexCategory < 0) indexCategory = 0;

            int indexRegion = LauncherMetadataHelper.GetGameRegionLastSavedIndexOrDefault(gameTitle);

            ComboBoxGameTitle.SelectedIndex = indexCategory;
            ComboBoxGameRegion.SelectedIndex = indexRegion;
        }

        private void PaimonClicked(object sender, PointerRoutedEventArgs e)
        {
            WindowUtility.SetWindowBackdrop(WindowBackdropKind.None);
            MainFrameChanger.ChangeWindowFrame(typeof(MainPage), new DrillInNavigationTransitionInfo());
        }

        private async void ShowError(object sender, RoutedEventArgs e)
        {
            await SimpleDialogs.Dialog_ShowUnhandledExceptionMenu();
        }

        private void GoToAppSettings(object sender, RoutedEventArgs e)
        {
            OverlayFrame.Navigate(typeof(Pages.SettingsPage));
            GoBackOverlayFrame.Visibility = Visibility.Visible;
            OverlayFrameBg.Visibility = Visibility.Visible;
            FrontGrid.Visibility = Visibility.Collapsed;
        }

        private void GoBackFromOverlayFrame(object sender, RoutedEventArgs e)
        {
            if (OverlayFrame?.CanGoBack ?? false)
                OverlayFrame?.GoBack();

            if ((OverlayFrame?.CanGoBack ?? false) || sender is not Button btn)
            {
                return;
            }

            btn.Visibility            = Visibility.Collapsed;
            OverlayFrameBg.Visibility = Visibility.Collapsed;
            FrontGrid.Visibility      = Visibility.Visible;
        }

        private void SetGameTitleChange(object sender, SelectionChangedEventArgs e)
        {
            object? selectedItem = ((ComboBox)sender).SelectedItem;
            if (selectedItem is not string asGameTitleString) return;

            ComboBoxGameRegion.ItemsSource = LauncherMetadataHelper.GetGameRegionList(asGameTitleString);
            ComboBoxGameRegion.SelectedIndex = LauncherMetadataHelper.GetGameRegionLastSavedIndexOrDefault(asGameTitleString);
        }

        private async void SetGameRegionChange(object sender, SelectionChangedEventArgs e)
        {
            if (ComboBoxGameTitle.SelectedValue is not string asGameTitleString) return;
            if (((ComboBox)sender).SelectedValue is not PresetConfig asGameRegionString) return;

            _ = await LauncherMetadataHelper.GetMetadataConfig(asGameTitleString, asGameRegionString.ZoneName ?? "");

            // Set and Save CurrentRegion in AppConfig
            LauncherConfig.SetAndSaveConfigValue("GameCategory", asGameTitleString);
            LauncherMetadataHelper.SaveGameRegionIndex(asGameTitleString, asGameRegionString.ZoneName ?? "");
        }
    }
}