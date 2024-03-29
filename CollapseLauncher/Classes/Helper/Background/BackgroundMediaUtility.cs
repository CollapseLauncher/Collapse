using CollapseLauncher.Extension;
using CollapseLauncher.Helper.Background.Loaders;
using Hi3Helper.Shared.Region;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

using ImageUI = Microsoft.UI.Xaml.Controls.Image;

#nullable enable
namespace CollapseLauncher.Helper.Background
{
    [System.Diagnostics.CodeAnalysis.SuppressMessageAttribute("ReSharper", "PossibleNullReferenceException")]
    [System.Diagnostics.CodeAnalysis.SuppressMessageAttribute("ReSharper", "AssignNullToNotNullAttribute")]
    internal static partial class BackgroundMediaUtility
    {
        internal enum MediaType { Media, StillImage, Unknown };

        internal const double TransitionDuration = 0.25d;
        internal const double TransitionDurationSlow = 0.5d;
        internal static readonly string[] _supportedImageExt = new string[] { ".jpg", ".jpeg", ".jfif", ".png", ".bmp", ".tiff", ".tif", ".webp" };
        internal static readonly string[] _supportedMediaPlayerExt = new string[] { ".mp4", ".mov", ".mkv", ".webm", ".avi", ".gif" };

        private static FrameworkElement? _parentUI = null;
        private static ImageUI? _bgImageBackground = null;
        private static ImageUI? _bgImageBackgroundLast = null;
        private static ImageUI? _bgImageForeground = null;
        private static ImageUI? _bgImageForegroundLast = null;
        private static MediaPlayerElement? _bgMediaPlayerBackground = null;

        private static Grid? _parentBgImageBackgroundGrid = null;
        private static Grid? _parentBgImageForegroundGrid = null;
        private static Grid? _parentBgMediaPlayerBackgroundGrid = null;

        internal static MediaType _currentAppliedMediaType = MediaType.Unknown;

        private static CancellationTokenSourceWrapper? _cancellationToken = null;
        private static StillImageLoader? _loaderStillImage = null;
        private static MediaPlayerLoader? _loaderMediaPlayer = null;
        private static IBackgroundMediaLoader? _currentMediaLoader = null;

        private static bool _isCurrentDimmAnimRun = false;
        private static bool _isCurrentUndimmAnimRun = false;
        private static bool _isCurrentRegistered = false;

        private delegate ValueTask AssignDefaultAction<T>(T element) where T : class;

        /// <summary>
        /// Attach and register the <c>Grid</c> of the page to be assigned with background utility.
        /// The <c>Grid</c> must be empty or have existing instances of previously registered grid.
        /// </summary>
        /// <param name="bgImageGridForeground">The parent <c>Grid</c> for Foreground Image.</param>
        /// <param name="bgImageGridBackground">The parent <c>Grid</c> for Background Image.</param>
        /// <param name="bgMediaPlayerGrid">The parent <c>Grid</c> for Background Media Player</param>
        internal static async Task RegisterCurrent(FrameworkElement? parentUI, Grid bgImageGridForeground, Grid bgImageGridBackground, Grid bgMediaPlayerGrid)
        {
            // Set the parent UI
            _parentUI = parentUI;

            // Initialize the background instances
            (_bgImageForeground, _bgImageForegroundLast) = await InitializeElementGrid<ImageUI>(bgImageGridForeground, "ImageForeground", AssignDefaultImage);
            (_bgImageBackground, _bgImageBackgroundLast) = await InitializeElementGrid<ImageUI>(bgImageGridBackground, "ImageBackground", AssignDefaultImage);
            _bgMediaPlayerBackground = (await TryGetFirstGridElement<MediaPlayerElement>(bgMediaPlayerGrid.WithOpacity(0), "MediaPlayer"))
                .WithHorizontalAlignment(HorizontalAlignment.Center)
                .WithVerticalAlignment(VerticalAlignment.Center)
                .WithStretch(Stretch.UniformToFill);

            // Store the parent grid reference into this static variables
            _parentBgImageForegroundGrid = bgImageGridForeground;
            _parentBgImageBackgroundGrid = bgImageGridBackground;
            _parentBgMediaPlayerBackgroundGrid = bgMediaPlayerGrid;

            // Set that the current page has been registered
            _isCurrentRegistered = true;
        }

        /// <summary>
        /// Detach the current background utility from the previously attached <c>Grid</c>.
        /// </summary>
        internal static void DetachCurrent()
        {
            if (!_cancellationToken?.IsCancellationRequested ?? false)
                _cancellationToken?.Cancel();

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

            _loaderMediaPlayer = null;
            _loaderStillImage = null;

            _currentAppliedMediaType = MediaType.Unknown;

            _isCurrentRegistered = false;
        }

        /// <summary>
        /// Initialize or to find an existing element by its base tag and type.
        /// </summary>
        /// <typeparam name="TElement">The type of the element need to be created (where <c>TElement</c> is a member of <c>FrameworkElement</c>)</typeparam>
        /// <param name="elementGrid">The parent <c>Grid</c> where the element is going to be or has stored.</param>
        /// <param name="baseTagType">The base tag to determine the element type.</param>
        /// <param name="defaultAssignAction">The delegate to perform action after the new element is created.</param>
        /// <returns>The tuple of the new "current and last" instance of the element.</returns>
        private static async ValueTask<(TElement?, TElement?)> InitializeElementGrid<TElement>(Grid elementGrid, string baseTagType, AssignDefaultAction<TElement>? defaultAssignAction = null)
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
        private static async ValueTask<T?> TryGetFirstGridElement<T>(Grid elementGrid, string tagType, AssignDefaultAction<T>? defaultAssignAction = null)
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
        internal static async ValueTask AssignDefaultImage<TElement>(TElement element)
        {
            // If the element type is an "Image" type, then proceed
            if (element is ImageUI image)
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

        /// <summary>
        /// Ensure that the <see cref="ImageUI"/> instance is already initialized
        /// </summary>
        /// <exception cref="ArgumentNullException">Throw if <see cref="ImageUI"/> instance is not registered</exception>
        private static void EnsureCurrentImageRegistered()
        {
            if (_bgImageForeground == null || _bgImageForegroundLast == null) throw new ArgumentNullException("bgImageGridForeground");
            if (_bgImageBackground == null || _bgImageBackgroundLast == null) throw new ArgumentNullException("bgImageGridBackground");
        }

        /// <summary>
        /// Ensure that the <see cref="MediaPlayerElement"/> instance is already initialized
        /// </summary>
        /// <exception cref="ArgumentNullException">Throw if <see cref="MediaPlayerElement"/> instance is not registered</exception>
        private static void EnsureCurrentMediaPlayerRegistered()
        {
            if (_bgMediaPlayerBackground == null) throw new ArgumentNullException("bgMediaPlayerGrid");
        }

        /// <summary>
        /// Load Still Image or Video as a background.
        /// </summary>
        /// <param name="mediaPath">Path of the background file</param>
        /// <param name="isRequestInit">Request an initialization before processing the background file</param>
        /// <param name="isForceRecreateCache">Request a cache recreation if the background file properties have been cached</param>
        /// <exception cref="FormatException">Throws if the background file is not supported</exception>
        /// <exception cref="NullReferenceException">Throws if some instances aren't yet initialized</exception>
        internal static async Task LoadBackground(string mediaPath, bool isRequestInit = false, bool isForceRecreateCache = false)
        {
            while (!_isCurrentRegistered) await Task.Delay(250, _cancellationToken?.Token ?? default);

            EnsureCurrentImageRegistered();
            EnsureCurrentMediaPlayerRegistered();

            if (_loaderMediaPlayer == null)
                _loaderMediaPlayer = new MediaPlayerLoader(
                    _parentUI!,
                    _parentBgMediaPlayerBackgroundGrid!,
                    _bgMediaPlayerBackground);

            if (_loaderStillImage == null)
                _loaderStillImage = new StillImageLoader(
                    _parentUI!,
                    _parentBgImageForegroundGrid!, _parentBgImageBackgroundGrid!,
                    _bgImageForeground, _bgImageForegroundLast,
                    _bgImageBackground, _bgImageBackgroundLast,
                    TransitionDuration);

            MediaType mediaType = GetMediaType(mediaPath);

            switch (mediaType)
            {
                case MediaType.Media:
                    _currentMediaLoader = _loaderMediaPlayer;
                    break;
                case MediaType.StillImage:
                    _currentMediaLoader = _loaderStillImage;
                    break;
                case MediaType.Unknown:
                default:
                    throw new FormatException($"Media format is unknown and cannot be determined!");
            }

            if (_currentMediaLoader == null) throw new NullReferenceException("No background image loader is assigned!");

            if (_cancellationToken != null && !_cancellationToken.IsDisposed)
            {
                if (!_cancellationToken.IsCancelled) await _cancellationToken.CancelAsync();
                _cancellationToken.Dispose();
            }
            _cancellationToken = new CancellationTokenSourceWrapper();
            await _currentMediaLoader.LoadAsync(mediaPath, isForceRecreateCache, isRequestInit, _cancellationToken?.Token ?? default);

            if (mediaType == MediaType.Media && _currentAppliedMediaType == MediaType.Unknown)
                await _loaderStillImage.DimmAsync(_cancellationToken?.Token ?? default);

            if (_currentAppliedMediaType != mediaType && _currentAppliedMediaType != MediaType.Unknown && mediaType == MediaType.Media)
                await _loaderStillImage!.HideAsync(_cancellationToken?.Token ?? default);
            else if (_currentAppliedMediaType != mediaType && _currentAppliedMediaType != MediaType.Unknown && mediaType == MediaType.StillImage)
                await _loaderMediaPlayer!.HideAsync(_cancellationToken?.Token ?? default);

            if (InnerLauncherConfig.m_appCurrentFrameName != "HomePage")
            {
                _loaderMediaPlayer._isMediaPlayerDimm = true;
                _loaderStillImage._isImageDimm = true;
            }

            if (mediaType == MediaType.Media || InnerLauncherConfig.m_appCurrentFrameName != "HomePage")
                await _currentMediaLoader.ShowAsync(_cancellationToken?.Token ?? default);

            _currentAppliedMediaType = mediaType;
        }

        /// <summary>
        /// Dimming the current loaded background
        /// </summary>
        internal static async void Dimm()
        {
            while (_isCurrentDimmAnimRun || _isCurrentUndimmAnimRun) { await Task.Delay(250); }

            try
            {
                _isCurrentDimmAnimRun = true;
                await GetImageLoader(_currentAppliedMediaType)!
                     .DimmAsync(_cancellationToken?.Token ?? default);
            }
            finally
            {
                _isCurrentDimmAnimRun = false;
            }
        }

        /// <summary>
        /// Undimming the current loaded background
        /// </summary>
        internal static async void Undimm()
        {
            while (_isCurrentDimmAnimRun || _isCurrentUndimmAnimRun) { await Task.Delay(250); }

            try
            {
                _isCurrentUndimmAnimRun = true;
                await GetImageLoader(_currentAppliedMediaType)!
                     .UndimmAsync(_cancellationToken?.Token ?? default);
            }
            finally
            {
                _isCurrentUndimmAnimRun = false;
            }
        }

        /// <summary>
        /// Mute the audio of the currently loaded background
        /// </summary>
        internal static void Mute() => _currentMediaLoader?.Mute();

        /// <summary>
        /// Unmute the audio of the currently loaded background
        /// </summary>
        internal static void Unmute() => _currentMediaLoader?.Unmute();

        /// <summary>
        /// Set the volume of the audio from the currently loaded background
        /// </summary>
        internal static void SetVolume(double value) => _currentMediaLoader?.SetVolume(value);

        /// <summary>
        /// Trigger the unfocused window event to the currently loaded background
        /// </summary>
        internal static void WindowUnfocused() => _currentMediaLoader?.WindowUnfocused();

        /// <summary>
        /// Trigger the focused window event to the currently loaded background
        /// </summary>
        internal static void WindowFocused() => _currentMediaLoader?.WindowFocused();

        /// <summary>
        /// Play/Resume the currently loaded background
        /// </summary>
        internal static void Play() => _currentMediaLoader?.Play();

        /// <summary>
        /// Pause the currently loaded background
        /// </summary>
        internal static void Pause() => _currentMediaLoader?.Pause();

        private static IBackgroundMediaLoader? GetImageLoader(MediaType mediaType)
            => mediaType switch
            {
                MediaType.StillImage => _loaderStillImage,
                MediaType.Media => _loaderMediaPlayer,
                _ => throw new NotImplementedException()
            };

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
