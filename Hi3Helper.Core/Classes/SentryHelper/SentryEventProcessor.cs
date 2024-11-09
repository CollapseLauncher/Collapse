using Sentry;
using Sentry.Extensibility;
using static Hi3Helper.Logger;

namespace Hi3Helper.SentryHelper
{
    #nullable enable
    public class SentryEventProcessor : ISentryEventProcessor
    {
        public SentryEvent? Process(SentryEvent @event)
        {
            var log = $"Sentry event caught! ID: {@event.EventId}, Level: {@event.Level}\r\n{@event.Message?.Formatted}";
            LogWriteLine(log, LogType.Sentry, true); // Use our logger
            return @event; // Pipe em back up
        }
    }
}