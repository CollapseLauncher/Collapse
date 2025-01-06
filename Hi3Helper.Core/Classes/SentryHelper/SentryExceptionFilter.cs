using Sentry.Extensibility;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Sockets;

namespace Hi3Helper.SentryHelper
{
    public class NetworkException : IExceptionFilter
    {
        private static readonly HashSet<int> SocketExceptionFilters = new()
        {
            10050, // WSAENETDOWN - The network subsystem has failed.
            10051, // WSAENETUNREACH - The network can't be reached from this host at this time.
            10052, // WSAENETRESET - The connection has been broken due to keep-alive activity detecting a failure while the operation was in progress.
            10053, // WSAECONNABORTED - The connection was aborted due to a network error.
            10054, // WSAECONNRESET - The connection was reset by the remote peer.
            10060, // WSAETIMEDOUT - The connection has been dropped because of a network failure or because the peer system failed to respond.
            10061, // WSAECONNREFUSED - The connection was refused by the remote host.
            10065, // WSAEHOSTUNREACH - The host is unreachable.
            10055, // WSAENOBUFS - No buffer space available (resource issue on the client side).
            11001, // WSAHOST_NOT_FOUND - Host not found (DNS resolution error).
            11002, // WSATRY_AGAIN - Non-authoritative host not found (transient DNS error).
            11003, // WSANO_RECOVERY - Non-recoverable error during a DNS query.
            11004  // WSANO_DATA - Valid name, no data record of requested type (DNS error).
        };

        private static readonly HashSet<string> HttpExceptionFilters = new()
        {
            "net_http_client_execution_error", // General HTTP client execution error.
            "net_http_request_aborted", // HTTP request was aborted by the client.
            "net_http_timeout_error", // HTTP request timed out.
            "net_http_connection_closed", // Connection closed unexpectedly.
            "net_http_name_resolution_failure" // DNS resolution failure for the HTTP client.
        };

        private static readonly HashSet<HttpRequestError> HttpRequestErrorFilters = new()
        {
            HttpRequestError.NameResolutionError, // DNS resolution failed.
            HttpRequestError.SecureConnectionError, // TLS/SSL handshake failure.
        };

        public bool Filter(Exception ex)
        {
            var returnValue = ex switch
                              {
                                  SocketException socketEx => SocketExceptionFilters.Contains(socketEx.ErrorCode),
                                  HttpRequestException httpEx => HttpRequestErrorFilters.Contains(httpEx.HttpRequestError) 
                                                                 || (httpEx.InnerException is SocketException socketEx && SocketExceptionFilters.Contains(socketEx.ErrorCode))
                                                                 || HttpExceptionFilters.Any(f => httpEx.Message.Contains(f)),
                                  _ => false
                              };

            if (returnValue)
            {
                Logger.LogWriteLine("[Sentry] Filtered exception: " + ex.Message, LogType.Sentry);
            }
            
            return returnValue;
        }
    }
}