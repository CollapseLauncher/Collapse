using System;
using System.Globalization;
using Hi3Helper.Shared.ClassStruct;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Data;

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
    }

    public class NullVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string input) => (bool)value == true ? Visibility.Visible : Visibility.Collapsed;

        public object ConvertBack(object value, Type targetType, object parameter, string input) => new NotImplementedException();
    }
}
