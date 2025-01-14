using CommunityToolkit.Labs.WinUI.Labs.MarkdownTextBlock;
using Hi3Helper.Shared.Region;
using Microsoft.UI.Xaml;
using System.Collections.Generic;
using System.IO;
// ReSharper disable IdentifierTypo
// ReSharper disable InconsistentNaming

namespace CollapseLauncher.Pages.OOBE
{
    internal class AgreementProperty
    {
        internal AgreementProperty(string title, string filePath)
        {
            FilePath = Path.Combine(LauncherConfig.AppExecutableDir, filePath);
            Title = title;
        }

        internal string Text
        {
            get
            {
                return !File.Exists(FilePath) ? $"### Failed to read the file\n**{FilePath}**" : File.ReadAllText(FilePath);
            }
        }
        internal string Title { get; init; }
        internal string FilePath { get; init; }
        internal MarkdownConfig MarkdownConfig = new();
    }

    public static class OOBEAgreementMenuExtensions
    {
        internal static OOBEStartUpMenu OobeStartParentUI;
    }

    public sealed partial class OOBEAgreementMenu
    {
        internal readonly List<AgreementProperty> MarkdownFileList =
        [
            new("Privacy Policy (EN)",      "PRIVACY.md"),
            new("Third Party Notices (EN)", "THIRD_PARTY_NOTICES.md")
        ];

        public OOBEAgreementMenu()
        {
            InitializeComponent();
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            OOBEAgreementMenuExtensions.OobeStartParentUI.StartLauncherConfiguration();
        }
    }
}
