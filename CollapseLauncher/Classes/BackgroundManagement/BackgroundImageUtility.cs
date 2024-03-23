using CollapseLauncher.Extension;
using Hi3Helper.Shared.Region;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

#nullable enable
namespace CollapseLauncher.BackgroundManagement
{
    internal static class BackgroundImageUtility
    {
        private enum MediaType { Media, StillImage, Unknown };

        internal static readonly string[] _supportedImageExt = new string[] { ".jpg", ".jpeg", ".jfif", ".png", ".bmp", ".tiff", ".tif", ".webp" };
        internal static readonly string[] _supportedMediaPlayerExt = new string[] { ".mp4", ".mov", ".mkv", ".webm", ".avi", ".gif" };

        private static ImageSource? _imageSourceLast = null;
        private static ImageSource? _imageSourceCurrent = null;

        private static Image? _bgImageBackground = null;
        private static Image? _bgImageBackgroundLast = null;
        private static Image? _bgImageForeground = null;
        private static Image? _bgImageForegroundLast = null;
        private static MediaPlayerElement? _bgMediaPlayerBackground = null;

        private static Grid? _parentBgImageBackgroundGrid = null;
        private static Grid? _parentBgImageForegroundGrid = null;
        private static Grid? _parentBgMediaPlayerBackgroundGrid = null;

        private static bool _isCurrentRegistered = false;
        private static MediaType _currentAppliedMediaType = MediaType.Unknown;

        private delegate Task AssignDefaultAction<T>(T element) where T : class;

        /// <summary>
        /// Attach and register the <c>Grid</c> of the page to be assigned with background utility.
        /// The <c>Grid</c> must be empty or have existing instances of previously registered grid.
        /// </summary>
        /// <param name="bgImageGridForeground">The parent <c>Grid</c> for Foreground Image.</param>
        /// <param name="bgImageGridBackground">The parent <c>Grid</c> for Background Image.</param>
        /// <param name="bgMediaPlayerGrid">The parent <c>Grid</c> for Background Media Player</param>
        internal static async Task RegisterCurrent(Grid bgImageGridForeground, Grid bgImageGridBackground, Grid bgMediaPlayerGrid)
        {
            // Initialize the background instances
            (_bgImageForeground, _bgImageForegroundLast) = await InitializeElementGrid<Image>(bgImageGridForeground, "ImageForeground", AssignDefaultImage);
            (_bgImageBackground, _bgImageBackgroundLast) = await InitializeElementGrid<Image>(bgImageGridBackground, "ImageBackground", AssignDefaultImage);
            _bgMediaPlayerBackground = (await TryGetFirstGridElement<MediaPlayerElement>(bgMediaPlayerGrid, "MediaPlayer"))
                .WithHorizontalAlignment(HorizontalAlignment.Center)
                .WithVerticalAlignment(VerticalAlignment.Center)
                .WithStretch(Stretch.UniformToFill);

            // Store the parent grid reference into this static variables
            _parentBgImageForegroundGrid = bgImageGridForeground;
            _parentBgImageBackgroundGrid = bgImageGridBackground;
            _parentBgMediaPlayerBackgroundGrid = bgMediaPlayerGrid;

            _isCurrentRegistered = true;
        }

        /// <summary>
        /// Detach the current background utility from the previously attached <c>Grid</c>.
        /// </summary>
        internal static void DetachCurrent()
        {
            _imageSourceLast = null;
            _imageSourceCurrent = null;

            _bgImageBackground = null;
            _bgImageForeground = null;
            _bgImageBackgroundLast = null;
            _bgImageForegroundLast = null;
            _bgMediaPlayerBackground = null;

            _parentBgImageForegroundGrid?.ClearChildren();
            _parentBgImageBackgroundGrid?.ClearChildren();
            _parentBgMediaPlayerBackgroundGrid?.ClearChildren();

            _parentBgImageForegroundGrid = null;
            _parentBgImageBackgroundGrid = null;
            _parentBgMediaPlayerBackgroundGrid = null;

            _isCurrentRegistered = false;
            _currentAppliedMediaType = MediaType.Unknown;
        }

        /// <summary>
        /// Initialize or to find an existing element by its base tag and type.
        /// </summary>
        /// <typeparam name="TElement">The type of the element need to be created (where <c>TElement</c> is a member of <c>FrameworkElement</c>)</typeparam>
        /// <param name="elementGrid">The parent <c>Grid</c> where the element is going to be or has stored.</param>
        /// <param name="baseTagType">The base tag to determine the element type.</param>
        /// <param name="defaultAssignAction">The delegate to perform action after the new element is created.</param>
        /// <returns>The tuple of the new "current and last" instance of the element.</returns>
        private static async Task<(TElement?, TElement?)> InitializeElementGrid<TElement>(Grid elementGrid, string baseTagType, AssignDefaultAction<TElement>? defaultAssignAction = null)
            where TElement : FrameworkElement, new()
        {
            // Get the type name of the element
            string? typeName = typeof(TElement).Name + '_';
            
            // Find or create the element from/to the parent grid.
            TElement? elementCurrent = (await TryGetFirstGridElement(elementGrid, typeName + baseTagType + "_Current", defaultAssignAction))
                .WithHorizontalAlignment(HorizontalAlignment.Center)
                .WithVerticalAlignment(VerticalAlignment.Center)
                .WithStretch(Stretch.UniformToFill);
            TElement? elementLast = (await TryGetFirstGridElement(elementGrid, typeName + baseTagType + "_Last", defaultAssignAction))
                .WithHorizontalAlignment(HorizontalAlignment.Center)
                .WithVerticalAlignment(VerticalAlignment.Center)
                .WithStretch(Stretch.UniformToFill);

            // Return the current and last element
            return (elementCurrent, elementLast);
        }

        /// <summary>
        /// Try get the first element or create a new one from the parent <c>Grid</c> based on its tag type.
        /// </summary>
        /// <typeparam name="T">The type of the element to get (where <c>T</c> is a member of <c>FrameworkElement</c>)</typeparam>
        /// <param name="elementGrid">The parent <c>Grid</c> to get the element from.</param>
        /// <param name="tagType">The exact tag to determine the element type.</param>
        /// <param name="defaultAssignAction">The delegate to perform action after the new element is created.</param>
        /// <returns>Returns <c>null</c> if the <c>Grid</c> is null. Returns the new or existing element from the <c>Grid</c>.</returns>
        private static async Task<T?> TryGetFirstGridElement<T>(Grid elementGrid, string tagType, AssignDefaultAction<T>? defaultAssignAction = null)
            where T : FrameworkElement, new()
        {
            // If the parent grid is null, then return null
            if (elementGrid == null) return null;

            // Try find the existing element at the first pos of the
            // parent's grid with the corresponding tag. If not found,
            // assign it as null instead
            T? targetElement = elementGrid.Children.OfType<T>()
                .Where(x => x.Tag is string tagString && tagString == tagType)
                .FirstOrDefault();

            // If the existing element is not found, then starts
            // create a new one
            if (targetElement == null)
            {
                // Create a new instance of the element and add it into parent grid
                T newElement = elementGrid.AddElementToGridRowColumn(new T());
                UIElementExtensions.SetTag(newElement, tagType); // Set element tag

                // If a "assign action" delegate is defined, then execute the delegate
                if (defaultAssignAction != null) await defaultAssignAction(newElement);

                // Return a new instance of the element
                return newElement;
            }

            // Return an existing instance from the parent grid
            return targetElement;
        }

        /// <summary>
        /// Assign the default.png image to the new <c>Image</c> instance.
        /// </summary>
        /// <typeparam name="TElement">Type of an <c>Image</c>.</typeparam>
        /// <param name="element">Instance of an <c>Image</c>.</param>
        private static async Task AssignDefaultImage<TElement>(TElement element)
        {
            // If the element type is an "Image" type, then proceed
            if (element is Image image)
            {
                // Get the default.png path and check if it exists
                string filePath = LauncherConfig.AppDefaultBG;
                if (!File.Exists(filePath)) return;

                // Read the default.png image and load it to
                // the image element.
                using (FileStream fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read))
                {
                    BitmapImage imageSource = new BitmapImage();
                    await imageSource.SetSourceAsync(fileStream.AsRandomAccessStream());
                    image.Source = imageSource;
                }
            }
        }

        private static void EnsureCurrentImageRegistered()
        {
            if (_bgImageForeground == null || _bgImageForegroundLast == null) throw new ArgumentNullException("bgImageGridForeground");
            if (_bgImageBackground == null || _bgImageBackgroundLast == null) throw new ArgumentNullException("bgImageGridBackground");
        }

        private static void EnsureCurrentMediaPlayerRegistered()
        {
            if (_bgMediaPlayerBackground == null) throw new ArgumentNullException("bgMediaPlayerGrid");
        }

        private static void LoadBackground(string mediaPath)
        {
            MediaType mediaType = GetMediaType(mediaPath);

            switch (GetMediaType(mediaPath))
            {
                case MediaType.Media:
                    // TODO
                    break;
                case MediaType.StillImage:
                    // TODO
                    break;
                case MediaType.Unknown:
                default:
                    throw new FormatException($"Media format is unknown and cannot be determined!");

            }
        }

        private static MediaType GetMediaType(string mediaPath)
        {
            string extension = Path.GetExtension(mediaPath);
            if (_supportedImageExt.Contains(extension, StringComparer.InvariantCultureIgnoreCase))
                return MediaType.StillImage;

            if (_supportedMediaPlayerExt.Contains(extension, StringComparer.InvariantCultureIgnoreCase))
                return MediaType.Media;

            return MediaType.Unknown;
        }
    }
}
