using System;
using System.Collections;
using System.Globalization;
using System.Resources;
using System.Text.RegularExpressions;
using System.Threading;

#nullable enable

namespace Hi3Helper.SentryHelper
{
    public partial class SentryExceptionLocale
    {
        public static string? TranslateExceptionMessage(Exception exception)
        {
            try
            {
                var targetCulture = CultureInfo.CreateSpecificCulture("en-US");
                var uiCulture     = Thread.CurrentThread.CurrentUICulture;
                var a             = exception.GetType().Assembly;
                var rm            = new ResourceManager(a.GetName().Name ?? "CollapseLauncher", a);
                var rsOriginal    = rm.GetResourceSet(uiCulture,     true, true);
                var rsTranslated  = rm.GetResourceSet(targetCulture, true, true);
                if (rsOriginal == null || rsTranslated == null) return null;
                foreach (DictionaryEntry item in rsOriginal)
                {
                    if (!(item.Value is string message)) continue;
                    var    translated = rsTranslated.GetString(item.Key.ToString()!, false);
                    string result;
                    if (!message.Contains("{"))
                    {
                        result = exception.Message.Replace(message, translated);
                    }
                    else
                    {
                        var pattern = $"{Regex.Escape(message)}";
                        pattern = GroupPattern().Replace(pattern, "(?<group$1>.*)");
                        var regex          = new Regex(pattern);
                        var replacePattern = translated;
                        replacePattern = ReplacePattern()
                           .Replace(replacePattern ?? throw new InvalidOperationException("replacePattern is null"),
                                    "${group$1}");
                        replacePattern = replacePattern.Replace("\\$", "$");
                        result         = regex.Replace(exception.Message, replacePattern);
                    }

                    return result;
                }
            }
            catch (Exception ex)
            {
                Logger.LogWriteLine($"[Sentry] Failed to translate exception message: {ex.Message}. Returning original message." + $"\r\n {ex}",
                                    LogType.Sentry);
            }

            return null;
        }

        [GeneratedRegex(@"\\{([0-9]+)\}", RegexOptions.Compiled)]
        private static partial Regex GroupPattern();

        [GeneratedRegex(@"{([0-9]+)}", RegexOptions.Compiled)]
        private static partial Regex ReplacePattern();
    }
}