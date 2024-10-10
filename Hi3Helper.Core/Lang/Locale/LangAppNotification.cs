using WinRT;

namespace Hi3Helper
{
    public sealed partial class Locale
    {
        #region AppNotification
        public sealed partial class LocalizationParams
        {
            public LangAppNotification _AppNotification { get; set; } = LangFallback?._AppNotification;

            [GeneratedBindableCustomProperty]
            // ReSharper disable once PartialTypeWithSinglePart
            public sealed partial class LangAppNotification
            {
                public string NotifMetadataUpdateTitle { get; set; } = LangFallback?._AppNotification.NotifMetadataUpdateTitle;
                public string NotifMetadataUpdateSubtitle { get; set; } = LangFallback?._AppNotification.NotifMetadataUpdateSubtitle;
                public string NotifMetadataUpdateBtn { get; set; } = LangFallback?._AppNotification.NotifMetadataUpdateBtn;
                public string NotifMetadataUpdateBtnUpdating { get; set; } = LangFallback?._AppNotification.NotifMetadataUpdateBtnUpdating;
                public string NotifMetadataUpdateBtnCountdown { get; set; } = LangFallback?._AppNotification.NotifMetadataUpdateBtnCountdown;

                public string NotifFirstWelcomeTitle { get; set; } = LangFallback?._AppNotification.NotifFirstWelcomeTitle;
                public string NotifFirstWelcomeSubtitle { get; set; } = LangFallback?._AppNotification.NotifFirstWelcomeSubtitle;
                public string NotifFirstWelcomeBtn { get; set; } = LangFallback?._AppNotification.NotifFirstWelcomeBtn;

                public string NotifPreviewBuildUsedTitle { get; set; } = LangFallback?._AppNotification.NotifPreviewBuildUsedTitle;
                public string NotifPreviewBuildUsedSubtitle { get; set; } = LangFallback?._AppNotification.NotifPreviewBuildUsedSubtitle;
                public string NotifPreviewBuildUsedBtn { get; set; } = LangFallback?._AppNotification.NotifPreviewBuildUsedBtn;

                public string NotifKbShortcutTitle { get; set; } = LangFallback?._AppNotification.NotifKbShortcutTitle;
                public string NotifKbShortcutSubtitle { get; set; } = LangFallback?._AppNotification.NotifKbShortcutSubtitle;
                public string NotifKbShortcutBtn { get; set; } = LangFallback?._AppNotification.NotifKbShortcutBtn;
            }
        }
        #endregion
    }
}
