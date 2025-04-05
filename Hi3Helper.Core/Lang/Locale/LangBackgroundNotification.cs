using WinRT;
// ReSharper disable PartialTypeWithSinglePart
// ReSharper disable InconsistentNaming
// ReSharper disable IdentifierTypo

namespace Hi3Helper
{
    public sealed partial class Locale
    {
        #region BackgroundNotification
        public sealed partial class LocalizationParams
        {
            public LangBackgroundNotification _BackgroundNotification { get; set; } = LangFallback?._BackgroundNotification;

            [GeneratedBindableCustomProperty]
            public sealed partial class LangBackgroundNotification
            {
                public string LoadingTitle { get; set; } = LangFallback?._BackgroundNotification.LoadingTitle;
                public string Placeholder { get; set; } = LangFallback?._BackgroundNotification.Placeholder;
                public string CategoryTitle_Downloading { get; set; } = LangFallback?._BackgroundNotification.CategoryTitle_Downloading;
                public string CategoryTitle_DownloadingPreload { get; set; } = LangFallback?._BackgroundNotification.CategoryTitle_DownloadingPreload;
                public string CategoryTitle_Updating { get; set; } = LangFallback?._BackgroundNotification.CategoryTitle_Updating;
                public string NotifBadge_Completed { get; set; } = LangFallback?._BackgroundNotification.NotifBadge_Completed;
                public string NotifBadge_Error { get; set; } = LangFallback?._BackgroundNotification.NotifBadge_Error;
            }
        }
        #endregion
    }
}
