#nullable enable
using Sentry;
using Sentry.Extensibility;
using Sentry.Infrastructure;
using System;
using System.Text;
using static Hi3Helper.Logger;

namespace Hi3Helper.SentryHelper;

public abstract class LogToNativeLogger : IDiagnosticLogger
{
    // Based on https://github.com/getsentry/sentry-dotnet/blob/3d461f5bde60d4108d92384cbbb3addf7747147d/src/Sentry/Infrastructure/DiagnosticLogger.cs
    private readonly SentryLevel _minimalLevel;

    /// <summary>
    /// Creates a new instance of <see cref="DiagnosticLogger"/>.
    /// </summary>
    protected LogToNativeLogger(SentryLevel minimalLevel) => _minimalLevel = minimalLevel;

    /// <summary>
    /// Whether the logger is enabled to the defined level.
    /// </summary>
    public bool IsEnabled(SentryLevel level) => level >= _minimalLevel;

    /// <summary>
    /// Log message with level, exception and parameters.
    /// </summary>
    public void Log(SentryLevel logLevel, string message, Exception? exception = null, params object?[] args)
    {
        // Note, linefeed and newline chars are removed to guard against log injection attacks.
        // See https://github.com/getsentry/sentry-dotnet/security/code-scanning/5

        // Important: Only format the string if there are args passed.
        // Otherwise, a pre-formatted string that contains braces can cause a FormatException.
        var text = args.Length == 0 ? message : string.Format(message, args);
        var formattedMessage = ScrubNewlines(text);

        var completeMessage = exception == null
            ? $"{formattedMessage}"
            : $"{formattedMessage}{Environment.NewLine}{exception}";

        LogMessage(completeMessage, logLevel);
    }

    /// <summary>
    /// Writes a formatted message to the log.
    /// </summary>
    /// <param name="message">The complete message, ready to be logged.</param>
    /// <param name="logLevel">The level of the log.</param>
    protected abstract void LogMessage(string message, SentryLevel logLevel);

    private static string ScrubNewlines(string s)
    {
        // Replaces "\r", "\n", or "\r\n" with a single space in one pass (and trims the end result)

        var sb = new StringBuilder(s.Length);

        for (var i = 0; i < s.Length; i++)
        {
            var c = s[i];
            switch (c)
            {
                case '\r':
                    sb.Append(' ');
                    if (i < s.Length - 1 && s[i + 1] == '\n')
                    {
                        i++; // to prevent two consecutive spaces from "\r\n"
                    }
                    break;
                case '\n':
                    sb.Append(' ');
                    break;
                default:
                    sb.Append(c);
                    break;
            }
        }

        // trim end and return
        var len = sb.Length;
        while (sb[len - 1] == ' ')
        {
            len--;
        }

        return sb.ToString(0, len);
    }
}

public class CollapseLogger(SentryLevel minimalLevel) : LogToNativeLogger(minimalLevel)
{
    protected override void LogMessage(string message, SentryLevel logLevel)
    {
        switch (logLevel)
        {
            case SentryLevel.Debug:
                message = $"DEBUG: {message}";
                LogWriteLine(message, LogType.Sentry);
                break;

            case SentryLevel.Warning:
                message = $"WARNING: {message}";
                LogWriteLine(message, LogType.Sentry, true);
                break;
            case SentryLevel.Fatal:
                message = $"FATAL: {message}";
                LogWriteLine(message, LogType.Sentry, true);
                break;
            
            case SentryLevel.Error:
                message = $"ERROR: {message}";
                LogWriteLine(message, LogType.Sentry, true);
                break;

            case SentryLevel.Info:
            default:
                LogWriteLine(message, LogType.Sentry);
                break;
        }
    }
}