using Hi3Helper;
using Hi3Helper.Shared.Region;
using Hi3Helper.Win32.Native.Structs.Dns.RecordDataType;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using TurnerSoftware.DinoDNS;
using TurnerSoftware.DinoDNS.Protocol;
using Dns = Hi3Helper.Win32.ManagedTools.Dns;
// ReSharper disable CheckNamespace
// ReSharper disable StaticMemberInGenericType
// ReSharper disable IdentifierTypo
// ReSharper disable CommentTypo
// ReSharper disable InconsistentNaming

#nullable enable
namespace CollapseLauncher.Helper
{
    public enum DnsConnectionType
    {
        DoH,
        DoT,
        Udp
    }

    public partial class HttpClientBuilder
    {
        internal static readonly IPAddressEqualityComparer IPAddressComparer = new();

        public const string DnsHostSeparators = ";:,#/@%";
        public const StringSplitOptions DnsHostSplitOptions = StringSplitOptions.RemoveEmptyEntries |
                                                              StringSplitOptions.TrimEntries;

        private const           string    DnsLoopbackHost          = "localhost";
        private static readonly IPAddress DnsLoopbackIPAddrv4      = IPAddress.Loopback;
        private static readonly byte[]    DnsLoopbackIPAddrv4Bytes = DnsLoopbackIPAddrv4.GetAddressBytes();
        private static readonly IPAddress DnsLoopbackIPAddrv6      = IPAddress.IPv6Loopback;
        private static readonly byte[]    DnsLoopbackIPAddrv6Bytes = DnsLoopbackIPAddrv6.GetAddressBytes();

        internal static readonly Dictionary<string, IPAddress[]> DnsServerTemplate = new(StringComparer.OrdinalIgnoreCase)
        {
            { "Cloudflare", [ IPAddress.Parse("1.1.1.1"), IPAddress.Parse("2606:4700:4700::1111"), IPAddress.Parse("1.0.0.1"), IPAddress.Parse("2606:4700:4700::1001") ] },
            { "Google", [ IPAddress.Parse("8.8.8.8"), IPAddress.Parse("2001:4860:4860::8888"), IPAddress.Parse("8.8.4.4"), IPAddress.Parse("2001:4860:4860::8844") ] },
            { "quad9", [ IPAddress.Parse("9.9.9.10"), IPAddress.Parse("2620:fe::10"), IPAddress.Parse("149.112.112.10"), IPAddress.Parse("2620:fe::fe:10") ] }
        };

        internal static readonly Dictionary<string, int> DnsServerTemplateKeyIndex = GetDnsServerTemplateKeyIndex();

        private static Dictionary<string, int> GetDnsServerTemplateKeyIndex()
        {
            int index = 0;
            Dictionary<string, int> returnDict = new(StringComparer.OrdinalIgnoreCase);
            foreach (KeyValuePair<string, IPAddress[]> kvp in DnsServerTemplate)
            {
                returnDict.Add(kvp.Key, index++);
            }

            return returnDict;
        }

        private static readonly ConcurrentDictionary<string, IPAddress[]> DnsClientResolveCache =
            new(StringComparer.OrdinalIgnoreCase);

        private static readonly ConcurrentDictionary<string, DateTimeOffset> DnsClientResolveTtlCache =
            new(StringComparer.OrdinalIgnoreCase);

        internal static bool          IsUseExternalDns => SharedExternalDnsServers is { Length: > 0 };
        internal static NameServer[]? SharedExternalDnsServers;

        /// <summary>
        /// Supported input format:<br/>
        ///     [provider_prefix or ip_addressv4/v6];[...]|[connection_type]<br/>
        /// <br/>
        /// DNS Provider Entries:<br/>
        ///    - provider_prefix: The prefix of the DNS provider, e.g. $google for Google, $cloudflare for Cloudflare and $quad9 for Quad9.<br/>
        ///    - ip_addressv4/v6: Prefer to use other DNS server by using the IP address of the DNS server (supporting IPv4 and IPv6).<br/>
        /// The DNS provider entries are case-insensitive and can be used alongside the IP address of other DNS server.<br/>
        /// For example, if you use "$google;$cloudflare", it will be evaluated as you use both Google and Cloudflare as the DNS server.<br/>
        /// If you use "$cloudflare;208.67.220.220", it will be evaluated as you use Cloudflare and other DNS server with IP address (in this example, the IPv4 of OpenDNS).<br/>
        /// The entries are mainly separated by semicolon (;). But this can be changed by using other<br/>
        /// separator characters (e.g. colon (:), comma (,), slash (/), hash (#), at (@), and percent (%)).<br/>
        /// <br/>
        /// DNS Connection Type:<br/>
        ///     - DoH : DNS over HTTPS<br/>
        ///     - DoT : DNS over TLS<br/>
        ///     - Udp : DNS over UDP<br/>
        /// </summary>
        /// <param name="inputString"></param>
        /// <param name="hosts"></param>
        /// <param name="connectionType"></param>
        /// <exception cref="NullReferenceException"></exception>
        internal static void ParseDnsSettings(string? inputString, out string[]? hosts, out DnsConnectionType connectionType)
        {
            // Accepted string formats to test (just in-case you entered a weird string)
            //     - $google;|doT             -> Expected Result: Uses Google as DNS server, uses DnsConnectionType.DoT
            //     - $clOudFlarE|             -> Expected Result: Uses Cloudflare as DNS server, Has connection type separator with undefined value, fallback to DnsConnectionType.DoH
            //     - $clOudFlarE|Dunno        -> Expected Result: Uses Cloudflare as DNS server, unknown connection type value, fallback to DnsConnectionType.DoH
            //     - $clOudFlarE              -> Expected Result: Uses Cloudflare as DNS server, Has no connection type separator and no value, fallback to DnsConnectionType.DoH
            //     - 8.8.8.8,$cloudflare|uDp  -> Expected Result: Uses both 8.8.8.8 and Cloudflare as DNS server, uses DnsConnectionType.Udp
            //     - $cloudflare,$google|DoH  -> Expected Result: Uses both Cloudflare and Google as DNS server, uses DnsConnectionType.DoH
            //     - $cloudflare:$google|DoH  -> Expected Result: Uses both Cloudflare and Google as DNS server, uses DnsConnectionType.DoH
            //     - [null] or [empty string] -> Expected Result: Fallback to use Google as DNS server, fallback to DnsConnectionType.DoH
            //     - |DoH                     -> Expected Result: Fallback to use Google as DNS server, uses DnsConnectionType.DoH
            //     - 2001:4860:4860::8888|DoH -> Expected Result: Uses 2001:4860:4860::8888 as DNS server, uses DnsConnectionType.DoH
            //     - dns.google|DoH           -> Expected Result: Evaluate to 8.8.8.8 and 8.8.4.4 then use it as DNS server, uses DnsConnectionType.DoH

            connectionType = DnsConnectionType.DoH;
            hosts          = null;

            if (string.IsNullOrEmpty(inputString))
            {
                return;
            }

            if (TryParseDnsConnectionType(inputString, out connectionType) &&
                (hosts = TryParseDnsHostsAsync(inputString, false, false).GetAwaiter().GetResult()) != null)
            {
                return;
            }

            GetDefault(out hosts);
            return;

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

        public static bool TryParseDnsConnectionType(ReadOnlySpan<char> inputAsSpan, out DnsConnectionType connType)
        {
            connType = DnsConnectionType.DoH;
            if (inputAsSpan.IsEmpty)
            {
                return false;
            }

            Span<Range> delimitRanges    = stackalloc Range[2];
            int         delimitRangesLen = inputAsSpan.Split(delimitRanges, '|');

            switch (delimitRangesLen)
            {
                case 0:
                    return false;
                case >= 2:
                {
                    ReadOnlySpan<char> inputConnType = inputAsSpan[delimitRanges[1]];
                    if (!Enum.TryParse(inputConnType, true, out connType))
                    {
                        connType = DnsConnectionType.DoH;
                        Logger.LogWriteLine($"[HttpClientBuilder<T>::TryParseDnsConnectionType] Cannot parse the connection type token. Falling back to DoH. Current string: {inputConnType.ToString()}", LogType.Error, true);
                        return false;
                    }

                    break;
                }
            }

            return true;
        }

        public static async Task<string[]?> TryParseDnsHostsAsync(string inputAsSpan, bool mustPassAll, bool bypassCache, CancellationToken token = default)
        {
            if (string.IsNullOrEmpty(inputAsSpan))
            {
                return null;
            }

            Span<Range> delimitRanges = stackalloc Range[2];
            _ = inputAsSpan.Split(delimitRanges, '|');

            ReadOnlySpan<char> inputHost           = inputAsSpan[delimitRanges[0]];
            Memory<Range>      inputHostSplitRange = new Range[32]; // Set maximum as 32 entries

            int inputHostSplitLen = inputHost.SplitAny(inputHostSplitRange.Span, DnsHostSeparators, DnsHostSplitOptions);
            Dictionary<string, IPAddress[]>.AlternateLookup<ReadOnlySpan<char>> templateLookup = DnsServerTemplate.GetAlternateLookup<ReadOnlySpan<char>>();

            if (inputHostSplitLen is 0 or 1)
            {
                string firstHost = inputHost.ToString();
                if (inputHost.IsEmpty)
                {
                    return null;
                }

                if (inputHost[0] == '$' && templateLookup.ContainsKey(firstHost.AsSpan(1)))
                {
                    return [firstHost];
                }

                if (!IPAddress.TryParse(firstHost, out _))
                {
                    IPAddress[]? hostsOut = await EvaluateHostAndGetIpAsync(firstHost, bypassCache, token);
                    if (hostsOut?.Length == 0)
                    {
                        return null;
                    }

                    return hostsOut?
                          .Select(x => x.ToString())
                          .ToArray();
                }
            }

            List<string>  hostRangeList = [];
            Memory<Range> splittedRange = inputHostSplitRange[..inputHostSplitLen];
            for (int i = 0; i < splittedRange.Length; i++)
            {
                Range  ipHostRange  = splittedRange.Span[i];
                string currentRange = GetStringFromRange(inputAsSpan, ipHostRange);
                if (string.IsNullOrEmpty(currentRange))
                {
                    continue;
                }

                if (currentRange[0] == '$' && templateLookup.ContainsKey(currentRange.AsSpan(1)))
                {
                    hostRangeList.Add(currentRange);
                    continue;
                }

                if (!IPAddress.TryParse(currentRange, out _))
                {
                    IPAddress[]? currentAsIps = await EvaluateHostAndGetIpAsync(currentRange, bypassCache, token);
                    if (currentAsIps?.Length == 0)
                    {
                        if (mustPassAll)
                        {
                            return null;
                        }
                        continue;
                    }

                    hostRangeList.AddRange(currentAsIps?.Select(x => x.ToString()) ?? []);
                    continue;
                }

                hostRangeList.Add(currentRange);
            }

            return hostRangeList.Count != 0 ? hostRangeList.ToArray() : null;

            static string GetStringFromRange(ReadOnlySpan<char> span, Range range)
                => span[range].ToString();

            static async ValueTask<IPAddress[]?> EvaluateHostAndGetIpAsync(string host, bool bypassCache, CancellationToken innerToken)
            {
                if (!bypassCache && TryGetCachedIp(host, out IPAddress[]? cachedIpAddress))
                {
                    return cachedIpAddress;
                }

                ConcurrentDictionary<string, IPAddress[]>.AlternateLookup<ReadOnlySpan<char>> resolveCacheLookup = DnsClientResolveCache.GetAlternateLookup<ReadOnlySpan<char>>();
                ConcurrentDictionary<string, DateTimeOffset>.AlternateLookup<ReadOnlySpan<char>> ttlCacheLookup = DnsClientResolveTtlCache.GetAlternateLookup<ReadOnlySpan<char>>();

                List<IPAddress> addressedHosts = new List<IPAddress>(16);
                List<uint>      addressedTtls  = new List<uint>(16);

                await foreach ((IDNS_WITH_IPADDR record, uint timeToLive) in Dns
                                  .EnumerateIPAddressFromHostAsync(host,
                                                                   bypassCache,
                                                                   true,
                                                                   ILoggerHelper.GetILogger("HttpClientBuilder<T>::ParseDnsSettings"),
                                                                   innerToken))
                {
                    IPAddress address = record.GetIPAddress();
                    addressedHosts.Add(address);
                    addressedTtls.Add(timeToLive);
                }

                if (addressedHosts.Count == 0)
                {
                    return null;
                }

                IPAddress[] recordAddress = addressedHosts.ToArray();
                if (bypassCache)
                {
                    return recordAddress;
                }

                uint           recordAvgTtl = (uint)addressedTtls.Average(x => x);
                DateTimeOffset ttlOffset    = DateTimeOffset.Now.AddSeconds(recordAvgTtl);
                resolveCacheLookup.TryAdd(host, recordAddress);
                ttlCacheLookup.TryAdd(host, ttlOffset);

                return recordAddress;
            }
        }

        internal static void UseExternalDns(string[]? servers = null, DnsConnectionType connectionType = DnsConnectionType.DoH)
        {
            if (servers == null || servers.Length == 0)
            {
                UseExternalDns(null);
                return;
            }

            List<NameServer> nameServerList = [];
            // ReSharper disable once LoopCanBeConvertedToQuery
            foreach (IPAddress currentHost in EnumerateHostAsIp(servers).Distinct(IPAddressComparer))
            {
                nameServerList.Add(new NameServer(currentHost, connectionType switch
                {
                    DnsConnectionType.Udp => ConnectionType.Udp,
                    DnsConnectionType.DoT => ConnectionType.DoT,
                    _ => ConnectionType.DoH
                }));
            }

            if (nameServerList.Count != 0)
            {
                UseExternalDns(nameServerList.ToArray());
                return;
            }

            Logger.LogWriteLine("[HttpClientBuilder<T>::ParseDnsSettings] No valid IP addresses has been parsed to be used as the DNS query host, the settings will be reverted", LogType.Warning, true);
            return;
        }

        internal static IEnumerable<IPAddress> EnumerateHostAsIp(IEnumerable<string> input)
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

        internal static void UseExternalDns(NameServer[]? nameServers = null) => Interlocked.Exchange(ref SharedExternalDnsServers, nameServers);

        protected static async ValueTask<Stream> ExternalDnsConnectCallback(
            SocketsHttpConnectionContext context, CancellationToken token)
        {
            Socket socket = new(SocketType.Stream, ProtocolType.Tcp)
            {
                NoDelay = true
            };

            try
            {
                if (SharedExternalDnsServers == null || SharedExternalDnsServers.Length == 0)
                {
                    await socket.ConnectAsync(context.DnsEndPoint, token);
                    return new NetworkStream(socket, ownsSocket: true);
                }

                string      host            = context.DnsEndPoint.Host;
                IPAddress[] hostIpAddresses = await ResolveHostToIpAsync(host, SharedExternalDnsServers, token);

                await socket.ConnectAsync(hostIpAddresses, context.DnsEndPoint.Port, token);
                return new NetworkStream(socket, ownsSocket: true);
            }
            catch
            {
                socket.Dispose();
                throw;
            }
        }

        internal static async Task<IPAddress[]> ResolveHostToIpAsync(string host, NameServer[] dnsNameServers, CancellationToken token)
        {
            if (!TryGetCachedIp(host, out IPAddress[]? cachedIpAddress))
            {
                (ResourceRecordCollection recordsIpv4, ResourceRecordCollection recordsIpv6) =
                    await GetFallbackQuery(host, dnsNameServers, token);

                (ResourceRecord record, uint timeToLive)[] recordInfo = recordsIpv4
                                                                       .MergeWithZigZag(recordsIpv6)
                                                                       .EnumerateAOrAAAARecordOnly()
                                                                       .ToArray();

                cachedIpAddress = recordInfo
                                 .Select(x => new IPAddress(x.record.Data.Span))
                                 .ToArray();

                double avgTtl = recordInfo
                               .Select(x => x.timeToLive)
                               .Average(x => x);

                DateTimeOffset ttlOffset = DateTimeOffset.Now.AddSeconds(avgTtl);
                DnsClientResolveCache.TryAdd(host, cachedIpAddress);
                DnsClientResolveTtlCache.TryAdd(host, ttlOffset);
            }

            if (cachedIpAddress == null || cachedIpAddress.Length == 0)
                throw new Exception($"[HttpClientBuilder<T>::ConnectWithExternalDns] Cannot resolve the address of the host: {host}");

            return cachedIpAddress;
        }

        private static async ValueTask<(ResourceRecordCollection Ipv4, ResourceRecordCollection Ipv6)>
            GetFallbackQuery(string host, NameServer[] dnsNameServers, CancellationToken token)
        {
            Exception? lastException = null;
            (ResourceRecordCollection, ResourceRecordCollection)? returnTuple = await Impl(host, dnsNameServers, token);

            if (returnTuple != null)
            {
                return (returnTuple.Value.Item1, returnTuple.Value.Item2);
            }

            Logger.LogWriteLine($"[HttpClientBuilder<T>::GetFallbackQuery] Cannot resolve host: {host}. Trying to fallback...", LogType.Warning, true);
            DnsConnectionType[] restToTryType = Enum.GetValues<DnsConnectionType>();
            foreach (DnsConnectionType connType in restToTryType)
            {
                NameServer[] dnsNameFallback = GetModifiedConnNameServer(dnsNameServers, connType);

                returnTuple = await Impl(host, dnsNameFallback, token);
                if (returnTuple != null)
                {
                    return (returnTuple.Value.Item1, returnTuple.Value.Item2);
                }

                if (lastException != null)
                    Logger.LogWriteLine($"[HttpClientBuilder<T>::GetFallbackQuery] Cannot resolve host: {host} while using {connType} fallback connection. Ensure your ISP doesn't block the necessary ports.", LogType.Warning, true);
            }

            if (lastException != null)
                throw lastException;

            throw new Exception($"[HttpClientBuilder<T>::GetFallbackQuery] Cannot get resolve for host: {host}");

            NameServer[] GetModifiedConnNameServer(NameServer[] source, DnsConnectionType typeToChange)
            {
                NameServer[] dnsNameFallback = new NameServer[source.Length];
                for (int i = 0; i < dnsNameFallback.Length; i++)
                {
                    dnsNameFallback[i] = new NameServer(source[i].EndPoint, typeToChange switch
                    {
                        DnsConnectionType.Udp => ConnectionType.Udp,
                        DnsConnectionType.DoT => ConnectionType.DoT,
                        _ => ConnectionType.DoH
                    });
                }

                return dnsNameFallback;
            }

            async ValueTask<(ResourceRecordCollection, ResourceRecordCollection)?>
                Impl(string hostLocal, NameServer[] dnsNameLocal, CancellationToken tokenLocal)
            {
                try
                {
                    if (IsLoopbackOrIPAddr(hostLocal, out (ResourceRecordCollection, ResourceRecordCollection)? fallbackLocalReturn))
                    {
                        return fallbackLocalReturn;
                    }

                    DnsClient dnsClient = new(dnsNameLocal, DnsMessageOptions.Default);

                    DnsMessage dnsMessageIpv4 = await dnsClient.QueryAsync(hostLocal, DnsQueryType.A, DnsClass.IN, tokenLocal)
                                                               .ConfigureAwait(false);
                    ResourceRecordCollection recordsIpv4 = dnsMessageIpv4.Answers;

                    DnsMessage dnsMessageIpv6 = await dnsClient.QueryAsync(hostLocal, DnsQueryType.AAAA, DnsClass.IN, tokenLocal)
                                                               .ConfigureAwait(false);
                    ResourceRecordCollection recordsIpv6 = dnsMessageIpv6.Answers;

                    lastException = null;
                    return (recordsIpv4, recordsIpv6);
                }
                catch (Exception ex)
                {
                    lastException = ex;
                }

                return null;
            }

            bool IsLoopbackOrIPAddr(string hostLocal, out (ResourceRecordCollection, ResourceRecordCollection)? returnValue)
            {
                Unsafe.SkipInit(out returnValue);
                if (hostLocal.Equals(DnsLoopbackHost, StringComparison.OrdinalIgnoreCase))
                {
                    ResourceRecordCollection localhostIpv4 = CreateFromSingle(DnsLoopbackHost, DnsLoopbackIPAddrv4Bytes);
                    ResourceRecordCollection localhostIpv6 = CreateFromSingle(DnsLoopbackHost, DnsLoopbackIPAddrv6Bytes);

                    returnValue = (localhostIpv4, localhostIpv6);
                    return true;
                }

                if (!IPAddress.TryParse(hostLocal, out IPAddress? ipAddress))
                {
                    return false;
                }

                ResourceRecordCollection ipv4Return;
                ResourceRecordCollection ipv6Return;
                byte[] address = ipAddress.GetAddressBytes();

                if (ipAddress.AddressFamily.HasFlag(AddressFamily.InterNetworkV6))
                {
                    ipv4Return = CreateEmpty();
                    ipv6Return = CreateFromSingle(hostLocal, address);
                }
                else
                {
                    ipv4Return = CreateFromSingle(hostLocal, address);
                    ipv6Return = CreateEmpty();
                }

                returnValue = (ipv4Return, ipv6Return);
                return true;
            }

            ResourceRecordCollection CreateFromSingle(string hostInner, byte[] addressByte)
                => new(
                       [new ResourceRecord(hostInner,
                                           addressByte.Length > 4 ? DnsType.AAAA : DnsType.A,
                                           DnsClass.IN,
                                           uint.MaxValue,
                                           (ushort)addressByte.Length,
                                           addressByte)]
                      );

            ResourceRecordCollection CreateEmpty() => new();
        }

        private static bool TryGetCachedIp(ReadOnlySpan<char> host, out IPAddress[]? cachedIpAddress)
        {
            ConcurrentDictionary<string, IPAddress[]>.AlternateLookup<ReadOnlySpan<char>> resolveCacheLookup = DnsClientResolveCache.GetAlternateLookup<ReadOnlySpan<char>>();
            ConcurrentDictionary<string, DateTimeOffset>.AlternateLookup<ReadOnlySpan<char>> ttlCacheLookup = DnsClientResolveTtlCache.GetAlternateLookup<ReadOnlySpan<char>>();

            bool isCached = resolveCacheLookup.TryGetValue(host, out cachedIpAddress);
            bool isTtlCached = ttlCacheLookup.TryGetValue(host, out DateTimeOffset ttlInfo);

            if (!(isCached && isTtlCached) || (cachedIpAddress?.Length ?? 0) == 0)
            {
                resolveCacheLookup.TryRemove(host, out _);
                ttlCacheLookup.TryRemove(host, out _);
                return false;
            }

            DateTimeOffset currentTick = DateTimeOffset.Now;
            if (currentTick <= ttlInfo)
            {
                return true;
            }

            resolveCacheLookup.TryRemove(host, out _);
            ttlCacheLookup.TryRemove(host, out _);
            return false;
        }

        internal class IPAddressEqualityComparer : IEqualityComparer<IPAddress>
        {
            public bool Equals(IPAddress? x, IPAddress? y)
            {
                if (ReferenceEquals(x, y))
                {
                    return true;
                }

                if (x is null || y is null)
                {
                    return false;
                }

                return x.Equals(y);
            }

            public int GetHashCode(IPAddress obj)
            {
                return obj.GetHashCode();
            }
        }

        internal static void ApplyDnsConfigOnAppConfigLoad()
        {
            bool isEnabled = LauncherConfig.GetAppConfigValue("IsUseExternalDns");
            if (!isEnabled)
            {
                return;
            }

            string? settings = LauncherConfig.GetAppConfigValue("ExternalDnsAddresses");
            ParseDnsSettings(settings, out string[]? hosts, out DnsConnectionType connectionType);
            if (hosts == null || hosts.Length == 0)
            {
                return;
            }

            UseExternalDns(hosts, connectionType);
        }
    }

    internal static class ResourceRecordCollectionExtension
    {
        internal static IEnumerable<ResourceRecord> MergeWithZigZag(this ResourceRecordCollection records,
                                                                    ResourceRecordCollection another)
        {
            ResourceRecordCollection.Enumerator enumeratorOne = records.GetEnumerator();
            ResourceRecordCollection.Enumerator enumeratorTwo = another.GetEnumerator();

            try
            {
            Enumerate:
                bool isGetOne = enumeratorOne.MoveNext();
                bool isGetTwo = enumeratorTwo.MoveNext();

                // ReSharper disable once ConvertIfStatementToSwitchStatement
                if (!isGetOne && !isGetTwo)
                    yield break;

                if (isGetOne)
                    yield return enumeratorOne.Current;

                if (isGetTwo)
                    yield return enumeratorTwo.Current;

                goto Enumerate;
            }
            finally
            {
                enumeratorOne.Dispose();
                enumeratorTwo.Dispose();
            }
        }

        internal static IEnumerable<(ResourceRecord Record, uint TimeToLive)> EnumerateAOrAAAARecordOnly(
            this IEnumerable<ResourceRecord> records)
        {
            foreach (ResourceRecord record in records)
            {
                if (record.Type is DnsType.A or DnsType.AAAA)
                {
                    yield return (record, record.TimeToLive);
                }
            }
        }
    }
}