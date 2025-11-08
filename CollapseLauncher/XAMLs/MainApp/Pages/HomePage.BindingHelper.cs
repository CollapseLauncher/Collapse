using CollapseLauncher.Extension;
using CollapseLauncher.Helper.LauncherApiLoader.HoYoPlay;
using CommunityToolkit.WinUI;
using ImageEx;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
#pragma warning disable IDE0130

#nullable enable
namespace CollapseLauncher.Pages
{
    file static class HomePageExtension
    {
        internal static readonly BooleanVisibilityConverter BooleanVisibilityConverter = new();
    }

    public partial class HomePage
    {
        private static void ApplySocialMediaBinding(Panel panel)
        {
            // Bind QR Code Overlay
            HypLauncherSocialMediaContentData? dataBind = panel.Tag as HypLauncherSocialMediaContentData;
            Panel? qrParentPanel = panel.FindChild("SocialMediaParentPanel_QR") as Panel;
            Panel? linksParentPanel = panel.FindChild("SocialMediaParentPanel_Links") as Panel;
            TextBlock? descriptionParentPanel = panel.FindChild("SocialMediaParentPanel_Description") as TextBlock;

            // If already assigned, then return
            if (panel is { Tag: true } || dataBind == null)
            {
                panel.Tag = true;
                return;
            }

            // If qrParentPanel is not null, then proceed
            if (qrParentPanel != null)
            {
                BindSocialMediaQr(qrParentPanel, dataBind);
            }

            // If qrParentPanel is not null, then proceed
            if (linksParentPanel != null)
            {
                BindSocialMediaLinks(linksParentPanel, dataBind);
            }

            // If descriptionParentPanel is not null, then proceed
            if (descriptionParentPanel != null)
            {
                BindSocialMediaDescription(descriptionParentPanel, dataBind);
            }

            // Set as assigned
            panel.Tag = true;

            // Set visibility to visible
            panel.Visibility = Visibility.Visible;
        }

        private static void BindSocialMediaQr(Panel parentPanel, HypLauncherSocialMediaContentData dataBind)
        {
            // Bind visibility if dataBind has QR
            parentPanel.BindProperty(VisibilityProperty, dataBind, "IsHasQr", converter: HomePageExtension.BooleanVisibilityConverter);

            // Find the child grid
            Grid? childGrid = parentPanel.FindChild<Grid>();
            if (childGrid == null) return;

            // Bind the QR properties
            ImageEx.ImageEx? qrImageInstance = childGrid.FindChild<ImageEx.ImageEx>();
            qrImageInstance?.BindProperty(ImageExBase.SourceProperty, dataBind, "QrImg");

            TextBlock? textBlockInstance = childGrid.FindChild<TextBlock>();
            textBlockInstance?.BindProperty(TextBlock.TextProperty, dataBind, "QrTitle");
            textBlockInstance?.BindProperty(VisibilityProperty, dataBind, "IsHasQrDescription", converter: HomePageExtension.BooleanVisibilityConverter);
        }

        private static void BindSocialMediaLinks(Panel parentPanel, HypLauncherSocialMediaContentData dataBind)
        {
            // Bind visibility if dataBind has Links
            parentPanel.BindProperty(VisibilityProperty, dataBind, "IsHasLinks", converter: HomePageExtension.BooleanVisibilityConverter);

            // Find the ItemsControl
            ItemsControl? itemsControl = parentPanel.FindChild<ItemsControl>();
            if (itemsControl == null) return;

            // If dataBind has links, then assign the ItemsSource
            if (dataBind.ChildrenLinks.Count > 0)
            {
                itemsControl.ItemsSource = dataBind.ChildrenLinks;
            }
        }

        private static void BindSocialMediaDescription(TextBlock parentPanel, HypLauncherSocialMediaContentData dataBind)
        {
            // Bind visibility if dataBind has Description
            parentPanel.BindProperty(VisibilityProperty, dataBind, "IsHasDescription", converter: HomePageExtension.BooleanVisibilityConverter);

            // Bind description text
            parentPanel.BindProperty(TextBlock.TextProperty, dataBind, "Title");
        }
    }
}
