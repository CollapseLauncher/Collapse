using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Microsoft.Win32;
using Hi3Helper;
using Hi3Helper.Preset;
using Hi3Helper.Screen;

using static Hi3Helper.Logger;

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
                    string ConfigData = Encoding.UTF8.GetString(File.ReadAllBytes(Path.Combine("config", "fileconfig.json")));
                    TempConfig = JsonConvert.DeserializeObject<List<PresetConfigClasses>>(ConfigData);
                    if (TempConfig == null)
                        throw new NullReferenceException($"File config is empty!");

                    foreach (PresetConfigClasses i in TempConfig)
                        if (!i.IsSteamVersion)
                            IsConfigAvailable(i);
                        else
                            IsConfigAvailableSteam(i);

                    InitMirrorDropdown();
                    try
                    {
                        Dispatcher.Invoke(() => InstalledClientLabel.Content = GetInstalledClientName());
                    }
                    catch (Exception ex)
                    {
                        ShowConsoleWindow();
                        LogWriteLine($"No Client Installed!\r\n{ex}", LogType.Error, true);
                        DisableAllFunction();
                    }

                    ScreenProp.InitScreenResolution();
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
            return;
        }

        internal void InitMirrorDropdown()
        {
            Dispatcher.Invoke(() =>
            {
                MirrorSelector.ItemsSource = ConfigStore.AppConfigData.AvailableMirror;
                MirrorSelector.SelectedIndex = ConfigStore.AppConfigData.MirrorSelection;
                MirrorSelectorStatus.Content = MirrorSelector.SelectedItem;
            });
        }

        internal string GetInstalledClientName() => ConfigStore.Config.Select(i => i.ZoneName).Aggregate((i, j) => i + ", " + j);

        internal static bool IsConfigAvailableSteam(PresetConfigClasses i)
        {
            string RegValue = "InstallLocation";
            bool ret = true;

            try
            {
                i.ActualGameDataLocation = (string)Registry.GetValue(i.InstallRegistryLocation, RegValue, null);
                i.ActualGameLocation = Directory.GetParent(i.ActualGameDataLocation).FullName;

                string value = "";
                RegistryKey keys = Registry.CurrentUser.OpenSubKey(i.ConfigRegistryLocation);
                foreach (string valueName in keys.GetValueNames())
                    if (valueName.Contains("GENERAL_DATA_V2_ResourceDownloadVersion_"))
                        value = valueName;

                i.UsedLanguage = i.GetUsedLanguage(i.ConfigRegistryLocation, "MIHOYOSDK_NOTICE_LANGUAGE_", i.FallbackLanguage);
                i.GameVersion = Encoding.UTF8.GetString((byte[])Registry.GetValue($"HKEY_CURRENT_USER\\{i.ConfigRegistryLocation}", value, i.FallbackLanguage)).Replace('.', '_').Replace("\0", string.Empty) + "_0";
                ConfigStore.Config.Add(i);

                LogWriteLine($"\u001b[34;1m{i.ZoneName}\u001b[0m (\u001b[32;1m{Path.GetFileName(i.ConfigRegistryLocation)}\u001b[0m) version is detected!");
            }
            catch (DirectoryNotFoundException e)
            {
                LogWriteLine(e.ToString(), LogType.Warning, true);
            }
            catch (NullReferenceException e)
            {
                LogWriteLine(e.ToString(), LogType.Warning, true);
            }
            catch (Exception e)
            {
                LogWriteLine(e.ToString(), LogType.Error, true);
            }

            return ret;
        }

        internal static bool IsConfigAvailable(PresetConfigClasses i)
        {
            string RegValue = "InstallPath";
            bool ret = true;

            string a;
            try
            {
                a = (string)Registry.GetValue(i.InstallRegistryLocation, RegValue, null);
                if (a == null)
                {
                    ret = false;
                    throw new NullReferenceException($"Registry for \"{i.ZoneName}\" version doesn't exist, probably the version isn't installed.");
                }

                if (!Directory.Exists(a))
                {
                    if (Directory.Exists(i.DefaultGameLocation))
                    {
                        i.ActualGameLocation = i.DefaultGameLocation;
                        ConfigStore.Config.Add(i);
                        LogWriteLine($"Registered path for {i.ZoneName} version doesn't exist. But the default path does exist!", LogType.Warning);
                        return true;
                    }
                    ret = false;
                    throw new DirectoryNotFoundException($"Registry does exist but the registered directory for \"{i.ZoneName}\" version seems to be missing!");
                }
                else
                {
                    i.SetGameLocationParameters(a);
                    ConfigStore.Config.Add(i);
                    LogWriteLine($"\u001b[34;1m{i.ZoneName}\u001b[0m (\u001b[32;1m{Path.GetFileName(i.ConfigRegistryLocation)}\u001b[0m) version is detected!");
                }
            }
            catch (DirectoryNotFoundException e)
            {
                LogWriteLine(e.ToString(), LogType.Warning, true);
            }
            catch (NullReferenceException e)
            {
                LogWriteLine(e.ToString(), LogType.Warning, true);
            }
            catch (Exception e)
            {
                LogWriteLine(e.ToString(), LogType.Error, true);
            }

            return ret;
        }
    }
}
