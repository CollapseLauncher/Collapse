using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;

namespace Hi3Helper
{
#nullable enable
    /// <summary>
    /// Provides helper methods for managing instances of <see cref="ILogger"/>.
    /// </summary>
    public static class ILoggerHelper
    {
        private static readonly Dictionary<string, ILogger> ILoggerCache = new();

        /// <summary>
        /// Retrieves an instance of <see cref="ILogger"/> from the cache based on the given prefix.
        /// </summary>
        /// <param name="prefix">The prefix to be used for the logger instance. If no prefix is provided, an empty string is used.</param>
        /// <returns>An instance of <see cref="ILogger"/> associated with the specified prefix.</returns>
        public static ILogger GetILogger(string prefix = "")
        {
            // Lock the cache to ensure thread safety when accessing the dictionary
            lock (ILoggerCache)
            {
                // Use TryGetValue for thread-safe read operation
                if (ILoggerCache.TryGetValue(prefix, out var logger))
                {
                    return logger; // Return the cached logger instance if it exists
                }

                // Create a new logger instance and cache it
                logger               = new LoggerWrapper(prefix);
                ILoggerCache[prefix] = logger;
                return logger;
            }
        }

        private class LoggerWrapper(string prefix = "") : ILogger
        {
            public IDisposable BeginScope<TState>(TState state)
                where TState : notnull => null!;

            public bool IsEnabled(LogLevel logLevel) => true;

            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
                                    Func<TState, Exception?, string> formatter)
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
                                    #if DEBUG
                                        LogType.Debug => true,
                                    #else
                                        LogType.Debug => false,
                                    #endif
                                        _ => false
                                    };

                string message = formatter(state, exception);
                Logger.LogWriteLine($"{(!string.IsNullOrEmpty(prefix) ? $"[{prefix}] " : "")}{message}", logType,
                                    isWriteToLog);
            }
        }
    }
}