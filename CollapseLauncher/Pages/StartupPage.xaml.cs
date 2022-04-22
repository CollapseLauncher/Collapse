using System;
using System.IO;
using Windows.Storage;
using Windows.Storage.Pickers;

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

using static Hi3Helper.Data.ConverterTool;
using static Hi3Helper.Shared.Region.LauncherConfig;

namespace CollapseLauncher.Pages
{
    public sealed partial class StartupPage : Page
    {
        public StartupPage()
        {
            this.InitializeComponent();
        }

        private async void ChooseFolder(object sender, RoutedEventArgs e)
        {
            FolderPicker folderPicker = new FolderPicker();
            StorageFolder folder;
            string returnFolder;

            folderPicker.FileTypeFilter.Add("*");
            WinRT.Interop.InitializeWithWindow.Initialize(folderPicker, AppConfig.m_windowHandle);

            switch (await Dialogs.SimpleDialogs.Dialog_LocateFirstSetupFolder(Content, Path.Combine(AppDataFolder, "GameFolder")))
            {
                case ContentDialogResult.Primary:
                    AppGameFolder = Path.Combine(AppDataFolder, "GameFolder");
                    MainFrameChanger.ChangeWindowFrame(typeof(MainPage));
                    break;
                case ContentDialogResult.Secondary:
                    folder = await folderPicker.PickSingleFolderAsync();
                    if (folder != null)
                        if (IsUserHasPermission(returnFolder = folder.Path))
                        {
                            AppGameFolder = returnFolder;
                            ErrMsg.Text = "";
                            MainFrameChanger.ChangeWindowFrame(typeof(MainPage));
                        }
                        else
                            ErrMsg.Text = "Permission Denied! Choose another folder!";
                    else
                        ErrMsg.Text = "Folder hasn't been choosen!";
                    break;

            }
        }
    }
}
