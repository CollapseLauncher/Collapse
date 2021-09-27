using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Threading.Tasks;
//using System.Diagnostics;
using Newtonsoft.Json;
using Microsoft.Win32;
using Hi3HelperGUI.Preset;
using static Hi3HelperGUI.Logger;
using static Hi3HelperGUI.Preset.ConfigStore;

namespace Hi3HelperGUI
{
    public partial class MainWindow
    {
        public async void CheckConfigSettings()
        {
            List<PresetConfigClasses> TempConfig = new List<PresetConfigClasses>();
            try
            {
                await Task.Run(() =>
                {
                    string ConfigData = Encoding.UTF8.GetString(File.ReadAllBytes(@"fileconfig.json"));
                    TempConfig = JsonConvert.DeserializeObject<List<PresetConfigClasses>>(ConfigData);
                    if (TempConfig == null)
                        throw new NullReferenceException($"File config is empty!");

                    foreach (PresetConfigClasses i in TempConfig)
                        isConfigAvailable(i);

                    InitMirrorDropdown();
                });
            }
            catch (JsonReaderException e)
            {
                LogWriteLine($"There's something wrong with the configuration file.\r\nTraceback: {e}", LogType.Error, true);
                InstalledClientLabel.Content = "Configuration file is error!";
                InstalledClientLabel.Foreground = System.Windows.Media.Brushes.Red;
                DisableAllFunction();
                return;
            }
            catch (Exception e)
            {
                LogWriteLine($"Unknown error occured while reading configuration file.\r\nTraceback: {e}", LogType.Error, true);
                InstalledClientLabel.Content = "Configuration file is blank!";
                InstalledClientLabel.Foreground = System.Windows.Media.Brushes.Red;
                DisableAllFunction();
                return;
            }
            InstalledClientLabel.Content = GetInstalledClientName();
            return;
        }

        internal void InitMirrorDropdown()
        {
            Dispatcher.Invoke(() =>
            {
                MirrorSelector.ItemsSource = AppConfigData.AvailableMirror;
                MirrorSelector.SelectedIndex = AppConfigData.MirrorSelection;
                MirrorSelectorStatus.Content = MirrorSelector.SelectedItem;
            });
        }

        internal string GetInstalledClientName() => Config.Select(i => i.ZoneName).Aggregate((i, j) => i + ", " + j);
        
        internal static bool isConfigAvailable(PresetConfigClasses i)
        {
            string RegValue = "InstallPath";
            bool ret = true;

            string a;
            try
            {
                a = (string)Registry.GetValue(i.InstallRegistryLocation, RegValue, null);
                if (a == null)
                    throw new NullReferenceException($"Registry for \"{i.ZoneName}\" version doesn't exist, probably the version isn't installed.");

                if (!Directory.Exists(a))
                {
                    if (Directory.Exists(i.DefaultGameLocation))
                    {
                        i.ActualGameLocation = i.DefaultGameLocation;
                        Config.Add(i);
                        LogWriteLine($"Registered path for {i.ZoneName} version doesn't exist. But the default path does exist!", LogType.Warning);
                        return true;
                    }
                    ret = false;
                    throw new DirectoryNotFoundException($"Registry does exist but the registered directory for \"{i.ZoneName}\" version seems to be missing!");
                }
                else
                {
                    i.ActualGameLocation = a;
                    SetLanguageParameter(i);
                    Config.Add(i);
                    LogWriteLine($"\u001b[34;1m{i.ZoneName}\u001b[0m (\u001b[32;1m{Path.GetFileName(i.InstallRegistryLocation)}\u001b[0m) [{i.UsedLanguage}] version is detected!");
                }
            }
            catch (NullReferenceException e)
            {
                LogWriteLine(e.ToString(), Logger.LogType.Warning, true);
            }
            catch (Exception e)
            {
                LogWriteLine(e.ToString(), Logger.LogType.Error, true);
            }

            return ret;
        }

        internal static void SetLanguageParameter(in PresetConfigClasses i) => i.UsedLanguage = GetUsedLanguage(i.ConfigRegistryLocation, "MIHOYOSDK_NOTICE_LANGUAGE_", i.FallbackLanguage);

        internal static string GetUsedLanguage(string RegLocation, string RegValueWildCard, string FallbackValue)
        {
            string value = "";
            try
            {
                RegistryKey keys = Registry.CurrentUser.OpenSubKey(RegLocation);
                foreach (string valueName in keys.GetValueNames())
                    if (valueName.Contains(RegValueWildCard))
                        value = valueName;

                return Encoding.UTF8.GetString((byte[])Registry.GetValue($"HKEY_CURRENT_USER\\{RegLocation}", value, FallbackValue)).Replace("\0", string.Empty);
            }
            catch
            {
                LogWriteLine($"Language setting is not exist. You'll be using {FallbackValue} as value.", LogType.Warning);
                return FallbackValue;
            }
        }
    }
}
