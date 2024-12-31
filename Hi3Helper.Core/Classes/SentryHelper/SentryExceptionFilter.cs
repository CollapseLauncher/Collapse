using Microsoft.Extensions.Logging;
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
        private static readonly HashSet<int> SocketExceptionFilters =
        [
            10050, // WSAENETDOWN - The network subsystem has failed.
            10051, // WSAENETUNREACH - The network can't be reached from this host at this time.
            10052, // WSAENETRESET - The connection has been broken due to keep-alive activity detecting a failure while the operation was in progress.
            10053, // WSAECONNABORTED - The connection was aborted due to a network error.
            10054, // WSAECONNRESET - The connection was reset by the remote peer.
            10060, // WSAETIMEDOUT - The connection has been dropped because of a network failure or because the peer system failed to respond.
            10061, // WSAECONNREFUSED - The connection was refused by the remote host.
            10065 // WSAEHOSTUNREACH - The host is unreachable.
        ];
        
        private static readonly HashSet<string> HttpExceptionFilters =
        [
            "net_http_client_execution_error",
            "net_http_request_aborted"
        ];
        
        private static readonly HashSet<HttpRequestError> HttpRequestErrorFilters =
        [
            HttpRequestError.NameResolutionError,
            HttpRequestError.SecureConnectionError
        ];

        public bool Filter(Exception ex)
        {
            var returnValue = ex switch
                              {
                                  SocketException socketEx => SocketExceptionFilters.Contains(socketEx.ErrorCode),
                                  HttpRequestException httpEx => HttpRequestErrorFilters.Contains(httpEx.HttpRequestError)
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