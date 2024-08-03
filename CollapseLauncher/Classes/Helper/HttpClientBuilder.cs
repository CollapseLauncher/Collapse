using CollapseLauncher.Helper.Update;
using Hi3Helper.Shared.Region;
using System;
using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Runtime.InteropServices;

#nullable enable
namespace CollapseLauncher.Helper
{
    public class HttpClientBuilder
    {
        private const int _maxConnectionsDefault = 16;
        private const double _httpTimeoutDefault = 90; // in Seconds

        private bool IsUseProxy { get; set; } = true;
        private bool IsUseSystemProxy { get; set; } = true;
        private bool IsAllowHttpRedirections { get; set; } = false;
        private bool IsAllowHttpCookies { get; set; } = false;
        private bool IsAllowUntrustedCert { get; set; } = false;

        private int MaxConnections { get; set; } = _maxConnectionsDefault;
        private DecompressionMethods DecompressionMethod { get; set; } = DecompressionMethods.All;
        private WebProxy? ExternalProxy { get; set; }
        private Version HttpProtocolVersion { get; set; } = HttpVersion.Version30;
        private string? HttpUserAgent { get; set; } = GetDefaultUserAgent();
        private HttpVersionPolicy HttpProtocolVersionPolicy { get; set; } = HttpVersionPolicy.RequestVersionOrLower;
        private TimeSpan HttpTimeout { get; set; } = TimeSpan.FromSeconds(_httpTimeoutDefault);

        public HttpClientBuilder() { }

        public HttpClientBuilder UseProxy(bool isUseSystemProxy = true)
        {
            IsUseProxy = true;
            IsUseSystemProxy = isUseSystemProxy;
            return this;
        }

        private static string GetDefaultUserAgent()
        {
            bool isWindows10 = InnerLauncherConfig.m_isWindows11;
            Version operatingSystemVer = Environment.OSVersion.Version;
            FileVersionInfo winAppSDKVer = FileVersionInfo.GetVersionInfo("Microsoft.ui.xaml.dll");

            return $"Mozilla/5.0 (Windows NT {operatingSystemVer}; Win64; x64) "
                + $"{RuntimeInformation.FrameworkDescription.ToString().Replace(' ', '/')} (KHTML, like Gecko) "
                + $"Collapse/{LauncherUpdateHelper.LauncherCurrentVersionString}-{(LauncherConfig.IsPreview ? "Preview" : "Stable")} "
                + $"WinAppSDK/{winAppSDKVer.ProductVersion}";
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
            IsUseSystemProxy = false;

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

        public HttpClientBuilder AllowUntrustedCert(bool allowUntrustedCert = false)
        {
            IsAllowUntrustedCert = allowUntrustedCert;
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

        public HttpClientBuilder SetUserAgent(string? userAgent = null)
        {
            HttpUserAgent = userAgent;
            return this;
        }

        public HttpClient Create()
        {
            // Set the HttpClientHandler
            HttpClientHandler handler = new HttpClientHandler()
            {
                UseProxy = IsUseProxy || IsUseSystemProxy,
                MaxConnectionsPerServer = MaxConnections,
                AllowAutoRedirect = IsAllowHttpRedirections,
                UseCookies = IsAllowHttpCookies,
                AutomaticDecompression = DecompressionMethod,
                ClientCertificateOptions = ClientCertificateOption.Manual
            };

            // Toggle for allowing untrusted cert
            if (IsAllowUntrustedCert)
                handler.ServerCertificateCustomValidationCallback = (_, _, _, _) => true;

            // Set if the external proxy is set
            if (!IsUseSystemProxy && ExternalProxy != null)
                handler.Proxy = ExternalProxy;

            // Create the HttpClient instance
            HttpClient client = new HttpClient(handler)
            {
                Timeout = HttpTimeout,
                DefaultRequestVersion = HttpProtocolVersion,
                DefaultVersionPolicy = HttpProtocolVersionPolicy
            };

            // Set User-agent
            if (!string.IsNullOrEmpty(HttpUserAgent))
                client.DefaultRequestHeaders.Add("User-Agent", HttpUserAgent);

            return client;
        }

        public HttpClientBuilder UseLauncherConfig(int maxConnections = 16)
        {
            bool lIsUseProxy = LauncherConfig.GetAppConfigValue("IsUseProxy").ToBool();
            bool lIsAllowHttpRedirections = LauncherConfig.GetAppConfigValue("IsAllowHttpRedirections").ToBool();
            bool lIsAllowHttpCookies = LauncherConfig.GetAppConfigValue("IsAllowHttpCookies").ToBool();
            bool lIsAllowUntrustedCert = LauncherConfig.GetAppConfigValue("IsAllowUntrustedCert").ToBool();

            string? lHttpProxyUrl = LauncherConfig.GetAppConfigValue("HttpProxyUrl").ToString();
            string? lHttpProxyUsername = LauncherConfig.GetAppConfigValue("HttpProxyUsername").ToString();
            string? lHttpProxyPassword = LauncherConfig.GetAppConfigValue("HttpProxyPassword").ToString();
            int lHttpClientConnections = maxConnections;

            double lHttpClientTimeout = LauncherConfig.GetAppConfigValue("HttpClientTimeout").ToDouble();

            bool isHttpProxyUrlValid = Uri.TryCreate(lHttpProxyUrl, UriKind.Absolute, out Uri? lProxyUri);

            this.UseProxy();

            if (lIsUseProxy && isHttpProxyUrlValid && lProxyUri != null)
                this.UseExternalProxy(lProxyUri, lHttpProxyUsername, lHttpProxyPassword);

            this.AllowUntrustedCert(lIsAllowUntrustedCert);
            this.AllowCookies(lIsAllowHttpCookies);
            this.AllowRedirections(lIsAllowHttpRedirections);

            this.SetTimeout(lHttpClientTimeout);
            this.SetMaxConnection(lHttpClientConnections);

            return this;
        }
    }
}
#nullable restore