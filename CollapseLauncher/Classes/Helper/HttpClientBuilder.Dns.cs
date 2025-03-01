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
    public enum DnsConnectionType
    {
        DoH,
        DoT,
        Udp
    }

    public partial class HttpClientBuilder<THandler>
    {
        internal static readonly Dictionary<string, IPAddress[]> DnsServerTemplate = new(StringComparer.OrdinalIgnoreCase)
        {
            { "Google", [ IPAddress.Parse("8.8.8.8"), IPAddress.Parse("2001:4860:4860::8888"), IPAddress.Parse("8.8.4.4"), IPAddress.Parse("2001:4860:4860::8844") ] },
            { "Cloudflare", [ IPAddress.Parse("1.1.1.1"), IPAddress.Parse("2606:4700:4700::1111"), IPAddress.Parse("1.0.0.1"), IPAddress.Parse("2606:4700:4700::1001") ] }
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

        private static bool IsUseExternalDns { get; set; }

        private static NameServer[]? ExternalDnsServers { get; set; }

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

            if (string.IsNullOrEmpty(inputString))
            {
                GetDefault(out hosts);
                return;
            }

            if (TryParseDnsConnectionType(inputString, out connectionType) &&
                TryParseDnsHosts(inputString, false, out hosts))
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

            Span<Range>        delimitRanges    = stackalloc Range[2];
            int                delimitRangesLen = inputAsSpan.Split(delimitRanges, '|');

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

        public static bool TryParseDnsHosts(ReadOnlySpan<char> inputAsSpan, bool bypassCache, out string[]? hosts)
        {
            const string             hostSeparators   = ";:,#/@%";
            const StringSplitOptions hostSplitOptions = StringSplitOptions.RemoveEmptyEntries;

            if (inputAsSpan.IsEmpty)
            {
                hosts = [];
                return false;
            }

            Span<Range>        delimitRanges = stackalloc Range[2];
            _ = inputAsSpan.Split(delimitRanges, '|');
            
            ReadOnlySpan<char> inputHost           = inputAsSpan[delimitRanges[0]];
            Span<Range>        inputHostSplitRange = stackalloc Range[32]; // Set maximum as 32 entries

            int inputHostSplitLen = inputHost.Split(inputHostSplitRange, hostSeparators, hostSplitOptions);

            if (inputHostSplitLen == 0)
            {
                if (inputHost.IsEmpty)
                {
                    hosts = [];
                    return false;
                }

                Dictionary<string, IPAddress[]>.AlternateLookup<ReadOnlySpan<char>> templateLookup = DnsServerTemplate.GetAlternateLookup<ReadOnlySpan<char>>();

                if (inputHost[0] == '$' && templateLookup.ContainsKey(inputHost[1..]))
                {
                    hosts = [inputHost[0].ToString()];
                    return true;
                }

                if (!IPAddress.TryParse(inputHost, out _))
                {
                    EvaluateHostAndGetIp(inputHost, bypassCache, out IPAddress[]? hostsOut);
                    if (hostsOut?.Length == 0)
                    {
                        hosts = [];
                        return false;
                    }

                    hosts = hostsOut?
                           .Select(x => x.ToString())
                           .ToArray();
                    return true;
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
                    EvaluateHostAndGetIp(currentRange, bypassCache, out IPAddress[]? currentAsIps);
                    if (currentAsIps?.Length == 0)
                    {
                        continue;
                    }

                    hostRangeList.AddRange(currentAsIps?.Select(x => x.ToString()) ?? []);
                    continue;
                }

                hostRangeList.Add(currentRange.ToString());
            }

            if (hostRangeList.Count != 0)
            {
                hosts = hostRangeList.ToArray();
                return true;
            }

            hosts = [];
            return false;

            static void EvaluateHostAndGetIp(ReadOnlySpan<char> host, bool bypassCache, out IPAddress[]? addresses)
            {
                if (!bypassCache && TryGetCachedIp(host, out IPAddress[]? cachedIpAddress))
                {
                    addresses = cachedIpAddress;
                    return;
                }

                (IDNS_WITH_IPADDR Record, uint RecordTimeToLive)[] recordAddressEvaluate = Dns
                   .EnumerateIPAddressFromHost(host.ToString(),
                                               bypassCache,
                                               true,
                                               ILoggerHelper
                                                  .GetILogger("HttpClientBuilder<T>::ParseDnsSettings"))
                   .ToArray();

                if (recordAddressEvaluate.Length != 0)
                {
                    ConcurrentDictionary<string, IPAddress[]>.AlternateLookup<ReadOnlySpan<char>> resolveCacheLookup = DnsClientResolveCache.GetAlternateLookup<ReadOnlySpan<char>>();
                    ConcurrentDictionary<string, DateTimeOffset>.AlternateLookup<ReadOnlySpan<char>> ttlCacheLookup = DnsClientResolveTtlCache.GetAlternateLookup<ReadOnlySpan<char>>();

                    IPAddress[] recordAddress = recordAddressEvaluate
                                               .Select(x => x.Record.GetIPAddress())
                                               .ToArray();

                    if (!bypassCache)
                    {
                        uint recordAvgTtl = (uint)recordAddressEvaluate
                                                 .Select(x => (int)x.RecordTimeToLive)
                                                 .Average();

                        DateTimeOffset ttlOffset = DateTimeOffset.Now.AddSeconds(recordAvgTtl);
                        resolveCacheLookup.TryAdd(host, recordAddress);
                        ttlCacheLookup.TryAdd(host, ttlOffset);
                    }

                    addresses = recordAddress;
                    return;
                }

                addresses = [];
            }
        }

        public HttpClientBuilder<THandler> UseExternalDns(string[]? hosts = null, DnsConnectionType connectionType = DnsConnectionType.DoH)
        {
            if (hosts == null)
            {
                IsUseExternalDns = false;
                ExternalDnsServers = null;
                return this;
            }

            if (ExternalDnsServers != null && IsUseExternalDns)
            {
                return this;
            }

            List<NameServer> nameServerList = [];
            // ReSharper disable once LoopCanBeConvertedToQuery
            foreach (IPAddress currentHost in EnumerateHostAsIp(hosts).Distinct())
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
                ExternalDnsServers = null;
                return this;
            }

            IsUseExternalDns = true;
            ExternalDnsServers = nameServers;
            return this;
        }

        private static async ValueTask<Stream> ExternalDnsConnectCallback(
            SocketsHttpConnectionContext context, CancellationToken token)
        {
            if (ExternalDnsServers == null)
            {
                return await ConnectWithSystemDns(context, token);
            }

            return await ConnectWithExternalDns(ExternalDnsServers, context, token);
        }

        private static async ValueTask<(ResourceRecordCollection Ipv4, ResourceRecordCollection Ipv6)>
            GetFallbackQuery(string host, NameServer[] dnsNameServers, CancellationToken token)
        {
            Exception?                                            lastException = null;
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
                    DnsClient dnsClient = new(dnsNameLocal, DnsMessageOptions.Default);

                    DnsMessage dnsMessageIpv4 = await dnsClient.QueryAsync(hostLocal, DnsQueryType.A, DnsClass.IN, tokenLocal);
                    ResourceRecordCollection recordsIpv4 = dnsMessageIpv4.Answers;

                    DnsMessage dnsMessageIpv6 = await dnsClient.QueryAsync(hostLocal, DnsQueryType.AAAA, DnsClass.IN, tokenLocal);
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
        }

        private static bool TryGetCachedIp(ReadOnlySpan<char> host, out IPAddress[]? cachedIpAddress)
        {
            ConcurrentDictionary<string, IPAddress[]>.AlternateLookup<ReadOnlySpan<char>> resolveCacheLookup = DnsClientResolveCache.GetAlternateLookup<ReadOnlySpan<char>>();
            ConcurrentDictionary<string, DateTimeOffset>.AlternateLookup<ReadOnlySpan<char>> ttlCacheLookup = DnsClientResolveTtlCache.GetAlternateLookup<ReadOnlySpan<char>>();

            bool isCached    = resolveCacheLookup.TryGetValue(host, out cachedIpAddress);
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

        private static async ValueTask<Stream> ConnectWithExternalDns(NameServer[]                 dnsNameServers,
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

                await socket.ConnectAsync(cachedIpAddress, context.DnsEndPoint.Port, token);
                return new NetworkStream(socket, ownsSocket: true);
            }
            catch
            {
                socket.Dispose();
                throw;
            }
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
    }

    internal static class ResourceRecordCollectionExtension
    {
        internal static IEnumerable<ResourceRecord> MergeWithZigZag(this ResourceRecordCollection records,
                                                                    ResourceRecordCollection      another)
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
