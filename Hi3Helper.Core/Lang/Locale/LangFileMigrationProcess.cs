using WinRT;

namespace Hi3Helper
{
    public sealed partial class Locale
    {
        #region FileMigrationProcess
        public sealed partial class LocalizationParams
        {
            public LangFileMigrationProcess _FileMigrationProcess { get; set; } = LangFallback?._FileMigrationProcess;

            [GeneratedBindableCustomProperty]
            public sealed partial class LangFileMigrationProcess
            {
                public string PathActivityPanelTitle { get; set; } = LangFallback?._FileMigrationProcess.PathActivityPanelTitle;
                public string SpeedIndicatorTitle { get; set; } = LangFallback?._FileMigrationProcess.SpeedIndicatorTitle;
                public string FileCountIndicatorTitle { get; set; } = LangFallback?._FileMigrationProcess.FileCountIndicatorTitle;
                public string LocateFolderSubtitle { get; set; } = LangFallback?._FileMigrationProcess.LocateFolderSubtitle;
                public string ChoosePathTextBoxPlaceholder { get; set; } = LangFallback?._FileMigrationProcess.ChoosePathTextBoxPlaceholder;
                public string ChoosePathButton { get; set; } = LangFallback?._FileMigrationProcess.ChoosePathButton;
                public string ChoosePathErrorTitle { get; set; } = LangFallback?._FileMigrationProcess.ChoosePathErrorTitle;
                public string ChoosePathErrorPathIdentical { get; set; } = LangFallback?._FileMigrationProcess.ChoosePathErrorPathIdentical;
                public string ChoosePathErrorPathUnselected { get; set; } = LangFallback?._FileMigrationProcess.ChoosePathErrorPathUnselected;
                public string ChoosePathErrorPathNotExist { get; set; } = LangFallback?._FileMigrationProcess.ChoosePathErrorPathNotExist;
                public string ChoosePathErrorPathNoPermission { get; set; } = LangFallback?._FileMigrationProcess.ChoosePathErrorPathNoPermission;
            }
        }
        #endregion
    }
}
