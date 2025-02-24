using Hi3Helper;
using Hi3Helper.Win32.Native.Structs.Dns.RecordDataType;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using TurnerSoftware.DinoDNS;
using TurnerSoftware.DinoDNS.Protocol;
using Dns = Hi3Helper.Win32.ManagedTools.Dns;
// ReSharper disable CheckNamespace
// ReSharper disable StaticMemberInGenericType
// ReSharper disable IdentifierTypo
// ReSharper disable CommentTypo

#nullable enable
namespace CollapseLauncher.Helper
{
    public partial class HttpClientBuilder<THandler>
    {
        private static readonly Dictionary<string, IPAddress[]> DnsServerTemplate = new(StringComparer.OrdinalIgnoreCase)
        {
            { "Google", [ IPAddress.Parse("8.8.8.8"), IPAddress.Parse("8.8.4.4"), IPAddress.Parse("2001:4860:4860::8888"), IPAddress.Parse("2001:4860:4860::8844") ] },
            { "Cloudflare", [ IPAddress.Parse("1.1.1.1"), IPAddress.Parse("1.0.0.1"), IPAddress.Parse("2606:4700:4700::1111"), IPAddress.Parse("2606:4700:4700::1001") ] }
        };

        private static readonly Dictionary<string, string[]> DnsEvaluateResolveCache =
            new(StringComparer.OrdinalIgnoreCase);

        private static readonly ConcurrentDictionary<string, IPAddress[]> DnsClientResolveCache =
            new(StringComparer.OrdinalIgnoreCase);

        private static bool IsUseExternalDns { get; set; }

        private static DnsClient? ExternalDns { get; set; }

        private static void ParseDnsSettings(string? inputString, out string[] hosts, out ConnectionType connectionType)
        {
            // TODO: Test this parser with input string like:
            //     - $google;|doT             -> Expected Result: Uses Google as DNS server, uses ConnectionType.DoT
            //     - $clOudFlarE|             -> Expected Result: Uses Cloudflare as DNS server, Has connection type separator with undefined value, fallback to ConnectionType.DoH
            //     - $clOudFlarE|Dunno        -> Expected Result: Uses Cloudflare as DNS server, unknown connection type value, fallback to ConnectionType.DoH
            //     - $clOudFlarE              -> Expected Result: Uses Cloudflare as DNS server, Has no connection type separator and no value, fallback to ConnectionType.DoH
            //     - 8.8.8.8;$cloudflare|uDp  -> Expected Result: Uses both 8.8.8.8 and Cloudflare as DNS server, uses ConnectionType.Udp
            //     - $cloudflare;$google|DoH  -> Expected Result: Uses both Cloudflare and Google as DNS server, uses ConnectionType.DoH
            //     - [null] or [empty string] -> Expected Result: Fallback to use Google as DNS server, fallback to ConnectionType.DoH
            //     - |DoH                     -> Expected Result: Fallback to use Google as DNS server, uses ConnectionType.DoH
            //     - 2001:4860:4860::8888|DoH -> Expected Result: Uses 2001:4860:4860::8888 as DNS server, uses ConnectionType.DoH
            //     - dns.google|DoH           -> Expected Result: Evaluate to 8.8.8.8 and 8.8.4.4 then use it as DNS server, uses ConnectionType.DoH

            connectionType = ConnectionType.DoH;

            if (string.IsNullOrEmpty(inputString))
            {
                GetDefault(out hosts);
                return;
            }

            ReadOnlySpan<char> inputAsSpan   = inputString;
            Span<Range>        delimitRanges = stackalloc Range[2];
            int delimitRangesLen = inputAsSpan
                                  .Split(delimitRanges,
                                         '|',
                                         StringSplitOptions.RemoveEmptyEntries);

            switch (delimitRangesLen)
            {
                case 0:
                    GetDefault(out hosts);
                    return;
                case >= 2:
                {
                    ReadOnlySpan<char> inputConnType = inputAsSpan[delimitRanges[1]];
                    if (!Enum.TryParse(inputConnType, true, out connectionType))
                    {
                        connectionType = ConnectionType.DoH;
                        Logger.LogWriteLine($"[HttpClientBuilder<T>::ParseDnsSettings] Cannot parse the connection type token. Falling back to DoH. Current string: {inputConnType.ToString()}", LogType.Error, true);
                    }

                    break;
                }
            }

            ReadOnlySpan<char> inputHost           = inputAsSpan[delimitRanges[0]];
            Span<Range>        inputHostSplitRange = stackalloc Range[32]; // Set maximum as 32 entries
            int inputHostSplitLen = inputHost.Split(inputHostSplitRange,
                                                    ';',
                                                    StringSplitOptions.RemoveEmptyEntries);

            if (inputHostSplitLen == 0)
            {
                if (inputHost.IsEmpty)
                {
                    GetDefault(out hosts);
                    return;
                }

                if (inputHost[0] == '$')
                {
                    hosts = [inputHost[0].ToString()];
                    return;
                }

                if (!IPAddress.TryParse(inputHost, out _))
                {
                    EvaluateHostAndGetIp(inputHost, out string[] hostsOut);
                    if (hostsOut.Length == 0)
                    {
                        GetDefault(out hosts);
                    }
                    else
                    {
                        hosts = hostsOut;
                    }

                    return;
                }
            }

            List<string> hostRangeList = [];
            foreach (Range ipHostRange in inputHostSplitRange[..inputHostSplitLen])
            {
                ReadOnlySpan<char> currentRange = inputHost[ipHostRange];
                if (currentRange.IsEmpty)
                {
                    continue;
                }

                if (currentRange[0] == '$')
                {
                    hostRangeList.Add(currentRange.ToString());
                    continue;
                }

                if (!IPAddress.TryParse(currentRange, out _))
                {
                    EvaluateHostAndGetIp(currentRange, out string[] currentAsIps);
                    if (currentAsIps.Length == 0)
                    {
                        continue;
                    }

                    hostRangeList.AddRange(currentAsIps);
                    continue;
                }

                hostRangeList.Add(currentRange.ToString());
            }

            if (hostRangeList.Count != 0)
            {
                hosts = hostRangeList.ToArray();
                return;
            }

            GetDefault(out hosts);
            return;

            static void EvaluateHostAndGetIp(ReadOnlySpan<char> host, out string[] addresses)
            {
                Dictionary<string, string[]>.AlternateLookup<ReadOnlySpan<char>> lookupCache = DnsEvaluateResolveCache.GetAlternateLookup<ReadOnlySpan<char>>();

                bool isCacheMiss;
                if ((isCacheMiss = lookupCache.TryGetValue(host, out string[]? resultCacheAddress)) && (resultCacheAddress?.Length ?? 0) != 0)
                {
                    addresses = resultCacheAddress!;
                    return;
                }

                IDNS_WITH_IPADDR[] recordAddressEvaluate = Dns
                                                          .EnumerateIPAddressFromHost(host.ToString(),
                                                               false,
                                                               ILoggerHelper
                                                                  .GetILogger("HttpClientBuilder<T>::ParseDnsSettings"))
                                                          .ToArray();

                if (recordAddressEvaluate.Length != 0)
                {
                    string[] recordAddress = recordAddressEvaluate
                                            .Select(x => x.GetIPAddress().ToString())
                                            .ToArray();

                    if (isCacheMiss)
                    {
                        lookupCache[host] = recordAddress;
                    }
                    else
                    {
                        lookupCache.TryAdd(host, recordAddress);
                    }

                    addresses = recordAddress;
                    return;
                }

                addresses = [];
            }

            static void GetDefault(out string[] innerHosts)
            {
                KeyValuePair<string, IPAddress[]>? valueTemplate = DnsServerTemplate.FirstOrDefault();
                if (valueTemplate == null)
                {
                    throw new NullReferenceException("[HttpClientBuilder<T>::ParseDnsSettings] No DnsServerTemplate is available!");
                }

                innerHosts = [$"${valueTemplate.Value.Key.ToLower()}"];
            }
        }

        public HttpClientBuilder<THandler> UseExternalDns(string[]? hosts = null, ConnectionType connectionType = ConnectionType.DoH)
        {
            if (hosts == null)
            {
                IsUseExternalDns = false;
                ExternalDns      = null;
                return this;
            }

            if (ExternalDns != null && IsUseExternalDns)
            {
                return this;
            }

            List<NameServer> nameServerList = [];
            // ReSharper disable once LoopCanBeConvertedToQuery
            foreach (IPAddress currentHost in EnumerateHostAsIp(hosts).Distinct())
            {
                nameServerList.Add(new NameServer(currentHost, connectionType));
            }

            if (nameServerList.Count != 0)
            {
                return UseExternalDns(nameServerList.ToArray());
            }

            Logger.LogWriteLine("[HttpClientBuilder<T>::ParseDnsSettings] No valid IP addresses has been parsed to be used as the DNS query host, the settings will be reverted", LogType.Warning, true);
            return this;

            static IEnumerable<IPAddress> EnumerateHostAsIp(IEnumerable<string> input)
            {
                Dictionary<string, IPAddress[]>.AlternateLookup<ReadOnlySpan<char>> lookup = DnsServerTemplate.GetAlternateLookup<ReadOnlySpan<char>>();

                foreach (ReadOnlySpan<char> currentHostLocal in input)
                {
                    if ('$' == currentHostLocal[0] && lookup.TryGetValue(currentHostLocal[1..], out IPAddress[]? addresses))
                    {
                        foreach (IPAddress ipEntry in addresses)
                        {
                            yield return ipEntry;
                        }
                        continue;
                    }

                    if (!IPAddress.TryParse(currentHostLocal, out IPAddress? addressFromString))
                    {
                        Logger.LogWriteLine($"[HttpClientBuilder<T>::ParseDnsSettings] Cannot parse string: {currentHostLocal} as it's not a valid IPv4 or IPv6 address format", LogType.Warning, true);
                        continue;
                    }

                    yield return addressFromString;
                }
            }
        }

        public HttpClientBuilder<THandler> UseExternalDns(NameServer[]? nameServers = null)
        {
            if (nameServers == null || nameServers.Length == 0)
            {
                IsUseExternalDns = false;
                ExternalDns      = null;
                return this;
            }

            IsUseExternalDns = true;
            ExternalDns      = new DnsClient(nameServers, DnsMessageOptions.Default);
            return this;
        }

        private static async ValueTask<Stream> ExternalDnsConnectCallback(
            SocketsHttpConnectionContext context, CancellationToken token)
        {
            if (ExternalDns == null)
            {
                return await ConnectWithSystemDns(context, token);
            }

            return await ConnectWithExternalDns(ExternalDns, context, token);
        }

        private static async ValueTask<Stream> ConnectWithSystemDns(SocketsHttpConnectionContext context,
                                                                    CancellationToken            token)
        {
            Socket socket = new(SocketType.Stream, ProtocolType.Tcp)
            {
                NoDelay = true
            };

            try
            {
                await socket.ConnectAsync(context.DnsEndPoint, token);
                return new NetworkStream(socket, ownsSocket: true);
            }
            catch
            {
                socket.Dispose();
                throw;
            }
        }

        private static async ValueTask<Stream> ConnectWithExternalDns(DnsClient                    dnsClient,
                                                                      SocketsHttpConnectionContext context,
                                                                      CancellationToken            token)
        {
            Socket socket = new(SocketType.Stream, ProtocolType.Tcp)
            {
                NoDelay = true
            };

            try
            {
                string host = context.DnsEndPoint.Host;
                bool isCached;

                if (!((isCached = DnsClientResolveCache
                        .TryGetValue(host, out IPAddress[]? cachedIpAddress)) &&
                     (cachedIpAddress?.Length ?? 0) != 0))
                {
                    DnsMessage dnsMessageIpv4 = await dnsClient.QueryAsync(host, DnsQueryType.A, DnsClass.IN, token);
                    ResourceRecordCollection recordsIpv4 = dnsMessageIpv4.Answers;

                    DnsMessage dnsMessageIpv6 = await dnsClient.QueryAsync(host, DnsQueryType.AAAA, DnsClass.IN, token);
                    ResourceRecordCollection recordsIpv6 = dnsMessageIpv6.Answers;

                    cachedIpAddress = recordsIpv4
                                     .MergeWith(recordsIpv6)
                                     .EnumerateAOrAAAARecordOnly()
                                     .Select(x => new IPAddress(x.Data.Span))
                                     .ToArray();

                    if (isCached)
                    {
                        DnsClientResolveCache[host] = cachedIpAddress;
                    }
                    else
                    {
                        DnsClientResolveCache.TryAdd(host, cachedIpAddress);
                    }
                }

                if (cachedIpAddress == null || cachedIpAddress.Length == 0)
                    throw new Exception($"Cannot resolve the address of the host: {host}");

                await socket.ConnectAsync(cachedIpAddress, context.DnsEndPoint.Port, token);
                return new NetworkStream(socket, ownsSocket: true);
            }
            catch
            {
                socket.Dispose();
                throw;
            }
        }
    }

    internal static class ResourceRecordCollectionExtension
    {
        internal static IEnumerable<ResourceRecord> MergeWith(this ResourceRecordCollection records,
                                                              ResourceRecordCollection      another)
        {
            foreach (ResourceRecord record in records)
            {
                yield return record;
            }

            foreach (ResourceRecord record in another)
            {
                yield return record;
            }
        }

        internal static IEnumerable<ResourceRecord> EnumerateAOrAAAARecordOnly(
            this IEnumerable<ResourceRecord> records)
        {
            foreach (ResourceRecord record in records)
            {
                if (record.Type is DnsType.A or DnsType.AAAA)
                {
                    yield return record;
                }
            }
        }
    }
}
