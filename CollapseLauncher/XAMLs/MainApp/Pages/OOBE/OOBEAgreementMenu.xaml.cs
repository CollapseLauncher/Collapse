using CommunityToolkit.Labs.WinUI.Labs.MarkdownTextBlock;
using Hi3Helper.Shared.Region;
using Microsoft.UI.Xaml;
using System.Collections.Generic;
using System.IO;

namespace CollapseLauncher.Pages.OOBE
{
    internal class AgreementProperty
    {
        internal AgreementProperty(string title, string filePath)
        {
            this.filePath = Path.Combine(LauncherConfig.AppFolder, filePath);
            this.title = title;
        }

        internal string text
        {
            get
            {
                if (!File.Exists(filePath))
                    return $"### Failed to read the file\n**{filePath}**";

                return File.ReadAllText(filePath);
            }
        }
        internal string title { get; init; }
        internal string filePath { get; init; }
        internal MarkdownConfig markdownConfig = new MarkdownConfig();
    }

    public static class OOBEAgreementMenuExtensions
    {
        internal static OOBEStartUpMenu oobeStartParentUI;
    }

    public sealed partial class OOBEAgreementMenu
    {
        private List<AgreementProperty> markdownFileList = new List<AgreementProperty>
        {
            new AgreementProperty("Privacy Policy (EN)", "PRIVACY.md"),
            new AgreementProperty("Third Party Notices (EN)", "THIRD_PARTY_NOTICES.md")
        };

        public OOBEAgreementMenu()
        {
            this.InitializeComponent();
        }

        public void SetParentUI(OOBEStartUpMenu startUpParentUI) => OOBEAgreementMenuExtensions.oobeStartParentUI = startUpParentUI;

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            OOBEAgreementMenuExtensions.oobeStartParentUI.StartLauncherConfiguration();
        }
    }
}
