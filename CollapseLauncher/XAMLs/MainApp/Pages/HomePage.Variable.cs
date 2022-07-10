using Hi3Helper.Shared.ClassStruct;
using Hi3Helper.Shared.Region;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media.Animation;
using System;

using static Hi3Helper.Shared.Region.LauncherConfig;

namespace CollapseLauncher.Pages
{
    public sealed partial class HomePage : Page
    {
        string GameDirPath;

        string GameZipUrl;
        string GameZipPath;
        string GameZipRemoteHash;
        long GameZipRequiredSize;

        string GameZipVoiceUrl;
        string GameZipVoicePath;
        string GameZipVoiceRemoteHash;
        long GameZipVoiceSize;
        long GameZipVoiceRequiredSize;

        RegionResourceVersion VoicePackFile = new RegionResourceVersion();
        InstallManagement InstallTool;

        bool IsGameHasVoicePack;

        public Visibility IsPostEventPanelVisible => LauncherConfig.regionNewsProp.articlePanel?.Events.Count == 0 ? Visibility.Collapsed : Visibility.Visible;
        public Visibility IsPostEventPanelEmpty => LauncherConfig.regionNewsProp.articlePanel?.Events.Count != 0 ? Visibility.Collapsed : Visibility.Visible;
        public Visibility IsPostNoticePanelVisible => LauncherConfig.regionNewsProp.articlePanel?.Notices.Count == 0 ? Visibility.Collapsed : Visibility.Visible;
        public Visibility IsPostNoticePanelEmpty => LauncherConfig.regionNewsProp.articlePanel?.Notices.Count != 0 ? Visibility.Collapsed : Visibility.Visible;
        public Visibility IsPostInfoPanelVisible => LauncherConfig.regionNewsProp.articlePanel?.Info.Count == 0 ? Visibility.Collapsed : Visibility.Visible;
        public Visibility IsPostInfoPanelEmpty => LauncherConfig.regionNewsProp.articlePanel?.Info.Count != 0 ? Visibility.Collapsed : Visibility.Visible;

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

        public async void ToggleEventsPanel(bool hide) => await HideImageCarousel(!hide);
    }

    public class NullVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string input) => (bool)value == true ? Visibility.Visible : Visibility.Collapsed;
        public object ConvertBack(object value, Type targetType, object parameter, string input) => new NotImplementedException();
    }
}
