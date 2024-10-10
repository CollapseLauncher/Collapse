using WinRT;

namespace Hi3Helper
{
    public sealed partial class Locale
    {
        #region Misc
        public sealed partial class LocalizationParams
        {
            public LangMisc _Misc { get; set; } = LangFallback?._Misc;

            [GeneratedBindableCustomProperty]
            public sealed partial class LangMisc
            {
                public string SizePrefixes1000U              { get; set; } = LangFallback?._Misc.SizePrefixes1000U;
                public string UpdateCompleteTitle            { get; set; } = LangFallback?._Misc.UpdateCompleteTitle;
                public string UpdateCompleteSubtitle         { get; set; } = LangFallback?._Misc.UpdateCompleteSubtitle;
                public string FeatureUnavailableTitle        { get; set; } = LangFallback?._Misc.FeatureUnavailableTitle;
                public string FeatureUnavailableSubtitle     { get; set; } = LangFallback?._Misc.FeatureUnavailableSubtitle;
                public string TimeRemain                     { get; set; } = LangFallback?._Misc.TimeRemain;
                public string TimeRemainHMSFormat            { get; set; } = LangFallback?._Misc.TimeRemainHMSFormat;
                public string TimeRemainHMSFormatPlaceholder { get; set; } = LangFallback?._Misc.TimeRemainHMSFormatPlaceholder;

                public string Speed                { get; set; } = LangFallback?._Misc.Speed;
                public string SpeedTextOnly        { get; set; } = LangFallback?._Misc.SpeedTextOnly;
                public string SpeedPerSec          { get; set; } = LangFallback?._Misc.SpeedPerSec;
                public string SpeedPlaceholder     { get; set; } = LangFallback?._Misc.SpeedPlaceholder;
                public string PerFromTo            { get; set; } = LangFallback?._Misc.PerFromTo;
                public string PerFromToPlaceholder { get; set; } = LangFallback?._Misc.PerFromToPlaceholder;
                public string Cancel               { get; set; } = LangFallback?._Misc.Cancel;
                public string Close                { get; set; } = LangFallback?._Misc.Close;
                public string Skip                 { get; set; } = LangFallback?._Misc.Skip;
                public string MoveToDifferentDir   { get; set; } = LangFallback?._Misc.MoveToDifferentDir;
                public string UseCurrentDir        { get; set; } = LangFallback?._Misc.UseCurrentDir;
                public string UseDefaultDir        { get; set; } = LangFallback?._Misc.UseDefaultDir;
                public string LocateDir            { get; set; } = LangFallback?._Misc.LocateDir;
                public string Okay                 { get; set; } = LangFallback?._Misc.Okay;
                public string OkaySad              { get; set; } = LangFallback?._Misc.OkaySad;
                public string OkayHappy            { get; set; } = LangFallback?._Misc.OkayHappy;
                public string OkayBackToMenu       { get; set; } = LangFallback?._Misc.OkayBackToMenu;
                public string Next                 { get; set; } = LangFallback?._Misc.Next;
                public string Prev                 { get; set; } = LangFallback?._Misc.Prev;
                public string Uninstall            { get; set; } = LangFallback?._Misc.Uninstall;
                public string Yes                  { get; set; } = LangFallback?._Misc.Yes;
                public string YesRedownload        { get; set; } = LangFallback?._Misc.YesRedownload;
                public string YesResume            { get; set; } = LangFallback?._Misc.YesResume;
                public string YesRelocate          { get; set; } = LangFallback?._Misc.YesRelocate;
                public string YesMigrateIt         { get; set; } = LangFallback?._Misc.YesMigrateIt;
                public string YesConvertIt         { get; set; } = LangFallback?._Misc.YesConvertIt;
                public string YesImReallySure      { get; set; } = LangFallback?._Misc.YesImReallySure;
                public string YesIHaveBeefyPC      { get; set; } = LangFallback?._Misc.YesIHaveBeefyPC;
                public string YesChangeLocation    { get; set; } = LangFallback?._Misc.YesChangeLocation;
                public string YesContinue          { get; set; } = LangFallback?._Misc.YesContinue;
                public string No                   { get; set; } = LangFallback?._Misc.No;
                public string NoStartFromBeginning { get; set; } = LangFallback?._Misc.NoStartFromBeginning;
                public string NoCancel             { get; set; } = LangFallback?._Misc.NoCancel;
                public string NoKeepInstallIt      { get; set; } = LangFallback?._Misc.NoKeepInstallIt;
                public string NoIgnoreIt           { get; set; } = LangFallback?._Misc.NoIgnoreIt;
                public string NoOtherLocation      { get; set; } = LangFallback?._Misc.NoOtherLocation;
                public string NotSelected          { get; set; } = LangFallback?._Misc.NotSelected;
                public string ExtractAnyway        { get; set; } = LangFallback?._Misc.ExtractAnyway;
                public string LangNameENUS         { get; set; } = LangFallback?._Misc.LangNameENUS;
                public string LangNameJP           { get; set; } = LangFallback?._Misc.LangNameJP;
                public string LangNameCN           { get; set; } = LangFallback?._Misc.LangNameCN;
                public string LangNameKR           { get; set; } = LangFallback?._Misc.LangNameKR;
                public string Downloading          { get; set; } = LangFallback?._Misc.Downloading;
                public string Updating             { get; set; } = LangFallback?._Misc.Updating;
                public string UpdatingAndApplying  { get; set; } = LangFallback?._Misc.UpdatingAndApplying;
                public string Applying             { get; set; } = LangFallback?._Misc.Applying;
                public string Merging              { get; set; } = LangFallback?._Misc.Merging;
                public string Idle                 { get; set; } = LangFallback?._Misc.Idle;
                public string Change               { get; set; } = LangFallback?._Misc.Change;
                public string Cancelled            { get; set; } = LangFallback?._Misc.Cancelled;
                public string FinishingUp          { get; set; } = LangFallback?._Misc.FinishingUp;
                public string Extracting           { get; set; } = LangFallback?._Misc.Extracting;
                public string Converting           { get; set; } = LangFallback?._Misc.Converting;
                public string Patching             { get; set; } = LangFallback?._Misc.Patching;
                public string Verifying            { get; set; } = LangFallback?._Misc.Verifying;
                public string Completed            { get; set; } = LangFallback?._Misc.Completed;
                public string Skipped              { get; set; } = LangFallback?._Misc.Skipped;
                public string Select               { get; set; } = LangFallback?._Misc.Select;
                public string Install              { get; set; } = LangFallback?._Misc.Install;
                public string NotRunning           { get; set; } = LangFallback?._Misc.NotRunning;
                public string MovingFile           { get; set; } = LangFallback?._Misc.MovingFile;
                public string CheckingFile         { get; set; } = LangFallback?._Misc.CheckingFile;
                public string RepairingFile        { get; set; } = LangFallback?._Misc.RepairingFile;
                public string ApplyingPatch        { get; set; } = LangFallback?._Misc.ApplyingPatch;
                public string Disabled             { get; set; } = LangFallback?._Misc.Disabled;
                public string Enabled              { get; set; } = LangFallback?._Misc.Enabled;
                public string BuildChannelPreview  { get; set; } = LangFallback?._Misc.BuildChannelPreview;
                public string BuildChannelStable   { get; set; } = LangFallback?._Misc.BuildChannelStable;
                public string LocateExecutable     { get; set; } = LangFallback?._Misc.LocateExecutable;
                public string OpenDownloadPage     { get; set; } = LangFallback?._Misc.OpenDownloadPage;
                public string UseAsDefault         { get; set; } = LangFallback?._Misc.UseAsDefault;

                public string CDNDescription_Github          { get; set; } = LangFallback?._Misc.CDNDescription_Github;
                public string CDNDescription_Cloudflare      { get; set; } = LangFallback?._Misc.CDNDescription_Cloudflare;
                public string CDNDescription_Bitbucket       { get; set; } = LangFallback?._Misc.CDNDescription_Bitbucket;
                public string CDNDescription_Statically      { get; set; } = LangFallback?._Misc.CDNDescription_Statically;
                public string CDNDescription_jsDelivr        { get; set; } = LangFallback?._Misc.CDNDescription_jsDelivr;
                public string CDNDescription_GitLab          { get; set; } = LangFallback?._Misc.CDNDescription_GitLab;
                public string CDNDescription_Coding          { get; set; } = LangFallback?._Misc.CDNDescription_Coding;

                public string DiscordRP_Play         { get; set; } = LangFallback?._Misc.DiscordRP_Play;
                public string DiscordRP_InGame       { get; set; } = LangFallback?._Misc.DiscordRP_InGame;
                public string DiscordRP_Update       { get; set; } = LangFallback?._Misc.DiscordRP_Update;
                public string DiscordRP_Repair       { get; set; } = LangFallback?._Misc.DiscordRP_Repair;
                public string DiscordRP_Cache        { get; set; } = LangFallback?._Misc.DiscordRP_Cache;
                public string DiscordRP_GameSettings { get; set; } = LangFallback?._Misc.DiscordRP_GameSettings;
                public string DiscordRP_AppSettings  { get; set; } = LangFallback?._Misc.DiscordRP_AppSettings;
                public string DiscordRP_Idle         { get; set; } = LangFallback?._Misc.DiscordRP_Idle;
                public string DiscordRP_Default      { get; set; } = LangFallback?._Misc.DiscordRP_Default;
                public string DiscordRP_Ad           { get; set; } = LangFallback?._Misc.DiscordRP_Ad;
                public string DiscordRP_Region       { get; set; } = LangFallback?._Misc.DiscordRP_Region;

                public string DownloadModeLabelSophon { get; set; } = LangFallback?._Misc.DownloadModeLabelSophon;

                public string Taskbar_PopupHelp1  { get; set; } = LangFallback?._Misc.Taskbar_PopupHelp1;
                public string Taskbar_PopupHelp2  { get; set; } = LangFallback?._Misc.Taskbar_PopupHelp2;
                public string Taskbar_ShowApp     { get; set; } = LangFallback?._Misc.Taskbar_ShowApp;
                public string Taskbar_HideApp     { get; set; } = LangFallback?._Misc.Taskbar_HideApp;
                public string Taskbar_ShowConsole { get; set; } = LangFallback?._Misc.Taskbar_ShowConsole;
                public string Taskbar_HideConsole { get; set; } = LangFallback?._Misc.Taskbar_HideConsole;
                public string Taskbar_ExitApp     { get; set; } = LangFallback?._Misc.Taskbar_ExitApp;

                public string LauncherNameOfficial { get; set; } = LangFallback?._Misc.LauncherNameOfficial;
                public string LauncherNameBHI3L    { get; set; } = LangFallback?._Misc.LauncherNameBHI3L;
                public string LauncherNameSteam    { get; set; } = LangFallback?._Misc.LauncherNameSteam;
                public string LauncherNameUnknown  { get; set; } = LangFallback?._Misc.LauncherNameUnknown;

                public string IAcceptAgreement      { get; set; } = LangFallback?._Misc.IAcceptAgreement;
                public string IDoNotAcceptAgreement { get; set; } = LangFallback?._Misc.IDoNotAcceptAgreement;

                public string ImageCropperTitle     { get; set; } = LangFallback?._Misc.ImageCropperTitle;

                public string IsBytesMoreThanBytes  { get; set; } = LangFallback?._Misc.IsBytesMoreThanBytes;
                public string IsBytesUnlimited      { get; set; } = LangFallback?._Misc.IsBytesUnlimited;
                public string IsBytesNotANumber     { get; set; } = LangFallback?._Misc.IsBytesNotANumber;
            }
        }
        #endregion
    }
}
