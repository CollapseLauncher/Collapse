using CollapseLauncher.Helper.Update;
using Hi3Helper.Shared.Region;
using System;
using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Net.Security;
using System.Runtime.InteropServices;

#nullable enable
namespace CollapseLauncher.Helper
{
    public class HttpClientBuilder : HttpClientBuilder<HttpClientHandler>
    {
        public HttpClientBuilder() : base() { }
    }

    public class HttpClientBuilder<THandler> where THandler : HttpMessageHandler, new()
    {
        private const int _maxConnectionsDefault = 32;
        private const double _httpTimeoutDefault = 90; // in Seconds

        private bool IsUseProxy { get; set; } = true;
        private bool IsUseSystemProxy { get; set; } = true;
        private bool IsAllowHttpRedirections { get; set; }
        private bool IsAllowHttpCookies { get; set; }
        private bool IsAllowUntrustedCert { get; set; }

        private int MaxConnections { get; set; } = _maxConnectionsDefault;
        private DecompressionMethods DecompressionMethod { get; set; } = DecompressionMethods.All;
        private WebProxy? ExternalProxy { get; set; }
        private Version HttpProtocolVersion { get; set; } = HttpVersion.Version30;
        private string? HttpUserAgent { get; set; } = GetDefaultUserAgent();
        private HttpVersionPolicy HttpProtocolVersionPolicy { get; set; } = HttpVersionPolicy.RequestVersionOrLower;
        private TimeSpan HttpTimeout { get; set; } = TimeSpan.FromSeconds(_httpTimeoutDefault);
        private Uri? HttpBaseUri { get; set; }

        public HttpClientBuilder<THandler> UseProxy(bool isUseSystemProxy = true)
        {
            IsUseProxy = true;
            IsUseSystemProxy = isUseSystemProxy;
            return this;
        }

        private static string GetDefaultUserAgent()
        {
            Version operatingSystemVer = Environment.OSVersion.Version;
            FileVersionInfo winAppSDKVer = FileVersionInfo.GetVersionInfo("Microsoft.ui.xaml.dll");

            return $"Mozilla/5.0 (Windows NT {operatingSystemVer}; Win64; x64) "
                + $"{RuntimeInformation.FrameworkDescription.Replace(' ', '/')} (KHTML, like Gecko) "
                + $"Collapse/{LauncherUpdateHelper.LauncherCurrentVersionString}-{(LauncherConfig.IsPreview ? "Preview" : "Stable")} "
                + $"WinAppSDK/{winAppSDKVer.ProductVersion}";
        }

        public HttpClientBuilder<THandler> UseExternalProxy(string host, string? username = null, string? password = null)
        {
            // Try to create the Uri
            if (!Uri.TryCreate(host, UriKind.Absolute, out Uri? hostUri))
            {
                IsUseProxy = false;
                IsUseSystemProxy = false;
                ExternalProxy = null;
                return this;
            }

            return UseExternalProxy(hostUri, username, password);
        }

        public HttpClientBuilder<THandler> UseExternalProxy(Uri hostUri, string? username = null, string? password = null)
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

        public HttpClientBuilder<THandler> UseLauncherConfig(int maxConnections = _maxConnectionsDefault)
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

        public HttpClientBuilder<THandler> SetMaxConnection(int maxConnections = _maxConnectionsDefault)
        {
            MaxConnections = maxConnections;
            return this;
        }

        public HttpClientBuilder<THandler> SetAllowedDecompression(DecompressionMethods decompressionMethods = DecompressionMethods.All)
        {
            DecompressionMethod = decompressionMethods;
            return this;
        }

        public HttpClientBuilder<THandler> AllowRedirections(bool allowRedirections = true)
        {
            IsAllowHttpRedirections = allowRedirections;
            return this;
        }

        public HttpClientBuilder<THandler> AllowCookies(bool allowCookies = true)
        {
            IsAllowHttpCookies = allowCookies;
            return this;
        }

        public HttpClientBuilder<THandler> AllowUntrustedCert(bool allowUntrustedCert = false)
        {
            IsAllowUntrustedCert = allowUntrustedCert;
            return this;
        }

        public HttpClientBuilder<THandler> SetHttpVersion(Version? version = null, HttpVersionPolicy versionPolicy = HttpVersionPolicy.RequestVersionOrLower)
        {
            if (version != null)
                HttpProtocolVersion = version;

            HttpProtocolVersionPolicy = versionPolicy;
            return this;
        }

        public HttpClientBuilder<THandler> SetTimeout(double fromSeconds = _httpTimeoutDefault)
        {
            return SetTimeout(TimeSpan.FromSeconds(fromSeconds));
        }

        public HttpClientBuilder<THandler> SetTimeout(TimeSpan? timeout = null)
        {
            timeout ??= TimeSpan.FromSeconds(_httpTimeoutDefault);
            HttpTimeout = timeout.Value;
            return this;
        }

        public HttpClientBuilder<THandler> SetUserAgent(string? userAgent = null)
        {
            HttpUserAgent = userAgent;
            return this;
        }

        public HttpClientBuilder<THandler> SetBaseUrl(string baseUrl)
        {
            Uri baseUri = new Uri(baseUrl);
            return SetBaseUrl(baseUri);
        }

        public HttpClientBuilder<THandler> SetBaseUrl(Uri baseUrl)
        {
            HttpBaseUri = baseUrl;
            return this;
        }

        public HttpClient Create()
        {
            // Create the instance of the handler
            THandler handler = new();

            // Set the features of each handlers
            if (typeof(THandler) == typeof(HttpClientHandler))
            {
                // Cast as HttpClientHandler
                HttpClientHandler? httpClientHandler = handler as HttpClientHandler;
                if (httpClientHandler == null)
                    throw new InvalidCastException("Cannot cast handler as HttpClientHandler");

                // Set the properties
                httpClientHandler.UseProxy = IsUseProxy || IsUseSystemProxy;
                httpClientHandler.MaxConnectionsPerServer = MaxConnections;
                httpClientHandler.AllowAutoRedirect = IsAllowHttpRedirections;
                httpClientHandler.UseCookies = IsAllowHttpCookies;
                httpClientHandler.AutomaticDecompression = DecompressionMethod;
                httpClientHandler.ClientCertificateOptions = ClientCertificateOption.Manual;

                // Toggle for allowing untrusted cert
                if (IsAllowUntrustedCert)
                    httpClientHandler.ServerCertificateCustomValidationCallback = delegate { return true; };

                // Set if the external proxy is set
                if (!IsUseSystemProxy && ExternalProxy != null)
                    httpClientHandler.Proxy = ExternalProxy;
            }
            else if (typeof(THandler) == typeof(SocketsHttpHandler))
            {
                // Cast as SocketsHttpHandler
                SocketsHttpHandler? socketsHttpHandler = handler as SocketsHttpHandler;
                if (socketsHttpHandler == null)
                    throw new InvalidCastException("Cannot cast handler as SocketsHttpHandler");

                // Set the properties
                socketsHttpHandler.UseProxy = IsUseProxy || IsUseSystemProxy;
                socketsHttpHandler.MaxConnectionsPerServer = MaxConnections;
                socketsHttpHandler.AllowAutoRedirect = IsAllowHttpRedirections;
                socketsHttpHandler.UseCookies = IsAllowHttpCookies;
                socketsHttpHandler.AutomaticDecompression = DecompressionMethod;
                socketsHttpHandler.EnableMultipleHttp2Connections = true;

                // Toggle for allowing untrusted cert
                if (IsAllowUntrustedCert)
                {
                    SslClientAuthenticationOptions sslOptions = new SslClientAuthenticationOptions
                    {
                        RemoteCertificateValidationCallback = delegate { return true; }
                    };
                    socketsHttpHandler.SslOptions = sslOptions;
                }

                // Set if the external proxy is set
                if (!IsUseSystemProxy && ExternalProxy != null)
                    socketsHttpHandler.Proxy = ExternalProxy;
            }
            else
            {
                throw new InvalidOperationException("Generic must be a member of HttpMessageHandler!");
            }

            // Create the HttpClient instance
            HttpClient client = new HttpClient(handler, false)
            {
                Timeout = HttpTimeout,
                DefaultRequestVersion = HttpProtocolVersion,
                DefaultVersionPolicy = HttpProtocolVersionPolicy,
                BaseAddress = HttpBaseUri,
                MaxResponseContentBufferSize = int.MaxValue
            };

            // Set User-agent
            if (!string.IsNullOrEmpty(HttpUserAgent))
                client.DefaultRequestHeaders.Add("User-Agent", HttpUserAgent);

            return client;
        }
    }
}