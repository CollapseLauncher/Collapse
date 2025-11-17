using WinRT;

namespace Hi3Helper;

public sealed partial class Locale
{
    #region WpfPackageContext
    public sealed partial class LocalizationParams
    {
        public LangWpfPackageContext _WpfPackageContext { get; set; } = LangFallback?._WpfPackageContext;

        [GeneratedBindableCustomProperty]
        public sealed partial class LangWpfPackageContext
        {
            public string StartUpdateBtn { get; set; } = LangFallback?._WpfPackageContext.StartUpdateBtn;
            public string CancelUpdateBtn { get; set; } = LangFallback?._WpfPackageContext.CancelUpdateBtn;
            public string ReinstallToolBtn { get; set; } = LangFallback?._WpfPackageContext.ReinstallToolBtn;
            public string StartUpdateAutomatically { get; set; } = LangFallback?._WpfPackageContext.StartUpdateAutomatically;
            public string NotifUpdateCompletedTitle { get; set; } = LangFallback?._WpfPackageContext.NotifUpdateCompletedTitle;
            public string NotifUpdateCompletedSubtitle { get; set; } = LangFallback?._WpfPackageContext.NotifUpdateCompletedSubtitle;
        }
    }
    #endregion
}
