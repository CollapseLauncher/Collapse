using Sentry;
using Sentry.Extensibility;
using static Hi3Helper.Logger;

namespace Hi3Helper.SentryHelper
{
    #nullable enable
    public class SentryEventProcessor : ISentryEventProcessor
    {
        // This is to follow the default implementation of the interface
        // ReSharper disable once ReturnTypeCanBeNotNullable
        public SentryEvent? Process(SentryEvent @event)
        {
            var log =
                $"Sentry event caught! ID: {@event.EventId}, Level: {@event.Level}, Source: {@event.Exception?.Source}" +
                $"{(string.IsNullOrEmpty(@event.Message?.Formatted) ? "" : $"\r\n{@event.Message?.Formatted}")}";
            LogWriteLine(log, LogType.Sentry, true); // Use our logger
            return @event; // Pipe em back up
        }
    }
}