using CollapseLauncher.Helper.Metadata;
using Hi3Helper.Shared.Region;
using System;
using System.Net;
using System.Net.Http;
using System.Net.WebSockets;
using System.Security;

#nullable enable
namespace CollapseLauncher.Helper
{
    internal class HttpClientBuilder
    {
        private const int _maxConnectionsDefault = 16;
        private const double _httpTimeoutDefault = 90; // in Seconds

        private bool IsUseProxy { get; set; } = false;
        private bool IsUseSystemProxy { get; set; } = false;
        private bool IsAllowHttpRedirections { get; set; } = true;
        private bool IsAllowHttpCookies { get; set; } = true;

        private int  MaxConnections { get; set; } = _maxConnectionsDefault;
        private DecompressionMethods DecompressionMethod { get; set; } = DecompressionMethods.All;
        private WebProxy? ExternalProxy { get; set; }
        private Version HttpProtocolVersion { get; set; } = HttpVersion.Version30;
        private HttpVersionPolicy HttpProtocolVersionPolicy { get; set; } = HttpVersionPolicy.RequestVersionOrLower;
        private TimeSpan HttpTimeout { get; set; } = TimeSpan.FromSeconds(_httpTimeoutDefault);

        public HttpClientBuilder() { }

        public HttpClientBuilder UseProxy(bool isUseSystemProxy = false)
        {
            IsUseProxy = true;
            IsUseSystemProxy = isUseSystemProxy;
            return this;
        }

        public HttpClientBuilder UseExternalProxy(string host, string? username = null, string? password = null)
        {
            // Try create the Uri
            if (!Uri.TryCreate(host, UriKind.Absolute, out Uri? hostUri))
            {
                IsUseProxy = false;
                IsUseSystemProxy = false;
                ExternalProxy = null;
                return this;
            }

            return UseExternalProxy(hostUri, username, password);
        }

        public HttpClientBuilder UseExternalProxy(Uri hostUri, string? username = null, string? password = null)
        {
            // Throw if the proxy used is a default one
            if (IsUseSystemProxy) throw new InvalidOperationException("To use an external proxy, please set the \"IsUseSystemProxy\" to false on UseProxy()");

            // Initialize the proxy host
            ExternalProxy =
                !string.IsNullOrEmpty(username)
             && !string.IsNullOrEmpty(password) ?
                  new WebProxy(hostUri, true, null, new NetworkCredential(username, password))
                : new WebProxy(hostUri, true);

            return this;
        }

        public HttpClientBuilder SetMaxConnection(int maxConnections = _maxConnectionsDefault)
        {
            MaxConnections = maxConnections;
            return this;
        }

        public HttpClientBuilder SetAllowedDecompression(DecompressionMethods decompressionMethods = DecompressionMethods.All)
        {
            DecompressionMethod = decompressionMethods;
            return this;
        }

        public HttpClientBuilder AllowRedirections(bool allowRedirections = true)
        {
            IsAllowHttpRedirections = allowRedirections;
            return this;
        }

        public HttpClientBuilder AllowCookies(bool allowCookies = true)
        {
            IsAllowHttpCookies = allowCookies;
            return this;
        }

        public HttpClientBuilder SetHttpVersion(Version? version = null, HttpVersionPolicy versionPolicy = HttpVersionPolicy.RequestVersionOrLower)
        {
            if (version != null)
                HttpProtocolVersion = version;

            HttpProtocolVersionPolicy = versionPolicy;
            return this;
        }

        public HttpClientBuilder SetTimeout(double fromSeconds = _httpTimeoutDefault)
        {
            return SetTimeout(TimeSpan.FromSeconds(fromSeconds));
        }

        public HttpClientBuilder SetTimeout(TimeSpan? timeout = null)
        {
            timeout ??= TimeSpan.FromSeconds(_httpTimeoutDefault);
            HttpTimeout = timeout.Value;
            return this;
        }

        public HttpClient Create()
        {
            // Set the HttpClientHandler
            HttpClientHandler handler = new HttpClientHandler()
            {
                UseProxy = IsUseProxy,
                MaxConnectionsPerServer = MaxConnections,
                AllowAutoRedirect = IsAllowHttpRedirections,
                UseCookies = IsAllowHttpCookies,
                AutomaticDecompression = DecompressionMethod
            };
            
            // Set if the external proxy is set
            if (!IsUseSystemProxy && ExternalProxy != null)
                handler.Proxy = ExternalProxy;

            // Create the HttpClient instance
            return new HttpClient(handler)
            {
                Timeout = HttpTimeout,
                DefaultRequestVersion = HttpProtocolVersion,
                DefaultVersionPolicy = HttpProtocolVersionPolicy
            };
        }

        public HttpClient CreateBasedOnLauncherConfig()
        {
            bool lIsUseProxy = LauncherConfig.GetAppConfigValue("IsUseProxy").ToBool();
            bool lIsUseSystemProxy = LauncherConfig.GetAppConfigValue("IsUseSystemProxy").ToBool();
            bool lIsAllowHttpRedirections = LauncherConfig.GetAppConfigValue("IsAllowHttpRedirections").ToBool();
            bool lIsAllowHttpCookies = LauncherConfig.GetAppConfigValue("IsAllowHttpCookies").ToBool();

            string? lHttpProxyUrl = LauncherConfig.GetAppConfigValue("HttpProxyUrl").ToString();
            string? lHttpProxyUsername = LauncherConfig.GetAppConfigValue("HttpProxyUsername").ToString();
            string? lHttpProxyPassword = LauncherConfig.GetAppConfigValue("HttpProxyPassword").ToString();
            int lHttpClientConnections = LauncherConfig.GetAppConfigValue("HttpClientConnections").ToInt();

            double lHttpClientTimeout = LauncherConfig.GetAppConfigValue("HttpClientTimeout").ToDouble();

            bool isHttpProxyUrlValid = Uri.TryCreate(lHttpProxyUrl, UriKind.Absolute, out Uri? lProxyUri);

            // lHttpProxyPassword = DataCooker.ServeV3Data(lHttpProxyPassword);
            lIsUseSystemProxy = lIsUseSystemProxy && isHttpProxyUrlValid && !string.IsNullOrEmpty(lHttpProxyUsername)
                    && !string.IsNullOrEmpty(lHttpProxyPassword);

            if (lIsUseProxy)
                this.UseProxy(lIsUseSystemProxy);

            if (lIsUseSystemProxy && lProxyUri != null)
                this.UseExternalProxy(lProxyUri, lHttpProxyUsername, lHttpProxyPassword);

            this.SetTimeout(lHttpClientTimeout);
            this.SetMaxConnection(lHttpClientConnections);

            return this.Create();
        }
    }
}
#nullable restore