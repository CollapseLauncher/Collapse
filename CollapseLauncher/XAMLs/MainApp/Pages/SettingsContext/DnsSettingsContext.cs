using CollapseLauncher.Helper;
using Hi3Helper.Shared.Region;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Runtime.CompilerServices;
using TurnerSoftware.DinoDNS;

#nullable enable
namespace CollapseLauncher.Pages.SettingsContext
{
    internal class DnsSettingsContext(TextBox customDnsHostTextbox, TextBlock customDnsSettingsChangeWarning) : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;

        public List<string> ExternalDnsConnectionTypeList = GetExternalDnsConnectionTypeList();
        public List<string> ExternalDnsProviderList = GetExternalDnsProviderList();

        private static List<string> GetExternalDnsProviderList()
        {
            List<string> list = [];
            list.AddRange(HttpClientBuilder.DnsServerTemplate.Keys);
            list.Add("Custom");

            return list;
        }

        private static List<string> GetExternalDnsConnectionTypeList()
        {
            List<string> returnList = [];
            foreach (ConnectionType type in Enum.GetValues<ConnectionType>())
            {
                string translatedText = type switch
                {
                    ConnectionType.Udp => "UDP (Port 53)",
                    ConnectionType.Tcp => "TCP (Port 53)",
                    ConnectionType.UdpWithTcpFallback => "UDP + TCP Fallback",
                    ConnectionType.DoH => "DNS over HTTPS",
                    ConnectionType.DoT => "DNS over TLS",
                    _ => type.ToString()
                };
                returnList.Add(translatedText);
            }

            return returnList;
        }

        private void SetExternalDnsAddresses(string addresses, ConnectionType connType)
        {
            ExternalDnsAddressesRawString = $"{addresses}|{connType}";
        }

        public string? ExternalDnsAddressesRawString
        {
            get => field ??= LauncherConfig.GetAppConfigValue("ExternalDnsAddresses");
            set
            {
                field = value;
                LauncherConfig.SetAndSaveConfigValue("ExternalDnsAddresses", value);
            }
        }

        public int ExternalDnsProvider
        {
            get
            {
                ReadOnlySpan<char> keyLookup = ExternalDnsAddresses.AsSpan().TrimStart('$');
                if (keyLookup.IsEmpty)
                    return (int)ConnectionType.DoH;

                var dictLookup = HttpClientBuilder.DnsServerTemplateKeyIndex.GetAlternateLookup<ReadOnlySpan<char>>();
                if (dictLookup.TryGetValue(keyLookup, out int index))
                {
                    return index;
                }

                customDnsHostTextbox.Visibility = Visibility.Visible;
                return ExternalDnsProviderList.Count - 1;
            }
            set
            {
                int customIndex = ExternalDnsProviderList.Count - 1;
                bool isCustom = value == customIndex;

                customDnsHostTextbox.Visibility = isCustom ? Visibility.Visible : Visibility.Collapsed;
                if (isCustom)
                {
                    return;
                }

                string key = ExternalDnsProviderList[value];
                string addressKey = $"${key.ToLower()}";
                ExternalDnsAddresses = addressKey;
                OnPropertyChanged();
            }
        }

        [field: AllowNull, MaybeNull]
        public string ExternalDnsAddresses
        {
            get
            {
                if (field != null)
                    return field;

                ReadOnlySpan<char> value = ExternalDnsAddressesRawString;
                Span<Range> rangeSpan = stackalloc Range[2];
                int rangeLen = value.Split(rangeSpan, '|');
                if (rangeLen < 1)
                {
                    return field = GetResult(value[rangeSpan[0]]);
                }

                return field = GetResult(value[rangeSpan[0]]);

                static string GetResult(ReadOnlySpan<char> input)
                {
                    if (input.IsEmpty)
                        return GetDefault();

                    return input.ToString();
                }

                static string GetDefault()
                {
                    string key = HttpClientBuilder.DnsServerTemplate.FirstOrDefault().Key;
                    return $"${key.ToLower()}";
                }
            }
            set
            {
                field = value;
                SetExternalDnsAddresses(value, (ConnectionType)ExternalDnsConnectionType);
                OnPropertyChanged();
            }
        }

        public int ExternalDnsConnectionType
        {
            get
            {
                ReadOnlySpan<char> value = ExternalDnsAddressesRawString;
                if (value.IsEmpty)
                    return (int)ConnectionType.DoH;

                Span<Range> rangeSpan = stackalloc Range[2];
                int rangeLen = value.Split(rangeSpan, '|');
                if (rangeLen < 2)
                    return (int)ConnectionType.DoH;

                ReadOnlySpan<char> typeSpan = value[rangeSpan[1]];
                if (Enum.TryParse(typeSpan, true, out ConnectionType type))
                    return (int)type;

                return (int)ConnectionType.DoH;
            }
            set
            {
                ConnectionType type = (ConnectionType)value;
                if (!Enum.IsDefined(type))
                {
                    type = ConnectionType.DoH;
                }

                SetExternalDnsAddresses(ExternalDnsAddresses, type);
                OnPropertyChanged();
            }
        }

        public bool IsUseExternalDns
        {
            get => LauncherConfig.GetAppConfigValue("IsUseExternalDns");
            set
            {
                LauncherConfig.SetAndSaveConfigValue("IsUseExternalDns", value);
                OnPropertyChanged();
            }
        }

        private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            customDnsSettingsChangeWarning.Visibility = Visibility.Visible;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
