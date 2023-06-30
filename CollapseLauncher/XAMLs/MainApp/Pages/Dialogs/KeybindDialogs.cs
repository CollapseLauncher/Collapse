using CommunityToolkit.WinUI.UI;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Windows.System;
using System;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;
using static Hi3Helper.Logger;
using static Hi3Helper.Locale;
using static Hi3Helper.Shared.Region.LauncherConfig;
using static CollapseLauncher.Dialogs.SimpleDialogs;

namespace CollapseLauncher.Dialogs
{
    public static class KeybindDialogs
    {
        public static event EventHandler<int> KeyboardShortcutsEvent;

        private static string colorSchm = Application.Current.RequestedTheme == ApplicationTheme.Dark ? "SystemAccentColorLight2" : "SystemAccentColorDark2";

        public static async Task<ContentDialogResult> Dialog_ShowKeybinds(UIElement Content)
        {
            StackPanel stack = new StackPanel() { Orientation = Orientation.Vertical };

            List<List<string>> keys = KeyList;

            stack.Children.Add(new TextBlock { Text = "General", FontSize = 16, FontWeight = FontWeights.Bold, Margin = new Thickness(0, 8, 0, 2) });
            stack.Children.Add(new MenuFlyoutSeparator() { Margin = new Thickness(0, 8, 0, 8) });
            stack.Children.Add(GenerateShortcutBlock(keys[2], "Open this menu", "It can also be accessed through the App Settings"));
            stack.Children.Add(GenerateShortcutBlock(keys[3], "Go to the Home page", "Instantly travel to the Home page from any page"));
            stack.Children.Add(new MenuFlyoutSeparator() { Margin = new Thickness(0, 10, 0, 8) });

            stack.Children.Add(new TextBlock { Text = "Quick Game/Region change", FontSize = 16, FontWeight = FontWeights.Bold, Margin = new Thickness(0, 16, 0, 2) });
            stack.Children.Add(new TextBlock { Text = "Note: The keybinds follow the selector order", FontSize = 11.5 });

            string gameMod = keys[0][0];
            string regionMod = keys[1][0];

            StackPanel textBlock = new StackPanel() { Orientation = Orientation.Horizontal };
            textBlock.Children.Add(new TextBlock()
            {
                Text = "",
                FontSize = 12,
                VerticalAlignment = VerticalAlignment.Center,
                FontFamily = Application.Current.Resources["FontAwesomeSolid"] as FontFamily,
            });
            textBlock.Children.Add(new TextBlock() { Text = string.Format("Swap {0} and {1}", gameMod, regionMod), Margin = new Thickness(8, 0, 0, 0) });

            Button modifierSwap = new Button()
            {
                Content = textBlock,
                CornerRadius = new CornerRadius(5),
                DataContext = new List<string> { "Ctrl", "1 - X" },
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(0, -30, 0, 0)
            };
            modifierSwap.Click += Swap_Click;
            stack.Children.Add(modifierSwap);

            stack.Children.Add(new MenuFlyoutSeparator() { Margin = new Thickness(0, 8, 0, 8) });
            stack.Children.Add(GenerateShortcutBlock(keys[0], "Change game", string.Format("E.g. {0}+1 leads Honkai Impact 3rd's page (last used region)", gameMod), false));
            stack.Children.Add(GenerateShortcutBlock(keys[1], "Change region", string.Format("E.g. For Genshin Impact, {0}+1 leads to the Global region", regionMod), false));
            stack.Children.Add(new MenuFlyoutSeparator() { Margin = new Thickness(0, 10, 0, 8) });

            return await SpawnDialog(
                    "Keyboard Shortcuts",
                    stack,
                    Content,
                    Lang._Misc.Close,
                    null,
                    null,
                    ContentDialogButton.Primary
                );
        }

        private static Grid GenerateShortcutBlock(List<string> kbKeys, string description, string example = null, bool enableSwapButton = true)
        {
            Grid shortcut = new Grid()
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

            if (example != null)
            {
                shortcutDesc.Children.Add(new TextBlock() { Text = description, FontSize = 14, Margin = new Thickness(5, 2, 0, 1) });
                shortcutDesc.Children.Add(new TextBlock() { Text = example, FontSize = 11, Margin = new Thickness(5, 1, 0, 2) });
            }
            else
            {
                shortcutDesc.Children.Add(new TextBlock() { Text = description, FontSize = 14, Margin = new Thickness(5, 1, 5, 1) });
            }

            shortcut.Children.Add(shortcutDesc);
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
                    DataContext = kbKeys,
                    Margin = new Thickness(0, 0, 5, 0)
                };
                shortcutButtons.Children.Add(shortcutSwap);
                shortcutSwap.Click += Swap_Click;
            }

            foreach (string key in kbKeys)
            {
                shortcutButtons.Children.Add(CreateKeyBoardButton(key));
                shortcutButtons.Children.Add(new TextBlock() { Text = "+", FontWeight = FontWeights.Bold, FontSize = 20, VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Right });
            }

            shortcut.Children.Add(shortcutButtons);
            shortcutButtons.Children.RemoveAt(shortcutButtons.Children.Count - 1);

            Grid.SetColumn(shortcutButtons, 1);

            return shortcut;
        }

        private static Border CreateKeyBoardButton(string key)
        {
            Border keyboxBorder = new Border()
            {
                Height = 42,
                Width = 42,
                Margin = new Thickness(5, 0, 5, 0),
                CornerRadius = new CornerRadius(5),
                Background = new SolidColorBrush((Windows.UI.Color)Application.Current.Resources[colorSchm])
            };

            ThemeShadow ts = new ThemeShadow();
            ts.Receivers.Add(keyboxBorder);
            keyboxBorder.Translation += Shadow48;
            keyboxBorder.Shadow = ts;

            TextBlock keybox = new TextBlock()
            {
                Text = key,
                FontWeight = FontWeights.Medium,
                TextAlignment = TextAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };

            keyboxBorder.Child = keybox;

            return keyboxBorder;
        }

        private static async Task Dialog_SwitchKey(UIElement content, List<string> oldKeys)
        {
            StackPanel mainSwitchKeyContent = new StackPanel()
            {
                Orientation = Orientation.Vertical,
            };
            mainSwitchKeyContent.Children.Add(new TextBlock() { Text = "Input a valid key combination to change the shortcut.", Margin = new Thickness(0, 0, 0, 20) });
            mainSwitchKeyContent.Children.Add(new TextBlock() { Text = string.Join(" + ", oldKeys), FontSize = 18, FontWeight = FontWeights.Bold, HorizontalAlignment = HorizontalAlignment.Center });
            mainSwitchKeyContent.Children.Add(new FontIcon() { Glyph = "", FontSize = 15, FontFamily = Application.Current.Resources["FontAwesomeSolid"] as FontFamily, Margin = new Thickness(0, 10, 0, 10) });
            TextBlock keysPressed = new TextBlock() { Text = "...", FontSize = 18, FontWeight = FontWeights.Bold, HorizontalAlignment = HorizontalAlignment.Center, };
            mainSwitchKeyContent.Children.Add(keysPressed);

            ContentDialog result = new ContentDialog
            {
                Title = "Change Keybind",
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
            result.KeyDown += (e, s) =>
            {
                VirtualKey inputKey = s.Key;
                if (keyCount != 1)
                {
                    keyCount = 0;
                    result.IsPrimaryButtonEnabled = false;
                    switch (s.Key)
                    {
                        case VirtualKey.Control:
                            keyCount++;
                            keysPressed.Text = "Ctrl" + " + ";
                            break;
                        case VirtualKey.Shift:
                            keyCount++;
                            keysPressed.Text = "Shift" + " + ";
                            break;
                        case VirtualKey.Menu:
                            keyCount++;
                            keysPressed.Text = "Alt" + " + ";
                            break;
                    }
                }
            };

            List<string> newKeys = new List<string>();

            result.KeyUp += (e, s) =>
            {
                int keyValue = (int)s.Key;
                if (keyCount == 1 && ((keyValue >= 0x41 && keyValue <= 0x5A) || /*(keyValue >= 0x60 && keyValue <= 0x69) ||*/ keyValue == 9)) // Virtual-Key codes for NumPad, Letters and Tab
                {
                    string keyStr = s.Key.ToString();
                    //if (keyStr.Contains("NumberPad")) keyStr = string.Concat("Num", keyStr.Substring(9, 1));

                    keyCount++;
                    keysPressed.Text += keyStr;

                    newKeys = keysPressed.Text.Split(" + ").ToList();
                    result.IsPrimaryButtonEnabled = KeyList.FindIndex(i => i.Contains(newKeys[0]) && i.Contains(newKeys[1])) == -1;
                }
            };


            if (await result.ShowAsync() == ContentDialogResult.Primary)
            {
                ChangeKeybind(oldKeys, newKeys);
            }
            return;
        }

        private static async void Swap_Click(object sender, RoutedEventArgs e)
        {
            List<string> keys = (List<string>)(sender as Button).DataContext;

            if (keys.Any(x => x.StartsWith("1 - ")))
            {
                SwapKeybind();
                (sender as Button).FindParent<ContentDialog>().Hide();
                await Dialog_ShowKeybinds(sender as UIElement);
            }
            else
            {
                try
                {
                    (sender as Button).FindParent<ContentDialog>().Hide();
                    await Dialog_SwitchKey(sender as UIElement, keys);
                    await Dialog_ShowKeybinds(sender as UIElement);
                }
                catch (Exception ex)
                {
                    LogWriteLine(ex.ToString());
                }

            }
        }

        private static void ChangeKeybind(List<string> oldKeys, List<string> newKeys)
        {
            try
            {
                List<List<string>> keys = KeyList;

                if (keys.FindIndex(i => i.Contains(newKeys[0]) && i.Contains(newKeys[1])) == -1)
                {
                    keys[keys.FindIndex(i => i.Contains(oldKeys[0]) && i.Contains(oldKeys[1]))] = newKeys;
                    KeyList = keys;
                }
                KeyboardShortcutsEvent(null, 1);
            }
            catch (Exception ex)
            {
                LogWriteLine(ex.ToString());
            }
        }

        private static void SwapKeybind()
        {
            try
            {
                List<List<string>> keys = KeyList;

                (keys[1][0], keys[0][0]) = (keys[0][0], keys[1][0]);

                KeyList = keys;
                KeyboardShortcutsEvent(null, 1);
            }
            catch (Exception ex)
            {
                LogWriteLine(ex.ToString());
            }
        }

        public static VirtualKey StrToVKey(string key)
        {
            if (key.Contains("Num")) key = "NumberPad" + key[3];
            return (VirtualKey)Enum.Parse(typeof(VirtualKey), key);
        }

        public static VirtualKeyModifiers StrToVKeyModifier(string key)
        {
            switch (key)
            {
                case "Ctrl":
                    key = "Control";
                    break;
                case "Alt":
                    key = "Menu";
                    break;
            }

            return (VirtualKeyModifiers)Enum.Parse(typeof(VirtualKeyModifiers), key);
        }

        private readonly static List<List<string>> defaultKeyList = new List<List<string>>
                {
                    new List<string> { "Ctrl", "1 - 3" },
                    new List<string> { "Shift", "1 - 6" },
                    new List<string> { "Ctrl", "Tab" },
                    new List<string> { "Ctrl", "H" }
                };

        public static List<List<string>> KeyList
        {
            get
            {
                string keyListStr = GetAppConfigValue("KbShortcutList").ToString() ?? null;
                if (keyListStr == null) return defaultKeyList;

                List<List<string>> resultList = new List<List<string>>();
                foreach (string combination in keyListStr.Split('|'))
                {
                    resultList.Add(combination.Split(",").ToList());
                }

                if (resultList.Count < defaultKeyList.Count)
                {
                    resultList.InsertRange(resultList.Count, defaultKeyList.GetRange(resultList.Count, defaultKeyList.Count - resultList.Count));
                }

                for (int i = resultList.Count; i > defaultKeyList.Count; i--)
                {
                    resultList.RemoveAt(i);
                }

                return resultList;
            }

            set
            {
                value ??= defaultKeyList;
                string res = "";
                foreach (List<string> key in value) res = res + string.Join(",", key.ToArray()) + "|";
                LogWriteLine("Keybinds list was updated to: " + res.Remove(res.Length - 1));
                SetAndSaveConfigValue("KbShortcutList", res.Remove(res.Length - 1));
            }
        }
    }
}