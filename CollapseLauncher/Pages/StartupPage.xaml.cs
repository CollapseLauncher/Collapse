using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Storage;
using Windows.Storage.Pickers;

using static Hi3Helper.InvokeProp;
using static Hi3Helper.Data.ConverterTool;
using static Hi3Helper.Shared.Region.LauncherConfig;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace CollapseLauncher.Pages
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
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
            WinRT.Interop.InitializeWithWindow.Initialize(folderPicker, m_windowHandle);

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
