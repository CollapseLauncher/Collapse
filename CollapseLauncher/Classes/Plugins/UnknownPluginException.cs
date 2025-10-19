using System;

#nullable enable
namespace CollapseLauncher.Plugins
{
    internal class UnknownPluginException(string? message = null, Exception? innerException = null)
        : Exception(message, innerException);

    internal static class UnknownPluginExceptionExtension
    {
        internal static UnknownPluginException WrapPluginException(this Exception? exception, string? message = null)
            => new UnknownPluginException(message, exception);
    }
}
