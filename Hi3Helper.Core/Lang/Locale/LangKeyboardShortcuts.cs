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
                public string DialogTitle { get; set; } = LangFallback?._KbShortcuts.DialogTitle;

                public string GeneralTab { get; set; } = LangFallback?._KbShortcuts.GeneralTab;
                public string SwitchTab { get; set; } = LangFallback?._KbShortcuts.SwitchTab;
                public string GameFolderTab { get; set; } = LangFallback?._KbShortcuts.GameFolderTab;
                public string GameManagementTab { get; set; } = LangFallback?._KbShortcuts.GameManagementTab;

                public string General_Title { get; set; } = LangFallback?._KbShortcuts.General_Title;
                public string General_OpenMenu { get; set; } = LangFallback?._KbShortcuts.General_OpenMenu;
                public string General_GoHome { get; set; } = LangFallback?._KbShortcuts.General_GoHome;
            }
        }
        #endregion
    }
}
