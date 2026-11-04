using Hi3Helper.Log;
using Sentry;
using Sentry.Extensibility;
using static Hi3Helper.Log.Logger;

namespace Hi3Helper.SentryHelper
{
    #nullable enable
    public class SentryEventProcessor : ISentryEventProcessor
    {
        // This is to follow the default implementation of the interface
        // ReSharper disable once ReturnTypeCanBeNotNullable
        public SentryEvent? Process(SentryEvent @event)
        {
            string log =
                $"Sentry event caught! ID: {@event.EventId}, Level: {@event.Level}, Source: {@event.Exception?.Source}" +
                $"{(string.IsNullOrEmpty(@event.Message?.Formatted) ? "" : $"\r\n{@event.Message?.Formatted}")}";
            WriteLine(log, LogType.Sentry, true); // Use our logger
            return @event; // Pipe em back up
        }
    }
}