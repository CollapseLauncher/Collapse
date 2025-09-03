using CollapseLauncher.Helper.Update;
using Hi3Helper.Shared.Region;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Security;
using System.Runtime.InteropServices;
using System.Security;
using System.Threading;

// ReSharper disable StringLiteralTypo
// ReSharper disable UnusedMember.Global
// ReSharper disable CheckNamespace
// ReSharper disable StaticMemberInGenericType

#nullable enable
namespace CollapseLauncher.Helper
{
    public partial class HttpClientBuilder
    {
        protected const int    MaxConnectionsDefault = 32;
        protected const double HttpTimeoutDefault    = 90; // in Seconds

        protected static readonly Lock HttpClientBuilderSharedLock = new();
        protected                 bool IsUseProxy              { get; set; } = true;
        protected                 bool IsUseSystemProxy        { get; set; } = true;
        protected                 bool IsAllowHttpRedirections { get; set; }
        protected                 bool IsAllowHttpCookies      { get; set; }
        protected                 bool IsAllowUntrustedCert    { get; set; }

        protected int                  MaxConnections      { get; set; } = MaxConnectionsDefault;
        protected DecompressionMethods DecompressionMethod { get; set; } = DecompressionMethods.All;
        protected WebProxy?            ExternalProxy       { get; set; }

        protected Version           HttpProtocolVersion       { get; set; } = HttpVersion.Version30;
        protected string?           HttpUserAgent             { get; set; } = GetDefaultUserAgent();
        protected string?           HttpAuthHeader            { get; set; }
        protected HttpVersionPolicy HttpProtocolVersionPolicy { get; set; } = HttpVersionPolicy.RequestVersionOrLower;
        protected TimeSpan          HttpTimeout               { get; set; } = TimeSpan.FromSeconds(HttpTimeoutDefault);
        protected Uri?              HttpBaseUri               { get; set; }

        protected Dictionary<string, string?> HttpHeaders { get; } = new();

        private static string GetDefaultUserAgent()
        {
            Version operatingSystemVer = Environment.OSVersion.Version;

            return $"Mozilla/5.0 (Windows NT {operatingSystemVer}; Win64; x64) "
                   + $"{RuntimeInformation.FrameworkDescription.Replace(' ', '/')} (KHTML, like Gecko) "
                   + $"Collapse/{LauncherUpdateHelper.LauncherCurrentVersionString}-{(LauncherConfig.IsPreview ? "Preview" : "Stable")} "
                   + $"WinAppSDK/{LauncherConfig.WindowsAppSdkVersion}";
        }

        public static HttpClient CreateDefaultClient(int maxConnection = 32, bool isSkipDnsInit = false)
            => new HttpClientBuilder()
              .UseLauncherConfig()
              .Create();

        public HttpClientBuilder UseProxy(bool isUseSystemProxy = true)
        {
            IsUseProxy       = true;
            IsUseSystemProxy = isUseSystemProxy;
            return this;
        }

        public HttpClientBuilder UseExternalProxy(string host, string? username = null, SecureString? password = null)
        {
            // Try to create the Uri
            if (Uri.TryCreate(host, UriKind.Absolute, out Uri? hostUri))
            {
                return UseExternalProxy(hostUri, username, password);
            }

            IsUseProxy       = false;
            IsUseSystemProxy = false;
            ExternalProxy    = null;
            return this;
        }

        public HttpClientBuilder UseExternalProxy(Uri hostUri, string? username = null, SecureString? password = null)
        {
            IsUseSystemProxy = false;

            // Initialize the proxy host
            ExternalProxy =
                !string.IsNullOrEmpty(username)
             && password != null ?
                  new WebProxy(hostUri, true, null, new NetworkCredential(username, password))
                : new WebProxy(hostUri, true);

            return this;
        }

        public HttpClientBuilder UseLauncherConfig(int maxConnections = MaxConnectionsDefault, bool skipDnsInit = false)
        {
            bool lIsUseProxy = LauncherConfig.GetAppConfigValue("IsUseProxy");
            bool lIsAllowHttpRedirections = LauncherConfig.GetAppConfigValue("IsAllowHttpRedirections");
            bool lIsAllowHttpCookies = LauncherConfig.GetAppConfigValue("IsAllowHttpCookies");
            bool lIsAllowUntrustedCert = LauncherConfig.GetAppConfigValue("IsAllowUntrustedCert");

            string? lHttpProxyUrl = LauncherConfig.GetAppConfigValue("HttpProxyUrl");
            string? lHttpProxyUsername = LauncherConfig.GetAppConfigValue("HttpProxyUsername");
            string? lHttpProxyPassword = LauncherConfig.GetAppConfigValue("HttpProxyPassword");
            double lHttpClientTimeout = LauncherConfig.GetAppConfigValue("HttpClientTimeout");

            bool isHttpProxyUrlValid = Uri.TryCreate(lHttpProxyUrl, UriKind.Absolute, out Uri? lProxyUri);

            UseProxy();

            if (lIsUseProxy && isHttpProxyUrlValid && lProxyUri != null)
            {
                using SecureString? proxyPassword = SimpleProtectData.UnprotectStringAsSecureString(lHttpProxyPassword);
                UseExternalProxy(lProxyUri, lHttpProxyUsername, proxyPassword);
            }

            AllowUntrustedCert(lIsAllowUntrustedCert);
            AllowCookies(lIsAllowHttpCookies);
            AllowRedirections(lIsAllowHttpRedirections);

            SetTimeout(lHttpClientTimeout);
            SetMaxConnection(maxConnections);

            return this;
        }

        public HttpClientBuilder SetMaxConnection(int maxConnections = MaxConnectionsDefault)
        {
            if (maxConnections < 2)
                maxConnections = 2;

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

        public HttpClientBuilder SetAuthHeader(string authHeader)
        {
            if (!string.IsNullOrEmpty(authHeader)) HttpAuthHeader = authHeader;
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

        public HttpClientBuilder SetTimeout(double fromSeconds = HttpTimeoutDefault)
        {
            if (double.IsNaN(fromSeconds) || double.IsInfinity(fromSeconds))
                fromSeconds = HttpTimeoutDefault;

            return SetTimeout(TimeSpan.FromSeconds(fromSeconds));
        }

        public HttpClientBuilder SetTimeout(TimeSpan? timeout = null)
        {
            timeout ??= TimeSpan.FromSeconds(HttpTimeoutDefault);
            HttpTimeout = timeout.Value;
            return this;
        }

        public HttpClientBuilder SetUserAgent(string? userAgent = null)
        {
            HttpUserAgent = userAgent;
            return this;
        }

        public HttpClientBuilder SetBaseUrl(string baseUrl)
        {
            Uri baseUri = new Uri(baseUrl);
            return SetBaseUrl(baseUri);
        }

        public HttpClientBuilder SetBaseUrl(Uri baseUrl)
        {
            HttpBaseUri = baseUrl;
            return this;
        }

        public HttpClientBuilder AddHeader(string key, string? value)
        {
            // Throw if the key is null or empty
            ArgumentException.ThrowIfNullOrEmpty(key, nameof(key));

            // Try check if the key is user-agent. If the user-agent has already
            // been set, then override the value from HttpUserAgent property
            if (key.Equals("User-Agent", StringComparison.OrdinalIgnoreCase))
            {
                HttpUserAgent = null;
            }

            // If the key already exist, then override the previous one.
            // Otherwise, add the new key-value pair
            // ReSharper disable once RedundantDictionaryContainsKeyBeforeAdding
            if (!HttpHeaders.TryAdd(key, value))
            {
                HttpHeaders[key] = value;
            }

            // Return the instance of the builder
            return this;
        }

        public virtual HttpClient Create()
        {
            SocketsHttpHandler socketsHttpHandler = new();

            // Set the properties
            socketsHttpHandler.UseProxy                       = IsUseProxy || IsUseSystemProxy;
            socketsHttpHandler.MaxConnectionsPerServer        = MaxConnections;
            socketsHttpHandler.AllowAutoRedirect              = IsAllowHttpRedirections;
            socketsHttpHandler.UseCookies                     = IsAllowHttpCookies;
            socketsHttpHandler.AutomaticDecompression         = DecompressionMethod;
            socketsHttpHandler.EnableMultipleHttp2Connections = true;
            socketsHttpHandler.EnableMultipleHttp3Connections = true;

            // Toggle for allowing untrusted cert
            if (IsAllowUntrustedCert)
            {
                SslClientAuthenticationOptions sslOptions = new()
                {
                    RemoteCertificateValidationCallback = delegate { return true; }
                };
                socketsHttpHandler.SslOptions = sslOptions;
            }

            // Set if the external proxy is set
            if (!IsUseSystemProxy && ExternalProxy != null)
                socketsHttpHandler.Proxy = ExternalProxy;

            // Set Connect callback if the External DNS setting is available
            if (IsUseExternalDns && SharedExternalDnsServers != null)
                socketsHttpHandler.ConnectCallback = ExternalDnsConnectCallback;

            // Create the HttpClient instance
            HttpClient client = new(socketsHttpHandler, false)
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

            // Add Http Auth Header
            if (!string.IsNullOrEmpty(HttpAuthHeader))
                client.DefaultRequestHeaders.Add("Authorization", HttpAuthHeader);

            // Add other headers
            foreach (KeyValuePair<string, string?> header in HttpHeaders)
            {
                _ = client.DefaultRequestHeaders.TryAddWithoutValidation(header.Key, header.Value);
            }

            return client;
        }
    }
}