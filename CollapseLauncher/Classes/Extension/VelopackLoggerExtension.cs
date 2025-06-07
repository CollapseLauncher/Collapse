using Microsoft.Extensions.Logging;
using System;
using Velopack.Logging;

namespace CollapseLauncher.Classes.Extension
{
    public class VelopackLoggerAdaptor(ILogger logger) : IVelopackLogger
    {
        private readonly ILogger _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        public void Log(VelopackLogLevel logLevel, string message, Exception exception)
        {
            switch (logLevel)
            {
                case VelopackLogLevel.Trace:
                    _logger.LogTrace(message);
                    break;
                case VelopackLogLevel.Debug:
                    _logger.LogDebug(exception, message);
                    break;
                case VelopackLogLevel.Information:
                    _logger.LogInformation(message);
                    break;
                case VelopackLogLevel.Warning:
                    _logger.LogWarning(exception, message);
                    break;
                case VelopackLogLevel.Error:
                    _logger.LogError(exception, message);
                    break;
                case VelopackLogLevel.Critical:
                    _logger.LogCritical(exception, message);
                    break;
                default:
                    _logger.LogError(exception ?? new Exception("An unknown log level was encountered."), message);
                    break;
            }
        }
    }

    public static class LoggerExtensions
    {
        public static IVelopackLogger ToVelopackLogger(this ILogger logger)
        {
            return new VelopackLoggerAdaptor(logger);
        }
    }
}