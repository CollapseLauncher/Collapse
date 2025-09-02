#nullable enable
using Sentry;
using Sentry.Extensibility;
using Sentry.Infrastructure;
using System;
using System.Buffers;
using static Hi3Helper.Logger;
// ReSharper disable CheckNamespace

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
        string text = args.Length == 0 ? message : string.Format(message, args);
        string formattedMessage = ScrubNewlines(text);

        string completeMessage = exception == null
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

    private static readonly SearchValues<char> NewLineFeed = SearchValues.Create("\r\n");

    private static string ScrubNewlines(string s)
    {
        if (s.Length == 0)
        {
            return s;
        }

        const int  maxStackallocBufferSize = 1 << 10;
        const char spaceChar               = ' ';

        char[]? poolBuffer = s.Length > maxStackallocBufferSize ? ArrayPool<char>.Shared.Rent(s.Length) : null;
        scoped Span<char> buffer = poolBuffer ?? stackalloc char[s.Length];

        try
        {
            int                written = 0;
            ReadOnlySpan<char> sAsSpan = s;

            // Remove line feed characters: "\r", "\n" or both by using SplitAny extension.
            foreach (Range newLineFeed in sAsSpan.SplitAny(NewLineFeed))
            {
                ReadOnlySpan<char> curSpan = sAsSpan[newLineFeed];

                // To prevent two consecutive spaces. In this way, we don't need to check "\r\n" as
                // it's already trimmed by the SplitAny extension.
                if (curSpan.IsEmpty)
                {
                    continue;
                }

                curSpan.CopyTo(buffer[written..]);   // Do block copy instead of appending characters one by one.
                written           += curSpan.Length; // Advance the length of the buffer to write into.
                buffer[written++] =  spaceChar;      // Append space and advance the length.
            }

            // If length is 0, then return an empty string.
            if (written == 0)
            {
                return string.Empty;
            }

            // Trim trailing space character by subtracting the length.
            while (written > 0 &&
                   buffer[written - 1] == spaceChar)
            {
                --written;
            }

            // Write string from the buffer with specified length.
            return new string(buffer[..written]);
        }
        finally
        {
            if (poolBuffer != null)
            {
                ArrayPool<char>.Shared.Return(poolBuffer);
            }
        }
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