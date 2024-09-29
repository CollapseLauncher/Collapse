#if USEVELOPACK
using Hi3Helper;
using Microsoft.Extensions.Logging;
using System;

namespace CollapseLauncher.Helper
{
    internal static class ILoggerHelper
    {
        internal static ILogger CreateCollapseILogger() => new CollapseILoggerWrapper();
    }

    public class CollapseILoggerWrapper : ILogger
    {
        internal CollapseILoggerWrapper() { }

        public IDisposable BeginScope<TState>(TState state)
            where TState : notnull => default;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter)
        {
            LogType logType = logLevel switch
            {
                LogLevel.Trace => LogType.Debug,
                LogLevel.Debug => LogType.Debug,
                LogLevel.Information => LogType.Default,
                LogLevel.Warning => LogType.Warning,
                LogLevel.Error => LogType.Error,
                LogLevel.Critical => LogType.Error,
                LogLevel.None => LogType.NoTag,
                _ => LogType.Default
            };

            bool isWriteToLog = logType switch
            {
                LogType.Error => true,
                LogType.Warning => true,
                LogType.Debug => true,
                _ => false
            };

            string message = formatter(state, exception);
            Logger.LogWriteLine(message, logType, isWriteToLog);
        }
    }
}
#endif