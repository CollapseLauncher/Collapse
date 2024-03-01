using CollapseLauncher.Extension;
using CollapseLauncher.CustomControls;
using CollapseLauncher.Dialogs;
using CollapseLauncher.FileDialogCOM;
using Hi3Helper;
using Hi3Helper.Data;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Documents;
using Microsoft.UI.Xaml.Media;
using System.IO;
using System.Threading.Tasks;

namespace CollapseLauncher
{
    internal partial class FileMigrationProcess
    {
        private static async ValueTask<string> BuildCheckOutputPathUI(UIElement parentUI, string dialogTitle, string inputPath, string outputPath, bool isFileTransfer)
        {
            ContentDialogCollapse mainDialogWindow = new ContentDialogCollapse(ContentDialogTheme.Informational)
            {
                Title = dialogTitle,
                CloseButtonText = Locale.Lang!._Misc!.Cancel,
                PrimaryButtonText = null,
                SecondaryButtonText = null,
                DefaultButton = ContentDialogButton.Primary,
                XamlRoot = parentUI!.XamlRoot
            };

            Grid mainGrid = new Grid();
            mainGrid.AddGridRows(3);
            mainGrid.AddGridColumns(1, new GridLength(1.0, GridUnitType.Star));
            mainGrid.AddGridColumns(1);

            // ReSharper disable once UnusedVariable
            TextBlock locateFolderSubtitle = mainGrid.AddElementToGridColumn(new TextBlock
            {
                FontSize = 16d,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                TextWrapping = TextWrapping.Wrap,
                Text = Locale.Lang._FileMigrationProcess!.LocateFolderSubtitle
            }, 0, 2);

            TextBox choosePathTextBox = mainGrid.AddElementToGridRow(new TextBox
            {
                Margin = new Thickness(0d, 12d, 0d, 0d),
                IsSpellCheckEnabled = false,
                IsRightTapEnabled = false,
                Width = 500,
                PlaceholderText = Locale.Lang._FileMigrationProcess.ChoosePathTextBoxPlaceholder,
                Text = string.IsNullOrEmpty(outputPath) ? null : outputPath
            }, 1);

            Button choosePathButton = mainGrid
                .AddElementToGridRowColumn(UIElementExtensions
                    .CreateButtonWithIcon<Button>(Locale.Lang._FileMigrationProcess.ChoosePathButton, "", "FontAwesome", "AccentButtonStyle"),
                    1, 1);
            choosePathButton!.Margin = new Thickness(8d, 12d, 0d, 0d);

            TextBlock warningText = mainGrid.AddElementToGridRowColumn(new TextBlock
            {
                FontStyle = Windows.UI.Text.FontStyle.Italic,
                FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 255, 0, 0)),
                Margin = new Thickness(0d, 12d, 0d, 0d),
                Visibility = Visibility.Collapsed,
                TextWrapping = TextWrapping.Wrap,
                Text = ""
            }, 2, 0, 0, 2);

            mainDialogWindow.Content = mainGrid;

            if (!string.IsNullOrEmpty(outputPath))
                ToggleOrCheckPathWarning(outputPath);

            choosePathButton.Click += async (_, _) =>
            {
                string pathResult = isFileTransfer ? await FileDialogNative.GetFileSavePicker(null, dialogTitle) :
                                                       await FileDialogNative.GetFolderPicker(dialogTitle);

                choosePathTextBox!.Text = string.IsNullOrEmpty(pathResult) ? null : pathResult;
            };
            choosePathTextBox!.TextChanged += (sender, _) => ToggleOrCheckPathWarning(((TextBox)sender!).Text);

            void ToggleOrCheckPathWarning(string path)
            {
                string parentPath = path;
                if (isFileTransfer) parentPath = Path.GetDirectoryName(path);

                if (string.IsNullOrEmpty(parentPath))
                {
                    ToggleWarningText(Locale.Lang!._FileMigrationProcess!.ChoosePathErrorPathUnselected);
                    return;
                }
                if (!(File.Exists(parentPath) || Directory.Exists(parentPath)))
                {
                    ToggleWarningText(Locale.Lang!._FileMigrationProcess!.ChoosePathErrorPathNotExist);
                    return;
                }
                if (!ConverterTool.IsUserHasPermission(parentPath) || IsOutputPathSameAsInput(inputPath, path, isFileTransfer))
                {
                    ToggleWarningText(Locale.Lang!._FileMigrationProcess!.ChoosePathErrorPathNoPermission);
                    return;
                }
                ToggleWarningText();
            }

            void ToggleWarningText(string text = null)
            {
                bool canContinue = string.IsNullOrEmpty(text);
                warningText!.Visibility = canContinue ? Visibility.Collapsed : Visibility.Visible;
                warningText!.Text = text;
                mainDialogWindow.PrimaryButtonText = canContinue ? Locale.Lang!._Misc!.Next : null;
            }

            ContentDialogResult mainDialogWindowResult = await mainDialogWindow.QueueAndSpawnDialog();
            return mainDialogWindowResult == ContentDialogResult.Primary ? choosePathTextBox.Text : null;
        }

        private FileMigrationProcessUIRef BuildMainMigrationUI()
        {
            ContentDialogCollapse mainDialogWindow = new ContentDialogCollapse(ContentDialogTheme.Informational)
            {
                Title = this.dialogTitle,
                CloseButtonText = null,
                PrimaryButtonText = null,
                SecondaryButtonText = null,
                DefaultButton = ContentDialogButton.Primary,
                XamlRoot = parentUI!.XamlRoot
            };

            Grid mainGrid = new Grid { Width = 500d };
            mainGrid.AddGridColumns(2, new GridLength(1.0d, GridUnitType.Star));
            mainGrid.AddGridRows(1, new GridLength(1.0d, GridUnitType.Auto));
            mainGrid.AddGridRows(3, new GridLength(20d, GridUnitType.Pixel));

            // Build path indicator
            StackPanel pathActivityPanel = mainGrid.AddElementToGridRowColumn(
                new StackPanel { Margin = new Thickness(0, 0, 0, 8d) },
                0, 0, 0, 2
                );
            _ = pathActivityPanel.AddElementToStackPanel(
                new TextBlock
                {
                    FontWeight = FontWeights.Bold,
                    Text = Locale.Lang!._FileMigrationProcess!.PathActivityPanelTitle
                });
            TextBlock pathActivitySubtitle = pathActivityPanel.AddElementToStackPanel(
                new TextBlock {
                    Text = Locale.Lang._Misc!.Idle,
                    FontSize = 18d,
                    TextTrimming = TextTrimming.CharacterEllipsis
                });

            // Build speed indicator
            TextBlock speedIndicator = mainGrid.AddElementToGridRow(
                new TextBlock { FontWeight = FontWeights.Bold },
                1);
            Run speedIndicatorTitle = new Run { Text = Locale.Lang._FileMigrationProcess.SpeedIndicatorTitle, FontWeight = FontWeights.Medium };
            Run speedIndicatorSubtitle = new Run { Text = "-" };
            speedIndicator!.Inlines!.Add(speedIndicatorTitle);
            speedIndicator!.Inlines!.Add(speedIndicatorSubtitle);

            // Build file count indicator
            TextBlock fileCountIndicator = mainGrid.AddElementToGridRow(
                new TextBlock { FontWeight = FontWeights.Bold },
                2);
            Run fileCountIndicatorTitle = new Run { Text = Locale.Lang._FileMigrationProcess.FileCountIndicatorTitle, FontWeight = FontWeights.Medium };
            Run fileCountIndicatorSubtitle = new Run { Text = Locale.Lang._Misc.PerFromToPlaceholder };
            fileCountIndicator!.Inlines!.Add(fileCountIndicatorTitle);
            fileCountIndicator!.Inlines!.Add(fileCountIndicatorSubtitle);

            // Build file size indicator
            TextBlock fileSizeIndicator = mainGrid.AddElementToGridRowColumn(
                new TextBlock
                {
                    FontWeight = FontWeights.Bold,
                    HorizontalAlignment = HorizontalAlignment.Right,
                    HorizontalTextAlignment = TextAlignment.Right
                },
                1, 1);
            Run fileSizeIndicatorSubtitle = new Run { Text = Locale.Lang._Misc.PerFromToPlaceholder };
            fileSizeIndicator!.Inlines!.Add(fileSizeIndicatorSubtitle);

            // Build progress percentage indicator
            StackPanel progressTextIndicator = mainGrid.AddElementToGridRowColumn(
                new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    HorizontalAlignment = HorizontalAlignment.Right
                },
                2, 1);
            TextBlock progressTextIndicatorSubtitle = progressTextIndicator.AddElementToStackPanel(
                new TextBlock { Text = "0", FontWeight = FontWeights.Bold });
            _ = progressTextIndicator.AddElementToStackPanel(
                new TextBlock { Text = "%", FontWeight = FontWeights.Bold });

            // Build progress bar indicator
            ProgressBar progressBarIndicator = mainGrid.AddElementToGridRowColumn(
                new ProgressBar
                {
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                    VerticalAlignment = VerticalAlignment.Bottom,
                    Value = 0d,
                    Maximum = 100d,
                    IsIndeterminate = true
                },
                3, 0, 0, 2);

            // Set progress percentage indicator subtitle with progress bar value
            BindingOperations.SetBinding(progressTextIndicatorSubtitle, TextBlock.TextProperty, new Binding()
            {
                Source = progressBarIndicator,
                Path = new PropertyPath("Value"),
                Mode = BindingMode.OneWay,
                UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged
            });

            // Set the main dialog content and queue the dialog spawn
            mainDialogWindow.Content = mainGrid;
            _ = mainDialogWindow.QueueAndSpawnDialog();

            // Return the migration process UI ref struct 
            return new FileMigrationProcessUIRef
            {
                mainDialogWindow = mainDialogWindow,
                pathActivitySubtitle = pathActivitySubtitle,
                fileCountIndicatorSubtitle = fileCountIndicatorSubtitle,
                fileSizeIndicatorSubtitle = fileSizeIndicatorSubtitle,
                progressBarIndicator = progressBarIndicator,
                speedIndicatorSubtitle = speedIndicatorSubtitle,
            };
        }
    }
}
