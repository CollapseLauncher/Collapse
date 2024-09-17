namespace Hi3Helper
{
    public sealed partial class Locale
    {
        #region NotificationToast
        public sealed partial class LocalizationParams
        {
            public LangNotificationToast _NotificationToast { get; set; } = LangFallback?._NotificationToast;
            public sealed class LangNotificationToast
            {
                public string WindowHiddenToTray_Title                      { get; set; } = LangFallback?._NotificationToast.WindowHiddenToTray_Title;
                public string WindowHiddenToTray_Subtitle                   { get; set; } = LangFallback?._NotificationToast.WindowHiddenToTray_Subtitle;

                public string GameInstallCompleted_Title                    { get; set; } = LangFallback?._NotificationToast.GameInstallCompleted_Title;
                public string GameInstallCompleted_Subtitle                 { get; set; } = LangFallback?._NotificationToast.GameInstallCompleted_Subtitle;

                public string GameUpdateCompleted_Title                     { get; set; } = LangFallback?._NotificationToast.GameUpdateCompleted_Title;
                public string GameUpdateCompleted_Subtitle                  { get; set; } = LangFallback?._NotificationToast.GameUpdateCompleted_Subtitle;

                public string GamePreloadCompleted_Title                    { get; set; } = LangFallback?._NotificationToast.GamePreloadCompleted_Title;

                public string GameRepairCheckCompleted_Title                { get; set; } = LangFallback?._NotificationToast.GameRepairCheckCompleted_Title;
                public string GameRepairCheckCompletedFound_Subtitle        { get; set; } = LangFallback?._NotificationToast.GameRepairCheckCompletedFound_Subtitle;
                public string GameRepairCheckCompletedNotFound_Subtitle     { get; set; } = LangFallback?._NotificationToast.GameRepairCheckCompletedNotFound_Subtitle;

                public string GameRepairDownloadCompleted_Title             { get; set; } = LangFallback?._NotificationToast.GameRepairDownloadCompleted_Title;
                public string GameRepairDownloadCompleted_Subtitle          { get; set; } = LangFallback?._NotificationToast.GameRepairDownloadCompleted_Subtitle;

                public string CacheUpdateCheckCompleted_Title               { get; set; } = LangFallback?._NotificationToast.CacheUpdateCheckCompleted_Title;
                public string CacheUpdateCheckCompletedFound_Subtitle       { get; set; } = LangFallback?._NotificationToast.CacheUpdateCheckCompletedFound_Subtitle;
                public string CacheUpdateCheckCompletedNotFound_Subtitle    { get; set; } = LangFallback?._NotificationToast.CacheUpdateCheckCompletedNotFound_Subtitle;

                public string CacheUpdateDownloadCompleted_Title            { get; set; } = LangFallback?._NotificationToast.CacheUpdateDownloadCompleted_Title;
                public string CacheUpdateDownloadCompleted_Subtitle         { get; set; } = LangFallback?._NotificationToast.CacheUpdateDownloadCompleted_Subtitle;

                public string GenericClickNotifToGoBack_Subtitle            { get; set; } = LangFallback?._NotificationToast.GenericClickNotifToGoBack_Subtitle;
            }
        }
        #endregion
    }
}
