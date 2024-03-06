using CollapseLauncher.CustomControls;
using CommunityToolkit.WinUI;
using Hi3Helper.Preset;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Windows.System;
using static CollapseLauncher.Dialogs.SimpleDialogs;
using static Hi3Helper.Locale;
using static Hi3Helper.Logger;
using static Hi3Helper.Shared.Region.LauncherConfig;

namespace CollapseLauncher.Dialogs
{
    public static class KeyboardShortcuts
    {
        #region Properties
        public static event EventHandler<int> KeyboardShortcutsEvent;
        private static int _pageNum;
        private static int _oldSender;
        private static int _buttonWidth;
        public static bool CannotUseKbShortcuts { get; set; } = false;
        #endregion

        #region Show Shortcuts ContentDialog
        public static async Task<ContentDialogResult> Dialog_ShowKbShortcuts(UIElement Content, int page = 0)
        {
            _buttonWidth = int.Max(Lang._KbShortcuts.GeneralTab.Length * 5
                          + Lang._KbShortcuts.SwitchTab.Length * 5
                          + Lang._KbShortcuts.GameFolderTab.Length * 5
                          + Lang._KbShortcuts.GameManagementTab.Length * 5
                          + 5 * 50, 400);

            int swapButtonWidth = Lang._KbShortcuts.Switch_SwapBtn.Length * 5 + 2 * 50;

            StackPanel mainStack = new StackPanel() { Orientation = Orientation.Vertical, Margin = new Thickness(0, 0, 0, 0) };

            StackPanel mainStackContent = new StackPanel() { Orientation = Orientation.Vertical };

            _oldSender = page;

            // General shortcuts
            StackPanel genStack = new StackPanel() { Orientation = Orientation.Vertical, Visibility = Visibility.Collapsed };
            genStack.Children.Add(new TextBlock { Text = Lang._KbShortcuts.General_Title, FontSize = 16, FontWeight = FontWeights.Bold, Margin = new Thickness(0, 0, 0, 2) });
            genStack.Children.Add(new MenuFlyoutSeparator() { Margin = new Thickness(0, 8, 0, 8) });
            genStack.Children.Add(GenerateShortcutBlock("KbShortcutsMenu", KbShortcutList["KbShortcutsMenu"], Lang._KbShortcuts.General_OpenMenu, Lang._KbShortcuts.General_OpenMenu_Desc));
            genStack.Children.Add(GenerateShortcutBlock("HomePage", KbShortcutList["HomePage"], Lang._KbShortcuts.General_GoHome));
            genStack.Children.Add(GenerateShortcutBlock("SettingsPage", KbShortcutList["SettingsPage"], Lang._KbShortcuts.General_GoSettings));
            genStack.Children.Add(GenerateShortcutBlock("NotificationPanel", KbShortcutList["NotificationPanel"], Lang._KbShortcuts.General_OpenNotifTray));
            genStack.Children.Add(GenerateShortcutBlock("ReloadRegion", KbShortcutList["ReloadRegion"], Lang._KbShortcuts.General_ReloadRegion, Lang._KbShortcuts.General_ReloadRegion_Desc));
            genStack.Children.Add(new MenuFlyoutSeparator() { Margin = new Thickness(0, 10, 0, 8) });
            _pageNum++;

            // Region/Game Shortcuts
            StackPanel changeStack = new StackPanel() { Orientation = Orientation.Vertical, Visibility = Visibility.Collapsed };
            Grid changeTitleGrid = new Grid()
            {
                Margin = new Thickness(0, 0, 0, 0),
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                ColumnSpacing = 5,
                ColumnDefinitions = { new ColumnDefinition() { Width = GridLength.Auto }, new ColumnDefinition() }
            };
            StackPanel changeTitleStack = new StackPanel() { Orientation = Orientation.Vertical };
            changeTitleGrid.Children.Add(changeTitleStack);
            Grid.SetColumn(changeTitleStack, 0);
            changeTitleStack.Children.Add(new TextBlock { Text = Lang._KbShortcuts.Switch_Title, FontSize = 16, FontWeight = FontWeights.Bold, Margin = new Thickness(0, 0, 0, 2) });
            changeTitleStack.Children.Add(new TextBlock { Text = Lang._KbShortcuts.Switch_Subtitle, FontSize = 11.5, TextWrapping = TextWrapping.Wrap, MaxWidth = _buttonWidth - swapButtonWidth });

            string gameMod = KbShortcutList["GameSelection"].GetFormattedModifier();
            string regionMod = KbShortcutList["RegionSelection"].GetFormattedModifier();

            StackPanel textBlock = new StackPanel() { Orientation = Orientation.Horizontal };
            textBlock.Children.Add(new TextBlock()
            {
                Text = "",
                FontSize = 12,
                VerticalAlignment = VerticalAlignment.Center,
                FontFamily = Application.Current.Resources["FontAwesomeSolid"] as FontFamily,
            });
            textBlock.Children.Add(new TextBlock() { Text = string.Format(Lang._KbShortcuts.Switch_SwapBtn, gameMod, regionMod), Margin = new Thickness(8, 0, 0, 0) });

            Button modifierSwap = new Button()
            {
                Content = textBlock,
                CornerRadius = new CornerRadius(5),
                DataContext = new KbShortcutChangeData { KeyName = "GameSelection", PageNumber = 1 },
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(0, 0, 2, 0)
            };
            modifierSwap.Click += Swap_Click;
            changeTitleGrid.Children.Add(modifierSwap);
            Grid.SetColumn(modifierSwap, 1);
            changeStack.Children.Add(changeTitleGrid);

            changeStack.Children.Add(new MenuFlyoutSeparator() { Margin = new Thickness(0, 8, 0, 8) });
            changeStack.Children.Add(GenerateShortcutBlock("GameSelection", KbShortcutList["GameSelection"], Lang._KbShortcuts.Switch_ChangeGame, string.Format(Lang._KbShortcuts.Switch_ChangeGame_Desc, gameMod), false));
            changeStack.Children.Add(GenerateShortcutBlock("RegionSelection", KbShortcutList["RegionSelection"], Lang._KbShortcuts.Switch_ChangeRegion, string.Format(Lang._KbShortcuts.Switch_ChangeRegion_Desc, regionMod), false));
            changeStack.Children.Add(new MenuFlyoutSeparator() { Margin = new Thickness(0, 10, 0, 8) });
            _pageNum++;

            // Game folder
            StackPanel gameFolderStack = new StackPanel() { Orientation = Orientation.Vertical, Visibility = Visibility.Collapsed };
            gameFolderStack.Children.Add(new TextBlock { Text = Lang._KbShortcuts.GameFolder_Title, FontSize = 16, FontWeight = FontWeights.Bold, Margin = new Thickness(0, 0, 0, 2) });
            gameFolderStack.Children.Add(new MenuFlyoutSeparator() { Margin = new Thickness(0, 8, 0, 8) });
            gameFolderStack.Children.Add(GenerateShortcutBlock("ScreenshotFolder", KbShortcutList["ScreenshotFolder"], Lang._KbShortcuts.GameFolder_ScreenshotFolder));
            gameFolderStack.Children.Add(GenerateShortcutBlock("GameFolder", KbShortcutList["GameFolder"], Lang._KbShortcuts.GameFolder_MainFolder));
            gameFolderStack.Children.Add(GenerateShortcutBlock("CacheFolder", KbShortcutList["CacheFolder"], Lang._KbShortcuts.GameFolder_CacheFolder));
            gameFolderStack.Children.Add(new MenuFlyoutSeparator() { Margin = new Thickness(0, 10, 0, 8) });
            _pageNum++;

            // Game management
            StackPanel gameManageStack = new StackPanel() { Orientation = Orientation.Vertical, Visibility = Visibility.Collapsed };
            gameManageStack.Children.Add(new TextBlock { Text = Lang._KbShortcuts.GameManagement_Title, FontSize = 16, FontWeight = FontWeights.Bold, Margin = new Thickness(0, 0, 0, 2) });
            gameManageStack.Children.Add(new TextBlock { Text = Lang._KbShortcuts.GameManagement_Subtitle, FontSize = 11.5, TextWrapping = TextWrapping.Wrap });
            gameManageStack.Children.Add(new MenuFlyoutSeparator() { Margin = new Thickness(0, 8, 0, 8) });
            gameManageStack.Children.Add(GenerateShortcutBlock("ForceCloseGame", KbShortcutList["ForceCloseGame"], Lang._KbShortcuts.GameManagement_ForceCloseGame, Lang._KbShortcuts.GameManagement_ForceCloseGame_Desc));
            gameManageStack.Children.Add(GenerateShortcutBlock("RepairPage", KbShortcutList["RepairPage"], Lang._KbShortcuts.GameManagement_GoRepair));
            gameManageStack.Children.Add(GenerateShortcutBlock("GameSettingsPage", KbShortcutList["GameSettingsPage"], Lang._KbShortcuts.GameManagement_GoSettings));
            gameManageStack.Children.Add(GenerateShortcutBlock("CachesPage", KbShortcutList["CachesPage"], Lang._KbShortcuts.GameManagement_GoCaches));
            gameManageStack.Children.Add(new MenuFlyoutSeparator() { Margin = new Thickness(0, 10, 0, 8) });
            _pageNum = 0;

            StackPanel buttonStack = new StackPanel() { HorizontalAlignment = HorizontalAlignment.Center, Orientation = Orientation.Horizontal, Margin = new Thickness(0, 10, 0, 0) };

            Button genButton = new Button() { DataContext = 0, Content = new TextBlock() { Text = Lang._KbShortcuts.GeneralTab, Margin = new Thickness(5, 0, 5, 0), FontWeight = FontWeights.SemiBold } };
            Button changeButton = new Button() { DataContext = 1, Content = new TextBlock() { Text = Lang._KbShortcuts.SwitchTab, Margin = new Thickness(5, 0, 5, 0), FontWeight = FontWeights.SemiBold } };
            Button gameFolderButton = new Button() { DataContext = 2, Content = new TextBlock() { Text = Lang._KbShortcuts.GameFolderTab, Margin = new Thickness(5, 0, 5, 0), FontWeight = FontWeights.SemiBold } };
            Button gameManagerButton = new Button() { DataContext = 3, Content = new TextBlock() { Text = Lang._KbShortcuts.GameManagementTab, Margin = new Thickness(5, 0, 5, 0), FontWeight = FontWeights.SemiBold } };

            List<object> stacks = new List<object>() { genStack, changeStack, gameFolderStack, gameManageStack };
            List<object> buttons = new List<object>() { genButton, changeButton, gameFolderButton, gameManagerButton };

            foreach (Button button in buttons)
            {
                button.Click += (o, e) => { ChangeMenuVisibility((int)((Button)o).DataContext, stacks, buttons); };
                button.Margin = new Thickness(3, 0, 3, 0);
                button.CornerRadius = new CornerRadius(15);
                buttonStack.Children.Add(button);
            }

            mainStack.Children.Add(mainStackContent);
            mainStack.Children.Add(buttonStack);

            foreach (StackPanel stk in stacks)
            {
                stk.Width = _buttonWidth;
                mainStackContent.Children.Add(stk);
            }
            ChangeMenuVisibility(page, stacks, buttons);

            return await SpawnDialog(
                    Lang._KbShortcuts.DialogTitle,
                    mainStack,
                    Content,
                    Lang._Misc.Close
                );
        }

        private static Grid GenerateShortcutBlock(string keyName, KbShortcut shortcut, string description, string example = null, bool enableSwapButton = true)
        {
            Grid shortcutGrid = new Grid()
            {
                Margin = new Thickness(0, 8, 0, 8),
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                ColumnSpacing = 5,
                ColumnDefinitions = { new ColumnDefinition() { Width = GridLength.Auto }, new ColumnDefinition() }
            };

            StackPanel shortcutDesc = new StackPanel()
            {
                Margin = new Thickness(0, 1, 5, 1),
                HorizontalAlignment = HorizontalAlignment.Left,
                Orientation = Orientation.Vertical,
                VerticalAlignment = VerticalAlignment.Center
            };

            int maxLen = enableSwapButton ? _buttonWidth - 180 : _buttonWidth - 150;

            if (example != null)
            {
                shortcutDesc.Children.Add(new TextBlock() { Text = description, FontSize = 14, Margin = new Thickness(5, 2, 0, 1), TextWrapping = TextWrapping.Wrap, MaxWidth = maxLen });
                shortcutDesc.Children.Add(new TextBlock() { Text = example, FontSize = 11, Margin = new Thickness(5, 1, 0, 2), TextWrapping = TextWrapping.Wrap, MaxWidth = maxLen });
            }
            else
            {
                shortcutDesc.Children.Add(new TextBlock() { Text = description, FontSize = 14, Margin = new Thickness(5, 1, 5, 1), TextWrapping = TextWrapping.Wrap, MaxWidth = maxLen });
            }

            shortcutGrid.Children.Add(shortcutDesc);
            Grid.SetColumn(shortcutDesc, 0);

            StackPanel shortcutButtons = new StackPanel()
            {
                HorizontalAlignment = HorizontalAlignment.Right,
                Orientation = Orientation.Horizontal
            };


            if (enableSwapButton)
            {
                Button shortcutSwap = new Button()
                {
                    Content = new TextBlock() { Text = "", FontSize = 12, Margin = new Thickness(-5, 0, -5, 0), FontFamily = Application.Current.Resources["FontAwesomeSolid"] as FontFamily },
                    CornerRadius = new CornerRadius(5),
                    DataContext = new KbShortcutChangeData { Description = description, PageNumber = _pageNum, KeyName = keyName, Shortcut = shortcut },
                    Margin = new Thickness(0, 0, 5, 0)
                };
                shortcutButtons.Children.Add(shortcutSwap);
                shortcutSwap.Click += Swap_Click;
            }

            if (shortcut.Modifier != VirtualKeyModifiers.None)
            {
                shortcutButtons.Children.Add(CreateKeyBoardButton(shortcut.GetFormattedModifier()));
                shortcutButtons.Children.Add(new TextBlock()
                {
                    Text = "+",
                    FontWeight = FontWeights.Bold,
                    FontSize = 20,
                    VerticalAlignment = VerticalAlignment.Center,
                    HorizontalAlignment = HorizontalAlignment.Right
                });
            }

            shortcutButtons.Children.Add(CreateKeyBoardButton(shortcut.GetKey(keyName)));
            shortcutGrid.Children.Add(shortcutButtons);

            Grid.SetColumn(shortcutButtons, 1);

            return shortcutGrid;
        }

        private static Border CreateKeyBoardButton(string key)
        {
            Border keyboxBorder = new Border()
            {
                Height = 42,
                Width = 42,
                Margin = new Thickness(5, 0, 5, 0),
                CornerRadius = new CornerRadius(5),
                Background = Application.Current.Resources["SystemFillColorAttentionBrush"] as SolidColorBrush
            };

            ThemeShadow ts = new ThemeShadow();
            ts.Receivers.Add(keyboxBorder);
            keyboxBorder.Translation += Shadow48;
            keyboxBorder.Shadow = ts;

            TextBlock keybox = new TextBlock()
            {
                Text = key,
                Foreground = new SolidColorBrush(Application.Current.RequestedTheme == ApplicationTheme.Dark ? Microsoft.UI.Colors.Black : Microsoft.UI.Colors.White),
                FontWeight = FontWeights.Medium,
                TextAlignment = TextAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };

            keyboxBorder.Child = keybox;

            return keyboxBorder;
        }

        private static async void ChangeMenuVisibility(int sender, List<object> stacks, List<object> buttons)
        {
            try
            {
                (buttons[_oldSender] as Button).Style = Application.Current.Resources["DefaultButtonStyle"] as Style; ;
                StackPanel oldStack = stacks[_oldSender] as StackPanel;
                (buttons[sender] as Button).Style = Application.Current.Resources["AccentButtonStyle"] as Style;
                StackPanel newStack = stacks[sender] as StackPanel;

                if (sender == _oldSender)
                {
                    oldStack.Visibility = Visibility.Collapsed;
                    newStack.Visibility = Visibility.Visible;
                    return;
                }

                _oldSender = sender;

                Storyboard storyboard = new Storyboard();
                DoubleAnimation OpacityAnimation = new DoubleAnimation();
                OpacityAnimation.From = 1;
                OpacityAnimation.To = 0;
                OpacityAnimation.Duration = new Duration(TimeSpan.FromSeconds(0.2));

                Storyboard.SetTarget(OpacityAnimation, oldStack);
                Storyboard.SetTargetProperty(OpacityAnimation, "Opacity");
                storyboard.Children.Add(OpacityAnimation);

                storyboard.Begin();

                await Task.Delay(200);
                newStack.Visibility = Visibility.Visible;
                oldStack.Visibility = Visibility.Collapsed;

                Storyboard storyboard2 = new Storyboard();
                DoubleAnimation OpacityAnimation2 = new DoubleAnimation();
                OpacityAnimation2.From = 0;
                OpacityAnimation2.To = 1;
                OpacityAnimation2.Duration = new Duration(TimeSpan.FromSeconds(0.2));

                Storyboard.SetTarget(OpacityAnimation2, newStack);
                Storyboard.SetTargetProperty(OpacityAnimation2, "Opacity");
                storyboard2.Children.Add(OpacityAnimation2);

                storyboard2.Begin();
            }
            catch (Exception e)
            {
                LogWriteLine(e.ToString());
            }
        }
        #endregion

        #region Change Key Combinations
        private static async Task Dialog_SwitchKey(UIElement content, KbShortcutChangeData data)
        {
            StackPanel mainSwitchKeyContent = new StackPanel()
            {
                Orientation = Orientation.Vertical,
                MinWidth = 350,
                MaxWidth = 600
            };

            StackPanel HelpStack = new StackPanel() { Orientation = Orientation.Vertical, MaxWidth = 360 };
            Flyout HelpFlyout = new Flyout() { Content = HelpStack, Placement = Microsoft.UI.Xaml.Controls.Primitives.FlyoutPlacementMode.RightEdgeAlignedTop };
            HelpStack.Children.Add(new TextBlock()
            {
                Text = Lang._KbShortcuts.ChangeShortcut_Help1,
                Margin = new Thickness(0, 2, 0, 4),
                TextWrapping = TextWrapping.Wrap
            });
            HelpStack.Children.Add(new TextBlock()
            {
                Text = Lang._KbShortcuts.ChangeShortcut_Help2,
                Margin = new Thickness(5, 4, 0, 4)
            });
            HelpStack.Children.Add(new TextBlock()
            {
                Text = Lang._KbShortcuts.ChangeShortcut_Help3,
                Margin = new Thickness(5, 4, 0, 8)
            });
            HelpStack.Children.Add(new TextBlock()
            {
                Text = Lang._KbShortcuts.ChangeShortcut_Help4,
                Margin = new Thickness(0, 4, 0, 0),
                TextWrapping = TextWrapping.Wrap
            });

            StackPanel introPanel = new StackPanel() { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, -7, 0, 0) };
            introPanel.Children.Add(new TextBlock()
            {
                Text = Lang._KbShortcuts.ChangeShortcut_Text,
                Margin = new Thickness(0, 0, 0, 2),
                TextWrapping = TextWrapping.Wrap,
                HorizontalAlignment = HorizontalAlignment.Center
            });

            Button HelpButton = new Button()
            {
                Content = new TextBlock() { Text = "info", FontFamily = Application.Current.Resources["FontAwesomeSolid"] as FontFamily, FontSize = 10 },
                Flyout = HelpFlyout,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(8, -2, 0, 2)
            };
            introPanel.Children.Add(HelpButton);
            mainSwitchKeyContent.Children.Add(introPanel);

            mainSwitchKeyContent.Children.Add(new TextBlock()
            {
                Text = data.Description,
                Margin = new Thickness(5, 6, 0, 10),
                TextWrapping = TextWrapping.Wrap,
                HorizontalAlignment = HorizontalAlignment.Center,
                FontWeight = FontWeights.Bold
            });

            StackPanel keysPanel = new StackPanel()
            {
                Orientation = Orientation.Horizontal,
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 8, 0, -5)
            };

            keysPanel.Children.Add(CreateKeyBoardButton(data.Shortcut.GetFormattedModifier()));
            keysPanel.Children.Add(new TextBlock() { Text = "+", FontSize = 20, FontWeight = FontWeights.Bold, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center });
            keysPanel.Children.Add(CreateKeyBoardButton(data.Shortcut.GetKey(data.KeyName)));
            keysPanel.Children.Add(new FontIcon() { Glyph = "arrow-right", FontSize = 15, FontFamily = Application.Current.Resources["FontAwesomeSolid"] as FontFamily, Margin = new Thickness(10, 0, 10, 0) });
            Border newKey1 = CreateKeyBoardButton("?");
            keysPanel.Children.Add(newKey1);
            keysPanel.Children.Add(new TextBlock() { Text = "+", FontSize = 20, FontWeight = FontWeights.Bold, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center });
            Border newKey2 = CreateKeyBoardButton("?");
            keysPanel.Children.Add(newKey2);
            mainSwitchKeyContent.Children.Add(keysPanel);

            ContentDialogCollapse result = new ContentDialogCollapse(ContentDialogTheme.Informational)
            {
                Title = Lang._KbShortcuts.ChangeShortcut_Title,
                Content = mainSwitchKeyContent,
                CloseButtonText = Lang._Misc.Cancel,
                PrimaryButtonText = Lang._Misc.Change,
                SecondaryButtonText = null,
                IsPrimaryButtonEnabled = false,
                DefaultButton = ContentDialogButton.Primary,
                Background = (Brush)Application.Current.Resources["DialogAcrylicBrush"],
                XamlRoot = content.XamlRoot
            };

            int keyCount = 0;
            TextBlock text1 = newKey1.Child as TextBlock;
            TextBlock text2 = newKey2.Child as TextBlock;
            KbShortcut newShortcut = new KbShortcut();

            result.KeyDown += (e, s) =>
            {
                VirtualKey inputKey = s.Key;

                result.IsPrimaryButtonEnabled = false;
                keyCount = 1;
                text2.Text = "?";
                switch (inputKey)
                {
                    case VirtualKey.Control:
                        newShortcut.Modifier = VirtualKeyModifiers.Control;
                        break;
                    case VirtualKey.Shift:
                        newShortcut.Modifier = VirtualKeyModifiers.Shift;
                        break;
                    case VirtualKey.Menu:
                        newShortcut.Modifier = VirtualKeyModifiers.Menu;
                        break;
                    default:
                        keyCount = 0;
                        break;
                };
                text1.Text = keyCount != 0 ? newShortcut.GetFormattedModifier() : "?";
            };

            result.KeyUp += (e, s) =>
            {
                int keyValue = (int)s.Key;

                if (keyCount >= 1 && keyValue is (>= 0x41 and <= 0x5A) or 9) // Virtual-Key codes for Letters and Tab
                {
                    newShortcut.Key = s.Key;
                    text2.Text = s.Key.ToString();

                    result.IsPrimaryButtonEnabled = ValidKeyCombination(newShortcut);
                }
            };

            CannotUseKbShortcuts = true;
            if (await result.QueueAndSpawnDialog() == ContentDialogResult.Primary)
                ChangeShortcut(data.KeyName, newShortcut);
            CannotUseKbShortcuts = false;
        }

        private static async void Swap_Click(object sender, RoutedEventArgs e)
        {
            KbShortcutChangeData data = (KbShortcutChangeData)(sender as Button).DataContext;

            if (data.KeyName is "GameSelection" or "RegionSelection")
            {
                SwapModifiers();
                (sender as Button).FindParent<ContentDialogCollapse>().Hide();
                await Task.Delay(200);
                await Dialog_ShowKbShortcuts(sender as UIElement, 1);
            }
            else
            {
                try
                {
                    (sender as Button).FindParent<ContentDialogCollapse>().Hide();
                    await Task.Delay(200);
                    await Dialog_SwitchKey(sender as UIElement, data);
                    await Dialog_ShowKbShortcuts(sender as UIElement, data.PageNumber);
                }
                catch (Exception ex)
                {
                    LogWriteLine(ex.ToString());
                }
            }
        }

        private static void ChangeShortcut(string key, KbShortcut newShortcut)
        {
            if (!KbShortcutList.TryGetValue(key, out KbShortcut oldShortcut))
            {
                oldShortcut = new KbShortcut() { Key = VirtualKey.None, Modifier = VirtualKeyModifiers.None };
            }

            KbShortcutList[key] = newShortcut;

            LogWriteLine($"[KeyboardShortcuts::ChangeKeybind] Swapped {oldShortcut} with {newShortcut} for the key {key}.");
            SaveKbShortcuts();
            KeyboardShortcutsEvent(null, 1);
        }

        // Swaps the modifiers for the shortcuts related to changing game/region.
        private static void SwapModifiers()
        {
            (KbShortcutList["GameSelection"], KbShortcutList["RegionSelection"]) = (KbShortcutList["RegionSelection"], KbShortcutList["GameSelection"]);

            SaveKbShortcuts();
            KeyboardShortcutsEvent(null, 1);
        }

        public static void ResetKeyboardShortcuts()
        {
            KbShortcutList = null;
            SaveKbShortcuts();
            LoadKbShortcuts();
        }

        private static bool ValidKeyCombination(KbShortcut shortcut)
        {
            return !KbShortcutList.Any(x => x.Value.Key == shortcut.Key && x.Value.Modifier == shortcut.Modifier)
                   && !ForbiddenShortcutList.Any(x => x.Key == shortcut.Key && x.Modifier == shortcut.Modifier);
        }
        #endregion

        #region Default/Forbidden Keyboard Shortcuts
        public static readonly Dictionary<string, KbShortcut> DefaultShortcutList = new Dictionary<string, KbShortcut>()
        {
            { "GameSelection", new KbShortcut { Modifier = VirtualKeyModifiers.Control, Key = VirtualKey.None } },
            { "RegionSelection", new KbShortcut { Modifier = VirtualKeyModifiers.Shift, Key = VirtualKey.None } },

            { "KbShortcutsMenu", new KbShortcut { Modifier = VirtualKeyModifiers.Control, Key = VirtualKey.Tab } },
            { "HomePage", new KbShortcut { Modifier = VirtualKeyModifiers.Control, Key = VirtualKey.H } },
            { "SettingsPage", new KbShortcut { Modifier = VirtualKeyModifiers.Control, Key = VirtualKey.S } },
            { "NotificationPanel", new KbShortcut { Modifier = VirtualKeyModifiers.Control, Key = VirtualKey.Q } },

            { "ScreenshotFolder", new KbShortcut { Modifier = VirtualKeyModifiers.Shift, Key = VirtualKey.X } },
            { "GameFolder", new KbShortcut { Modifier = VirtualKeyModifiers.Shift, Key = VirtualKey.F } },
            { "CacheFolder", new KbShortcut { Modifier = VirtualKeyModifiers.Shift, Key = VirtualKey.G } },
            { "ForceCloseGame", new KbShortcut { Modifier = VirtualKeyModifiers.Shift, Key = VirtualKey.E } },

            { "RepairPage", new KbShortcut { Modifier = VirtualKeyModifiers.Shift, Key = VirtualKey.R } },
            { "GameSettingsPage", new KbShortcut { Modifier = VirtualKeyModifiers.Shift, Key = VirtualKey.S } },
            { "CachesPage", new KbShortcut { Modifier = VirtualKeyModifiers.Shift, Key = VirtualKey.C } },

            { "ReloadRegion", new KbShortcut { Modifier = VirtualKeyModifiers.Control, Key = VirtualKey.R } }
        };

        private static readonly List<KbShortcut> ForbiddenShortcutList =
        [
            new KbShortcut { Modifier = VirtualKeyModifiers.Control, Key = VirtualKey.A },
            new KbShortcut { Modifier = VirtualKeyModifiers.Control, Key = VirtualKey.X },
            new KbShortcut { Modifier = VirtualKeyModifiers.Control, Key = VirtualKey.C },
            new KbShortcut { Modifier = VirtualKeyModifiers.Control, Key = VirtualKey.V }
        ];
        #endregion

        #region Custom Keyboard Shortcuts
        public static Dictionary<string, KbShortcut> KbShortcutList = new Dictionary<string, KbShortcut>();

        public static void LoadKbShortcuts()
        {
            bool saveAfterLoad = false;
            string keyListStr = GetAppConfigValue("KbShortcutList").ToString();
            LogWriteLine("[KeyboardShortcuts] The following configuration is gonna be loaded from the .ini file:\n\r\t" + keyListStr);

            Dictionary<string, KbShortcut> resultList = new Dictionary<string, KbShortcut>();

            if (keyListStr == null)
            {
                foreach (var entry in DefaultShortcutList)
                {
                    resultList.Add(entry.Key, entry.Value);
                }

                KbShortcutList = resultList;
                return;
            }

            if (!keyListStr.Contains(':'))
            {
                saveAfterLoad = true;
                LogWriteLine($"[KeyboardShortcuts::LoadKbShortcuts] Detected old KbShortcutList! Converting...\n\r\tOld Value:\"{keyListStr}\"",
                    writeToLog: true);
                var keyList = DefaultShortcutList.Keys;
                int index = 0;
                foreach (string combination in keyListStr.Split('|'))
                {
                    resultList.Add(keyList.ElementAt(index), KbShortcut.FromOldKeyList(combination.Split(",")));
                    index++;
                }
            }
            else
            {
                foreach (string combination in keyListStr.Split('|'))
                {
                    string[] values = combination.Split(":");
                    resultList.Add(values[0], KbShortcut.FromCode(values[1]));
                }
            }

            var missingKeys = DefaultShortcutList.Keys.Except(resultList.Keys);
            var deprecatedKeys = resultList.Keys.Except(DefaultShortcutList.Keys);

            // ReSharper disable PossibleMultipleEnumeration
            saveAfterLoad = saveAfterLoad || missingKeys.Any() || deprecatedKeys.Any();

            foreach (string key in missingKeys)
            {
                resultList.Add(key, DefaultShortcutList[key]);
            }

            foreach (string key in deprecatedKeys)
            {
                resultList.Remove(key);
            }

            KbShortcutList = resultList;

            if (saveAfterLoad)
                SaveKbShortcuts();
        }

        public static void SaveKbShortcuts()
        {
            KbShortcutList ??= DefaultShortcutList;
            string res = "";
            foreach (var entry in KbShortcutList)
            {
                res += $"{entry.Key}:{entry.Value.ToCode()}|";
            }
            res = res.Remove(res.Length - 1);
            LogWriteLine("[KeyboardShortcuts::SaveKbShortcuts] The following configuration was saved:\n\r\t" + res);
            SetAndSaveConfigValue("KbShortcutList", res);
        }
        #endregion

        #region KbShortcut class
        public class KbShortcut
        {
            public VirtualKeyModifiers Modifier { get; set; } = VirtualKeyModifiers.None;
            public VirtualKey Key { get; set; } = VirtualKey.None;

            public string GetFormattedModifier()
            {
                return Modifier switch
                {
                    VirtualKeyModifiers.Control => Lang._KbShortcuts.Keyboard_Control,
                    VirtualKeyModifiers.Menu => Lang._KbShortcuts.Keyboard_Menu,
                    VirtualKeyModifiers.Shift => Lang._KbShortcuts.Keyboard_Shift,
                    _ => Modifier.ToString(),
                };
            }

            public string GetKey(string dictionaryKey = "")
            {
                return dictionaryKey switch
                {
                    "GameSelection" => $"1 - {ConfigV2Store.ConfigV2.GameCount}",
                    "RegionSelection" => $"1 - {ConfigV2Store.ConfigV2.MaxRegionCount}",
                    _ => Key.ToString()
                };
            }

            public string ToCode()
            {
                return (int)Modifier + "," + (int)Key;
            }

            public static KbShortcut FromCode(string code)
            {
                string[] split = code.Split(',');
                return new KbShortcut { Modifier = (VirtualKeyModifiers)int.Parse(split[0]), Key = (VirtualKey)int.Parse(split[1]) };
            }

            public static KbShortcut FromOldKeyList(string[] strings)
            {
                if (strings.Length != 2)
                    return null;

                VirtualKeyModifiers mod = strings[0] switch
                {
                    "Ctrl" => VirtualKeyModifiers.Control,
                    "Shift" => VirtualKeyModifiers.Shift,
                    "Alt" => VirtualKeyModifiers.Menu,
                    _ => (VirtualKeyModifiers)Enum.Parse(typeof(VirtualKeyModifiers), strings[0])
                };

                if (strings[1] is "1 - 3" or "1 - 6")
                    return new KbShortcut { Modifier = mod, Key = VirtualKey.None };

                VirtualKey key = (VirtualKey)Enum.Parse(typeof(VirtualKey), strings[1]);
                return new KbShortcut { Modifier = mod, Key = key };
            }

            public override string ToString()
            {
                return GetFormattedModifier() + "," + GetKey();
            }
        }
        #endregion

        #region Change Keyboard Shortcut struct
        private struct KbShortcutChangeData
        {
            public string KeyName { get; init; }
            public KbShortcut Shortcut { get; init; }
            public string Description { get; init; }
            public int PageNumber { get; init; }
        }
        #endregion
    }
}