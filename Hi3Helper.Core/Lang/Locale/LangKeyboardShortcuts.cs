namespace Hi3Helper
{
    public sealed partial class Locale
    {
        #region Keyboard Shortcuts
        public sealed partial class LocalizationParams
        {
            public LangKeyboardShortcuts _KbShortcuts { get; set; } = LangFallback?._KbShortcuts;
            public sealed class LangKeyboardShortcuts
            {

            }
        }
        #endregion
    }
}
