using Sentry;
using System.Threading.Tasks;
using static Hi3Helper.Logger;
using static Sentry.SentrySdk;

namespace Hi3Helper.SentryHelper;

public class SentryLoggerExtension
{
    public static void DetachedIngestion(string line, LogType type = LogType.Default)
    {
        _ = Task.Run(() =>
                     {
                         IngestLog(line, type);
                     });
    }
    public static void IngestLog(string line, LogType type = LogType.Default)
    {
        if (!SentryHelper.IsEnabled) return;

        // If the line is null, do nothing.
        if (line == null) return;

        switch (type)
        {
            case LogType.GLC:
            case LogType.Scheme:
            case LogType.Default:
                Experimental.Logger.LogInfo(line);
                break;
            
            case LogType.Debug:
                Experimental.Logger.LogDebug(line);
                break;
            
            case LogType.Error:
                Experimental.Logger.LogError(line);
                break;
            
            case LogType.Warning:
                Experimental.Logger.LogWarning(line);
                break;
            
            case LogType.NoTag:
                Experimental.Logger.LogInfo($"[NoTag] {line}");
                break;

            case LogType.Sentry:  // Do not log Sentry logs to Sentry.
            case LogType.Game: // Do not log game logs to Sentry.
            default:
                break;
        }
    }
}