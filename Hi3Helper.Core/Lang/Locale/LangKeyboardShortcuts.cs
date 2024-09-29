using WinRT;

namespace Hi3Helper
{
    public sealed partial class Locale
    {
        #region Keyboard Shortcuts
        public sealed partial class LocalizationParams
        {
            public LangKeyboardShortcuts _KbShortcuts { get; set; } = LangFallback?._KbShortcuts;

            [GeneratedBindableCustomProperty]
            public sealed partial class LangKeyboardShortcuts
            {
                public string DialogTitle { get; set; } = LangFallback?._KbShortcuts.DialogTitle;

                public string GeneralTab { get; set; } = LangFallback?._KbShortcuts.GeneralTab;
                public string SwitchTab { get; set; } = LangFallback?._KbShortcuts.SwitchTab;
                public string GameFolderTab { get; set; } = LangFallback?._KbShortcuts.GameFolderTab;
                public string GameManagementTab { get; set; } = LangFallback?._KbShortcuts.GameManagementTab;

                public string General_Title { get; set; } = LangFallback?._KbShortcuts.General_Title;
                public string General_OpenMenu { get; set; } = LangFallback?._KbShortcuts.General_OpenMenu;
                public string General_OpenMenu_Desc { get; set; } = LangFallback?._KbShortcuts.General_OpenMenu_Desc;
                public string General_GoHome { get; set; } = LangFallback?._KbShortcuts.General_GoHome;
                public string General_GoSettings { get; set; } = LangFallback?._KbShortcuts.General_GoSettings;
                public string General_OpenNotifTray { get; set; } = LangFallback?._KbShortcuts.General_OpenNotifTray;
                public string General_ReloadRegion { get; set; } = LangFallback?._KbShortcuts.General_ReloadRegion;
                public string General_ReloadRegion_Desc { get; set; } = LangFallback?._KbShortcuts.General_ReloadRegion_Desc;

                public string Switch_Title { get; set; } = LangFallback?._KbShortcuts.Switch_Title;
                public string Switch_Subtitle { get; set; } = LangFallback?._KbShortcuts.Switch_Subtitle;
                public string Switch_SwapBtn { get; set; } = LangFallback?._KbShortcuts.Switch_SwapBtn;
                public string Switch_ChangeGame { get; set; } = LangFallback?._KbShortcuts.Switch_ChangeGame;
                public string Switch_ChangeGame_Desc { get; set; } = LangFallback?._KbShortcuts.Switch_ChangeGame_Desc;
                public string Switch_ChangeRegion { get; set; } = LangFallback?._KbShortcuts.Switch_ChangeRegion;
                public string Switch_ChangeRegion_Desc { get; set; } = LangFallback?._KbShortcuts.Switch_ChangeRegion_Desc;

                public string GameFolder_Title { get; set; } = LangFallback?._KbShortcuts.GameFolder_Title;
                public string GameFolder_ScreenshotFolder { get; set; } = LangFallback?._KbShortcuts.GameFolder_ScreenshotFolder;
                public string GameFolder_MainFolder { get; set; } = LangFallback?._KbShortcuts.GameFolder_MainFolder;
                public string GameFolder_CacheFolder { get; set; } = LangFallback?._KbShortcuts.GameFolder_CacheFolder;

                public string GameManagement_Title { get; set; } = LangFallback?._KbShortcuts.GameManagement_Title;
                public string GameManagement_Subtitle { get; set; } = LangFallback?._KbShortcuts.GameManagement_Subtitle;
                public string GameManagement_ForceCloseGame { get; set; } = LangFallback?._KbShortcuts.GameManagement_ForceCloseGame;
                public string GameManagement_ForceCloseGame_Desc { get; set; } = LangFallback?._KbShortcuts.GameManagement_ForceCloseGame_Desc;
                public string GameManagement_GoRepair { get; set; } = LangFallback?._KbShortcuts.GameManagement_GoRepair;
                public string GameManagement_GoSettings { get; set; } = LangFallback?._KbShortcuts.GameManagement_GoSettings;
                public string GameManagement_GoCaches { get; set; } = LangFallback?._KbShortcuts.GameManagement_GoCaches;

                public string ChangeShortcut_Title { get; set; } = LangFallback?._KbShortcuts.ChangeShortcut_Title;
                public string ChangeShortcut_Text { get; set; } = LangFallback?._KbShortcuts.ChangeShortcut_Text;
                public string ChangeShortcut_Help1 { get; set; } = LangFallback?._KbShortcuts.ChangeShortcut_Help1;
                public string ChangeShortcut_Help2 { get; set; } = LangFallback?._KbShortcuts.ChangeShortcut_Help2;
                public string ChangeShortcut_Help3 { get; set; } = LangFallback?._KbShortcuts.ChangeShortcut_Help3;
                public string ChangeShortcut_Help4 { get; set; } = LangFallback?._KbShortcuts.ChangeShortcut_Help4;

                public string Keyboard_Control { get; set; } = LangFallback?._KbShortcuts.Keyboard_Control;
                public string Keyboard_Menu { get; set; } = LangFallback?._KbShortcuts.Keyboard_Menu;
                public string Keyboard_Shift { get; set; } = LangFallback?._KbShortcuts.Keyboard_Shift;
            }
        }
        #endregion
    }
}
