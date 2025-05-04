using CollapseLauncher.Helper;
using Hi3Helper;
using Hi3Helper.Shared.Region;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
// ReSharper disable CheckNamespace

#nullable enable
namespace CollapseLauncher.Pages.SettingsContext
{
    internal class DnsSettingsContext(TextBox customDnsHostTextbox) : INotifyPropertyChanged
    {
        public event EventHandler? PropertySavedChanged;
        public event PropertyChangedEventHandler? PropertyChanged;

        public string? DefaultExternalDnsProvider => field ??= ExternalDnsProviderList?.FirstOrDefault();

        public List<string>? ExternalDnsConnectionTypeList
        {
            get => field ??= GetExternalDnsConnectionTypeList();
            set;
        }

        public List<string>? ExternalDnsProviderList
        {
            get => field ??= GetExternalDnsProviderList();
            set;
        }

        private static List<string> GetExternalDnsProviderList()
        {
            List<string> list = [];
            list.AddRange(HttpClientBuilder.DnsServerTemplate.Keys);
            list.Add(Locale.Lang._SettingsPage.NetworkSettings_Dns_ProviderSelection_SelectionCustom);

            return list;
        }

        private static List<string> GetExternalDnsConnectionTypeList()
        {
            List<string> returnList = [];
            returnList.AddRange(Enum.GetValues<DnsConnectionType>()
                                    .Select(type => type switch
                                                    {
                                                        DnsConnectionType.Udp => Locale.Lang._SettingsPage.NetworkSettings_Dns_ConnectionType_SelectionUdp,
                                                        DnsConnectionType.DoH => Locale.Lang._SettingsPage.NetworkSettings_Dns_ConnectionType_SelectionDoH,
                                                        DnsConnectionType.DoT => Locale.Lang._SettingsPage.NetworkSettings_Dns_ConnectionType_SelectionDoT,
                                                        _ => type.ToString()
                                                    }));

            return returnList;
        }

        public int ExternalDnsProvider
        {
            get
            {
                ReadOnlySpan<char> keyLookup = ExternalDnsAddresses.AsSpan().TrimStart('$');
                if (keyLookup.IsEmpty)
                    return field = (int)DnsConnectionType.DoH;

                Dictionary<string, int>.AlternateLookup<ReadOnlySpan<char>> dictLookup = HttpClientBuilder
                    .DnsServerTemplateKeyIndex
                    .GetAlternateLookup<ReadOnlySpan<char>>();

                if (dictLookup.TryGetValue(keyLookup, out int index))
                {
                    return field = index;
                }

                customDnsHostTextbox.Visibility = Visibility.Visible;
                return field = (ExternalDnsProviderList?.Count ?? 1) - 1;
            }
            set
            {
                if (value < 0)
                {
                    return;
                }

                if (value == field)
                {
                    return;
                }

                int customIndex = (ExternalDnsProviderList?.Count ?? 1) - 1;
                bool isCustom = (field = value) == customIndex;

                customDnsHostTextbox.Visibility = isCustom ? Visibility.Visible : Visibility.Collapsed;
                if (isCustom)
                {
                    return;
                }

                string? key = ExternalDnsProviderList?[value];
                string addressKey = $"${key?.ToLower()}";
                ExternalDnsAddresses = addressKey;
                OnPropertyChanged();
            }
        }

        public string? ExternalDnsAddresses
        {
            get
            {
                if (field != null)
                {
                    return field;
                }

                ReadOnlySpan<char> value     = LauncherConfig.GetAppConfigValue("ExternalDnsAddresses").ToString();
                Span<Range>        rangeSpan = stackalloc Range[2];
                _ = value.Split(rangeSpan, '|');

                return field = GetResult(value[rangeSpan[0]]);

                static string GetResult(ReadOnlySpan<char> input)
                    => input.IsEmpty ? GetDefault() : input.ToString();

                static string GetDefault()
                {
                    string key = HttpClientBuilder.DnsServerTemplate.FirstOrDefault().Key;
                    return $"${key.ToLower()}";
                }
            }
            set
            {
                if (field?.Equals(value) ?? false)
                {
                    return;
                }

                field = value;
                OnPropertyChanged();
            }
        }

        private int? _externalDnsConnectionType;
        public int ExternalDnsConnectionType
        {
            get
            {
                const int defaultValue = (int)DnsConnectionType.DoH;

                if (_externalDnsConnectionType != null)
                {
                    return _externalDnsConnectionType ?? 0;
                }

                ReadOnlySpan<char> value = LauncherConfig.GetAppConfigValue("ExternalDnsAddresses").ToString();
                if (value.IsEmpty)
                {
                    _externalDnsConnectionType = defaultValue;
                    return defaultValue;
                }

                Span<Range> rangeSpan = stackalloc Range[2];
                int rangeLen = value.Split(rangeSpan, '|');
                if (rangeLen < 2)
                {
                    _externalDnsConnectionType = defaultValue;
                    return defaultValue;
                }

                ReadOnlySpan<char> typeSpan = value[rangeSpan[1]];
                if (Enum.TryParse(typeSpan, true, out DnsConnectionType type))
                {
                    int typeValue = (int)type;
                    _externalDnsConnectionType = typeValue;
                    return typeValue;
                }

                _externalDnsConnectionType = defaultValue;
                return defaultValue;
            }
            set
            {
                if (value < 0)
                {
                    return;
                }

                if (_externalDnsConnectionType == value)
                {
                    return;
                }

                DnsConnectionType type = (DnsConnectionType)value;
                if (!Enum.IsDefined(type))
                {
                    type = DnsConnectionType.DoH;
                }

                _externalDnsConnectionType = (int)type;
                OnPropertyChanged();
            }
        }

        private bool? _isUseExternalDns;
        public bool IsUseExternalDns
        {
            get => _isUseExternalDns ??= LauncherConfig.GetAppConfigValue("IsUseExternalDns");
            set
            {
                if (_isUseExternalDns == value)
                {
                    return;
                }

                LauncherConfig.SetAndSaveConfigValue("IsUseExternalDns", (bool)(_isUseExternalDns = value));
                OnPropertyChanged();
                PropertySavedChanged?.Invoke(null, null!);
            }
        }

        public void SaveSettings()
        {
            DnsConnectionType connType       = (DnsConnectionType)ExternalDnsConnectionType;
            string            addresses      = ExternalDnsAddresses ?? string.Empty;
            string            rawDnsSettings = $"{addresses}|{connType}";

            LauncherConfig.SetAndSaveConfigValue("IsUseExternalDns", IsUseExternalDns);
            LauncherConfig.SetAndSaveConfigValue("ExternalDnsAddresses", rawDnsSettings);
            PropertySavedChanged?.Invoke(null, null!);
        }

        private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
