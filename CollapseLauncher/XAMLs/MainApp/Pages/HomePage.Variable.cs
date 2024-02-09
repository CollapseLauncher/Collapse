using Hi3Helper.Preset;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Data;
using System;
using static CollapseLauncher.RegionResourceListHelper;
using static Hi3Helper.Shared.Region.LauncherConfig;

namespace CollapseLauncher.Pages
{
    public sealed partial class HomePage : Page
    {
        string GameDirPath { get => CurrentGameProperty._GameVersion.GameDirPath; }

        public Visibility IsPostEventPanelVisible => (regionNewsProp.articlePanel?.Events.Count ?? 0) == 0 ? Visibility.Collapsed : Visibility.Visible;
        public Visibility IsPostEventPanelEmpty => (regionNewsProp.articlePanel?.Events.Count ?? 0) != 0 ? Visibility.Collapsed : Visibility.Visible;
        public Visibility IsPostNoticePanelVisible => (regionNewsProp.articlePanel?.Notices.Count ?? 0) == 0 ? Visibility.Collapsed : Visibility.Visible;
        public Visibility IsPostNoticePanelEmpty => (regionNewsProp.articlePanel?.Notices.Count ?? 0) != 0 ? Visibility.Collapsed : Visibility.Visible;
        public Visibility IsPostInfoPanelVisible => (regionNewsProp.articlePanel?.Info.Count ?? 0) == 0 ? Visibility.Collapsed : Visibility.Visible;
        public Visibility IsPostInfoPanelEmpty => (regionNewsProp.articlePanel?.Info.Count ?? 0) != 0 ? Visibility.Collapsed : Visibility.Visible;

        public bool IsEventsPanelShow
        {
            get
            {
                bool ret = GetAppConfigValue("ShowEventsPanel").ToBoolNullable() ?? true;
                return ret;
            }
            set
            {
                SetAndSaveConfigValue("ShowEventsPanel", value);
                ToggleEventsPanel(value);
            }
        }

        public bool IsSocMedPanelShow
        {
            get
            {
                bool ret = GetAppConfigValue("ShowSocialMediaPanel").ToBoolNullable() ?? true;
                return ret;
            }
            set
            {
                SetAndSaveConfigValue("ShowSocialMediaPanel", value);
                ToggleSocmedPanelPanel(value);
            }
        }

        public string NoNewsSplashMascot
        {
            get
            {
                GameType gameType = CurrentGameProperty._GamePreset.GameType;
                return gameType switch
                {
                    GameType.Honkai => "ms-appx:///Assets/Images/GameMascot/AiShocked.png",
                    GameType.StarRail => "ms-appx:///Assets/Images/GameMascot/PomPomWhat.png",
                    GameType.Zenless => "ms-appx:///Assets/Images/GameMascot/BangbooShocked.png",
                    _ => "ms-appx:///Assets/Images/GameMascot/PaimonWhat.png"
                };
            }
        }

        public Thickness NoNewsSplashMascotMargin
        {
            get
            {
                GameType gameType = CurrentGameProperty._GamePreset.GameType;
                Thickness currentMargin = WindowSize.WindowSize.CurrentWindowSize.PostPanelPaimonTextMargin;
                currentMargin.Right += gameType switch
                {
                    GameType.Honkai => 16,
                    GameType.StarRail => 0,
                    GameType.Zenless => 0,
                    _ => 0
                };
                return currentMargin;
            }
        }

        public void ToggleEventsPanel(bool hide) => HideImageCarousel(!hide);
        public void ToggleSocmedPanelPanel(bool hide) => HideSocialMediaPanel(!hide);
    }

    public class NullVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string input) => (bool)value ? Visibility.Visible : Visibility.Collapsed;
        public object ConvertBack(object value, Type targetType, object parameter, string input) => new NotImplementedException();
    }
}
