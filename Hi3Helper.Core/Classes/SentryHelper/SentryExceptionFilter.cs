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
        private static readonly HashSet<SocketError> SocketExceptionFilters = 
        [
            SocketError.NetworkDown,           // 10050
            SocketError.NetworkUnreachable,    // 10051
            SocketError.NetworkReset,          // 10052
            SocketError.ConnectionAborted,     // 10053
            SocketError.ConnectionReset,       // 10054
            SocketError.TimedOut,              // 10060
            SocketError.ConnectionRefused,     // 10061
            SocketError.HostUnreachable,       // 10065
            SocketError.NoBufferSpaceAvailable,// 10055
            SocketError.HostNotFound,          // 11001
            SocketError.TryAgain,              // 11002
            SocketError.NoRecovery,            // 11003
            SocketError.NoData                 // 11004
        ];

        private static readonly HashSet<string> HttpExceptionFilters =
        [
            "net_http_client_execution_error", // General HTTP client execution error.
            "net_http_request_aborted", // HTTP request was aborted by the client.
            "net_http_timeout_error", // HTTP request timed out.
            "net_http_connection_closed", // Connection closed unexpectedly.
            "net_http_name_resolution_failure"
        ];

        private static readonly HashSet<HttpRequestError> HttpRequestErrorFilters =
        [
            HttpRequestError.NameResolutionError, // DNS resolution failed.
            HttpRequestError.SecureConnectionError // TLS/SSL handshake failure.
        ];
        
        private static readonly HashSet<int> DiskFullHResults =
        [
            unchecked((int)0x80070070), // ERROR_DISK_FULL
            unchecked((int)0x80070027)
        ];
        
        private static readonly HashSet<string> NullReferenceFilters =
        [
            "DB_001", // DB Uri not set
            "DB_002", // DB Token not set
            "DB_003"  // Invalid token
        ];

        public bool Filter(Exception ex)
        {
            var returnValue = ex switch
                              {
                                  HttpRequestException httpEx => IsHttpExceptionFiltered(httpEx),
                                  IOException ioEx => IsIoExceptionFiltered(ioEx),
                                  SocketException socketEx => SocketExceptionFilters.Contains(socketEx.SocketErrorCode),
                                  NullReferenceException => NullReferenceFilters.Any(f => ex.Message.Contains(f)),
                                  _ => false
                              };

            if (returnValue)
            {
                Logger.LogWriteLine("[Sentry] Filtered exception: " + ex.Message, LogType.Sentry);
            }
            
            return returnValue;
        }

        private static bool IsHttpExceptionFiltered(HttpRequestException httpEx) =>
            HttpRequestErrorFilters.Contains(httpEx.HttpRequestError)
            || (httpEx.InnerException is SocketException socketEx &&
                SocketExceptionFilters.Contains(socketEx.SocketErrorCode))
            || HttpExceptionFilters.Any(f => httpEx.Message.Contains(f));

        private static bool IsIoExceptionFiltered(IOException ioEx) =>
            DiskFullHResults.Contains(ioEx.HResult)
            || (ioEx.InnerException is SocketException socketEx &&
                SocketExceptionFilters.Contains(socketEx.SocketErrorCode));
    }
}