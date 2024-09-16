using CollapseLauncher.Helper.LauncherApiLoader.Sophon;
using CommunityToolkit.WinUI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Data;

#nullable enable
namespace CollapseLauncher.Pages
{
    file static class HomePageExtension
    {
        internal static BooleanVisibilityConverter BooleanVisibilityConverter = new BooleanVisibilityConverter();

        internal static void BindProperty(this FrameworkElement element, DependencyProperty dependencyProperty, object objectToBind, string propertyName, IValueConverter? converter = null, BindingMode bindingMode = BindingMode.OneWay)
        {
            // Create a new binding instance
            Binding binding = new Binding
            {
                Source = objectToBind,
                Mode = bindingMode,
                Path = new PropertyPath(propertyName),
                UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged
            };

            // If the converter is assigned, then add the converter
            if (converter != null)
            {
                binding.Converter = converter;
            }

            // Set binding to the element
            element.SetBinding(dependencyProperty, binding);
        }
    }

    public partial class HomePage
    {
        private void ApplySocialMediaBinding(Panel panel)
        {
            // Bind QR Code Overlay
            LauncherGameNewsSocialMedia? dataBind = panel.Tag as LauncherGameNewsSocialMedia;
            Panel? qrParentPanel = panel.FindChild("SocialMediaParentPanel_QR") as Panel;
            Panel? linksParentPanel = panel.FindChild("SocialMediaParentPanel_Links") as Panel;
            TextBlock? descriptionParentPanel = panel.FindChild("SocialMediaParentPanel_Description") as TextBlock;

            // If already assigned, then return
            if ((panel.Tag is bool isAssigned && isAssigned) || dataBind == null)
            {
                panel.Tag = true;
                return;
            }

            if (dataBind == null)
            {
                return;
            }

            // If qrParentPanel is not null, then proceed
            if (qrParentPanel != null)
            {
                BindSocialMediaQR(qrParentPanel, dataBind);
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

        private void BindSocialMediaQR(Panel parentPanel, LauncherGameNewsSocialMedia dataBind)
        {
            // Bind visibility if dataBind has QR
            parentPanel.BindProperty(StackPanel.VisibilityProperty, dataBind, "IsHasQr", HomePageExtension.BooleanVisibilityConverter);

            // Find the child grid
            Grid? childGrid = parentPanel.FindChild<Grid>();
            if (childGrid == null) return;

            // Bind the QR properties
            ImageEx.ImageEx? qrImageInstance = childGrid.FindChild<ImageEx.ImageEx>();
            qrImageInstance?.BindProperty(ImageEx.ImageEx.SourceProperty, dataBind, "QrImg");

            TextBlock? textBlockInstance = childGrid.FindChild<TextBlock>();
            textBlockInstance?.BindProperty(TextBlock.TextProperty, dataBind, "QrTitle");
            textBlockInstance?.BindProperty(TextBlock.VisibilityProperty, dataBind, "IsHasQrDescription", HomePageExtension.BooleanVisibilityConverter);
        }

        private void BindSocialMediaLinks(Panel parentPanel, LauncherGameNewsSocialMedia dataBind)
        {
            // Bind visibility if dataBind has Links
            parentPanel.BindProperty(StackPanel.VisibilityProperty, dataBind, "IsHasLinks", HomePageExtension.BooleanVisibilityConverter);

            // Find the ItemsControl
            ItemsControl? itemsControl = parentPanel.FindChild<ItemsControl>();
            if (itemsControl == null) return;

            // If dataBind has links, then assign the ItemsSource
            if (dataBind.IsHasLinks)
            {
                itemsControl.ItemsSource = dataBind.QrLinks;
            }
        }

        private void BindSocialMediaDescription(TextBlock parentPanel, LauncherGameNewsSocialMedia dataBind)
        {
            // Bind visibility if dataBind has Description
            parentPanel.BindProperty(TextBlock.VisibilityProperty, dataBind, "IsHasDescription", HomePageExtension.BooleanVisibilityConverter);

            // Bind description text
            parentPanel.BindProperty(TextBlock.TextProperty, dataBind, "Title");
        }
    }
}
