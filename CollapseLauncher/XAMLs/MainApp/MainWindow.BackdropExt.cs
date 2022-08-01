using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Microsoft.UI.Xaml;
using Microsoft.WindowsAPICodePack.Dialogs;

using static CollapseLauncher.InnerLauncherConfig;

namespace CollapseLauncher
{
    public sealed partial class MainWindow : Window
    {
        public string GetFolderPicker()
        {
            CommonOpenFileDialog dialog = new CommonOpenFileDialog();
            dialog.IsFolderPicker = true;

            CommonFileDialogResult Result = dialog.ShowDialog(m_windowHandle);

            if (Result == CommonFileDialogResult.Ok)
                return dialog.FileName;

            return null;
        }

        public string GetFilePicker(Dictionary<string, string> FileTypeFilter = null)
        {
            CommonOpenFileDialog dialog = new CommonOpenFileDialog();

            if (FileTypeFilter != null)
                foreach (KeyValuePair<string, string> entry in FileTypeFilter)
                    dialog.Filters.Add(new CommonFileDialogFilter(entry.Key, entry.Value));

            CommonFileDialogResult Result = dialog.ShowDialog(m_windowHandle);

            if (Result == CommonFileDialogResult.Ok)
                return dialog.FileName;

            return null;
        }

        public void SetThemeParameters()
        {
            switch (m_currentBackdrop)
            {
#if MICA
                case BackdropType.Mica:
                    {
                        (Application.Current.Resources["PagesSolidAcrylicBrush"] as AcrylicBrush).TintOpacity = 0f;
                        (Application.Current.Resources["PagesSolidAcrylicBrush"] as AcrylicBrush).TintLuminosityOpacity = 0f;
                        (Application.Current.Resources["DialogAcrylicBrush"] as AcrylicBrush).TintOpacity = 0f;
                        (Application.Current.Resources["DialogAcrylicBrush"] as AcrylicBrush).TintLuminosityOpacity = 0.75f;
                        (Application.Current.Resources["NavigationBarBrush"] as AcrylicBrush).TintOpacity = 0f;
                        (Application.Current.Resources["NavigationBarBrush"] as AcrylicBrush).TintLuminosityOpacity = 0f;
                    }
                    break;
#endif
                case BackdropType.DefaultColor:
                    {
#if !DISABLETRANSPARENT
                        if (CurrentRequestedAppTheme == ApplicationTheme.Light)
                            Application.Current.Resources["NavigationBarBrush"] = new AcrylicBrush
                            {
                                TintColor = new Windows.UI.Color { A = 244, R = 255, G = 255, B = 255 },
                                TintOpacity = 0f,
                                TintLuminosityOpacity = 0f
                            };

                        if (CurrentRequestedAppTheme == ApplicationTheme.Dark)
                        {
                            Application.Current.Resources["NavigationBarBrush"] = new AcrylicBrush
                            {
                                TintColor = new Windows.UI.Color { A = 244, R = 34, G = 34, B = 34 },
                                TintOpacity = 0f,
                                TintLuminosityOpacity = 0f
                            };
                            Application.Current.Resources["PagesSolidAcrylicBrush"] = new AcrylicBrush
                            {
                                TintColor = new Windows.UI.Color { A = 244, R = 34, G = 34, B = 34 },
                                TintOpacity = 1f,
                                TintLuminosityOpacity = 0f
                            };
                            Application.Current.Resources["DialogAcrylicBrush"] = new AcrylicBrush
                            {
                                TintColor = new Windows.UI.Color { A = 244, R = 34, G = 34, B = 34 },
                                TintOpacity = 0.4f,
                                TintLuminosityOpacity = 0.5f
                            };
                        }
#endif
                    }
                    break;
            }
        }
    }
}
