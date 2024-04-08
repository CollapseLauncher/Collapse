using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CollapseLauncher.Extension;
using CollapseLauncher.Helper.Background.Loaders;
using Hi3Helper.Shared.Region;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using ImageUI = Microsoft.UI.Xaml.Controls.Image;

#nullable enable
namespace CollapseLauncher.Helper.Background
{
    [SuppressMessage("ReSharper", "IdentifierTypo")]
    [SuppressMessage("ReSharper", "PossibleNullReferenceException")]
    [SuppressMessage("ReSharper", "AssignNullToNotNullAttribute")]
    internal class BackgroundMediaUtility : IDisposable
    {
        internal static BackgroundMediaUtility? Current { get; private set; }

        internal enum MediaType
        {
            Media,
            StillImage,
            Unknown
        }

        internal const double TransitionDuration     = 0.25d;
        internal const double TransitionDurationSlow = 0.5d;

        internal static readonly string[] SupportedImageExt =
            [".jpg", ".jpeg", ".jfif", ".png", ".bmp", ".tiff", ".tif", ".webp"];

        internal static readonly string[] SupportedMediaPlayerExt =
            [".mp4", ".mov", ".mkv", ".webm", ".avi", ".gif"];

        private FrameworkElement?   _parentUI;
        private ImageUI?            _bgImageBackground;
        private ImageUI?            _bgImageBackgroundLast;
        private MediaPlayerElement? _bgMediaPlayerBackground;

        private Grid? _bgAcrylicMask;
        private Grid? _bgOverlayTitleBar;

        private Grid? _parentBgImageBackgroundGrid;
        private Grid? _parentBgMediaPlayerBackgroundGrid;

        internal MediaType CurrentAppliedMediaType = MediaType.Unknown;

        private CancellationTokenSourceWrapper? _cancellationToken;
        private IBackgroundMediaLoader?         _loaderStillImage;
        private IBackgroundMediaLoader?         _loaderMediaPlayer;
        private IBackgroundMediaLoader?         _currentMediaLoader;

        private bool _isCurrentRegistered;

        private static FileStream? _alternativeFileStream;

        private delegate ValueTask AssignDefaultAction<in T>(T element) where T : class;

        /// <summary>
        ///     Attach and register the <see cref="Grid" /> of the page to be assigned with background utility.
        ///     The <see cref="Grid" /> must be empty or have existing instances of previously registered <see cref="Grid" />.
        /// </summary>
        /// <param name="parentUI">The parent UI to be assigned for the media elements.</param>
        /// <param name="bgAcrylicMask">The acrylic mask for Background Image.</param>
        /// <param name="bgOverlayTitleBar">The title bar shadow over Background Image.</param>
        /// <param name="bgImageGridBackground">The parent <see cref="Grid" /> for Background Image.</param>
        /// <param name="bgMediaPlayerGrid">The parent <see cref="Grid" /> for Background Media Player</param>
        internal static async Task RegisterCurrent(FrameworkElement? parentUI,          Grid bgAcrylicMask,
                                                                          Grid              bgOverlayTitleBar, Grid bgImageGridBackground,
                                                                          Grid              bgMediaPlayerGrid)
        {
            // Set the parent UI
            FrameworkElement? ui = parentUI;

            // Initialize the background instances
            var (bgImageBackground, bgImageBackgroundLast) =
                await InitializeElementGrid<ImageUI>(bgImageGridBackground, "ImageBackground", AssignDefaultImage);
            var bgMediaPlayerBackground =
                (await TryGetFirstGridElement<MediaPlayerElement>(bgMediaPlayerGrid.WithOpacity(0), "MediaPlayer"))
               .WithHorizontalAlignment(HorizontalAlignment.Center)
               .WithVerticalAlignment(VerticalAlignment.Center)
               .WithStretch(Stretch.UniformToFill);

            Current = new BackgroundMediaUtility(ui, bgAcrylicMask, bgOverlayTitleBar,
                                               bgImageGridBackground, bgMediaPlayerGrid, bgImageBackground,
                                               bgImageBackgroundLast, bgMediaPlayerBackground);
        }

        private BackgroundMediaUtility(FrameworkElement? parentUI,              Grid bgAcrylicMask,
                                       Grid              bgOverlayTitleBar,     Grid bgImageGridBackground,
                                       Grid              bgMediaPlayerGrid,     ImageUI? bgImageBackground,
                                       ImageUI?          bgImageBackgroundLast, MediaPlayerElement? mediaPlayerElement)
        {
            _parentUI                          = parentUI;
            _bgAcrylicMask                     = bgAcrylicMask;
            _bgOverlayTitleBar                 = bgOverlayTitleBar;
            _parentBgImageBackgroundGrid       = bgImageGridBackground;
            _parentBgMediaPlayerBackgroundGrid = bgMediaPlayerGrid;

            _bgImageBackground       = bgImageBackground;
            _bgImageBackgroundLast   = bgImageBackgroundLast;
            _bgMediaPlayerBackground = mediaPlayerElement;

            // Set that the current page has been registered
            _isCurrentRegistered = true;
        }

        ~BackgroundMediaUtility() => Dispose();

        /// <summary>
        ///     Detach and dispose the current background utility from the previously attached <see cref="Grid" />.
        /// </summary>
        public void Dispose()
        {
            if (_cancellationToken is { IsCancellationRequested: false })
            {
                _cancellationToken.Cancel();
            }
            _cancellationToken?.Dispose();

            _bgImageBackground       = null;
            _bgImageBackgroundLast   = null;
            _bgMediaPlayerBackground = null;

            _parentBgImageBackgroundGrid       = null;
            _parentBgMediaPlayerBackgroundGrid = null;
            _bgAcrylicMask                     = null;
            _bgOverlayTitleBar                 = null;

            _loaderMediaPlayer?.Dispose();
            _loaderMediaPlayer = null;
            _loaderStillImage?.Dispose();
            _loaderStillImage  = null;

            _alternativeFileStream?.Dispose();
            _alternativeFileStream = null;

            CurrentAppliedMediaType = MediaType.Unknown;

            _isCurrentRegistered = false;
            GC.SuppressFinalize(this);
        }

        /// <summary>
        ///     Initialize or to find an existing element by its base tag and type.
        /// </summary>
        /// <typeparam name="TElement">
        ///     The type of the element need to be created (where <c>TElement</c> is a member of
        ///     <c>FrameworkElement</c>)
        /// </typeparam>
        /// <param name="elementGrid">The parent <c>Grid</c> where the element is going to be or has stored.</param>
        /// <param name="baseTagType">The base tag to determine the element type.</param>
        /// <param name="defaultAssignAction">The delegate to perform action after the new element is created.</param>
        /// <returns>The tuple of the new "current and last" instance of the element.</returns>
        private static async ValueTask<(TElement?, TElement?)> InitializeElementGrid<TElement>(Grid elementGrid,
                                                                                               string baseTagType, AssignDefaultAction<TElement>? defaultAssignAction = null)
            where TElement : FrameworkElement, new()
        {
            // Get the type name of the element
            string typeName = typeof(TElement).Name + '_';

            // Find or create the element from/to the parent grid.
            TElement? elementCurrent =
                (await TryGetFirstGridElement(elementGrid, typeName + baseTagType + "_Current", defaultAssignAction))
               .WithHorizontalAlignment(HorizontalAlignment.Center)
               .WithVerticalAlignment(VerticalAlignment.Bottom)
               .WithStretch(Stretch.UniformToFill);
            TElement? elementLast =
                (await TryGetFirstGridElement(elementGrid, typeName + baseTagType + "_Last", defaultAssignAction))
               .WithHorizontalAlignment(HorizontalAlignment.Center)
               .WithVerticalAlignment(VerticalAlignment.Bottom)
               .WithStretch(Stretch.UniformToFill);

            // Return the current and last element
            return (elementCurrent, elementLast);
        }

        /// <summary>
        ///     Try to get the first element or create a new one from the parent <c>Grid</c> based on its tag type.
        /// </summary>
        /// <typeparam name="T">The type of the element to get (where <c>T</c> is a member of <c>FrameworkElement</c>)</typeparam>
        /// <param name="elementGrid">The parent <c>Grid</c> to get the element from.</param>
        /// <param name="tagType">The exact tag to determine the element type.</param>
        /// <param name="defaultAssignAction">The delegate to perform action after the new element is created.</param>
        /// <returns>Returns <c>null</c> if the <c>Grid</c> is null. Returns the new or existing element from the <c>Grid</c>.</returns>
        private static async ValueTask<T?> TryGetFirstGridElement<T>(Grid? elementGrid, string tagType,
                                                                     AssignDefaultAction<T>? defaultAssignAction = null)
            where T : FrameworkElement, new()
        {
            // If the parent grid is null, then return null
            if (elementGrid == null) return null;

            // Try to find the existing element at the first pos of the
            // parent's grid with the corresponding tag. If not found,
            // assign it as null instead
            T? targetElement = elementGrid.Children
                                          .OfType<T>()
                                          .FirstOrDefault(x => x.Tag is string tagString && tagString == tagType);

            // If the existing element is not found, then starts
            // create a new one
            if (targetElement != null) return targetElement; // Return an existing instance from the parent grid

            // Create a new instance of the element and add it into parent grid
            T newElement = elementGrid.AddElementToGridRowColumn(new T());
            newElement.SetTag(tagType); // Set element tag

            // If an "assign action" delegate is defined, then execute the delegate
            if (defaultAssignAction != null)
            {
                await defaultAssignAction(newElement);
            }

            // Return a new instance of the element
            return newElement;
        }

        /// <summary>
        ///     Assign the default.png image to the new <c>Image</c> instance.
        /// </summary>
        /// <typeparam name="TElement">Type of <c>Image</c>.</typeparam>
        /// <param name="element">Instance of an <c>Image</c>.</param>
        internal static async ValueTask AssignDefaultImage<TElement>(TElement element)
        {
            // If the element type is an "Image" type, then proceed
            if (element is ImageUI image)
            {
                // Get the default.png path and check if it exists
                string filePath = LauncherConfig.AppDefaultBG;
                if (!File.Exists(filePath))
                {
                    return;
                }

                // Read the default.png image and load it to
                // the image element.
                await using FileStream fileStream =
                    new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
                BitmapImage imageSource = new BitmapImage();
                await imageSource.SetSourceAsync(fileStream.AsRandomAccessStream());
                image.Source = imageSource;
            }
        }

        /// <summary>
        ///     Ensure that the <see cref="ImageUI" /> instance is already initialized
        /// </summary>
        /// <exception cref="ArgumentNullException">Throw if <see cref="ImageUI" /> instance is not registered</exception>
        private void EnsureCurrentImageRegistered()
        {
            if (_bgImageBackground == null || _bgImageBackgroundLast == null)
            {
                throw new NullReferenceException("bgImageGridBackground instance is null");
            }
        }

        /// <summary>
        ///     Ensure that the <see cref="MediaPlayerElement" /> instance is already initialized
        /// </summary>
        /// <exception cref="ArgumentNullException">Throw if <see cref="MediaPlayerElement" /> instance is not registered</exception>
        private void EnsureCurrentMediaPlayerRegistered()
        {
            if (_bgMediaPlayerBackground == null)
            {
                throw new NullReferenceException("bgMediaPlayerGrid instance is null");
            }
        }

        /// <summary>
        ///     Load Still Image or Video as a background.
        /// </summary>
        /// <param name="mediaPath">Path of the background file</param>
        /// <param name="isRequestInit">Request an initialization before processing the background file</param>
        /// <param name="isForceRecreateCache">Request a cache recreation if the background file properties have been cached</param>
        /// <exception cref="FormatException">Throws if the background file is not supported</exception>
        /// <exception cref="NullReferenceException">Throws if some instances aren't yet initialized</exception>
        internal async Task LoadBackground(string mediaPath, bool isRequestInit = false,
                                                  bool   isForceRecreateCache = false)
        {
            while (!_isCurrentRegistered)
            {
                await Task.Delay(250,  _cancellationToken?.Token ?? default);
            }

            EnsureCurrentImageRegistered();
            EnsureCurrentMediaPlayerRegistered();

            _loaderMediaPlayer ??= new MediaPlayerLoader(
                                                         _parentUI!,
                                                         _bgAcrylicMask!, _bgOverlayTitleBar!,
                                                         _parentBgMediaPlayerBackgroundGrid!,
                                                         _bgMediaPlayerBackground);

            _loaderStillImage ??= new StillImageLoader(
                                                       _parentUI!,
                                                       _bgAcrylicMask!, _bgOverlayTitleBar!,
                                                       _parentBgImageBackgroundGrid!,
                                                       _bgImageBackground, _bgImageBackgroundLast);

            MediaType mediaType = GetMediaType(mediaPath);

            _currentMediaLoader = mediaType switch
                                  {
                                      MediaType.Media => _loaderMediaPlayer,
                                      MediaType.StillImage => _loaderStillImage,
                                      MediaType.Unknown =>
                                          throw new
                                              FormatException("Media format is unknown and cannot be determined!"),
                                      _ => throw new
                                          FormatException("Media format is unknown and cannot be determined!")
                                  };

            if (_currentMediaLoader == null)
            {
                throw new NullReferenceException("No background image loader is assigned!");
            }

            if (_cancellationToken is { IsDisposed: false })
            {
                if (!_cancellationToken.IsCancelled)
                {
                    await _cancellationToken.CancelAsync();
                }

                _cancellationToken.Dispose();
            }

            _cancellationToken = new CancellationTokenSourceWrapper();
            await _currentMediaLoader.LoadAsync(mediaPath, isForceRecreateCache, isRequestInit,
                                                _cancellationToken.Token);

            if (CurrentAppliedMediaType != mediaType && CurrentAppliedMediaType != MediaType.Unknown &&
                mediaType == MediaType.Media)
            {
                await _loaderStillImage.HideAsync(_cancellationToken.Token);
            }
            else if (CurrentAppliedMediaType != mediaType && CurrentAppliedMediaType != MediaType.Unknown &&
                     mediaType == MediaType.StillImage)
            {
                await _loaderMediaPlayer.HideAsync(_cancellationToken.Token);
            }

            if (InnerLauncherConfig.m_appCurrentFrameName != "HomePage")
            {
                _loaderMediaPlayer.IsBackgroundDimm = true;
            }

            if ((mediaType == MediaType.Media || InnerLauncherConfig.m_appCurrentFrameName != "HomePage") && CurrentAppliedMediaType != mediaType)
            {
                await _currentMediaLoader.ShowAsync(_cancellationToken.Token);
            }

            CurrentAppliedMediaType = mediaType;
        }

        /// <summary>
        ///     Dimming the current loaded background
        /// </summary>
        internal void Dimm()
        {
            IBackgroundMediaLoader? loader = GetImageLoader(CurrentAppliedMediaType);
            if (loader == null) return;

            loader.Dimm(_cancellationToken?.Token ?? default);
        }

        /// <summary>
        ///     Undimming the current loaded background
        /// </summary>
        internal void Undimm()
        {
            IBackgroundMediaLoader? loader = GetImageLoader(CurrentAppliedMediaType);
            if (loader == null) return;

            loader.Undimm(_cancellationToken?.Token ?? default);
        }

        /// <summary>
        ///     Mute the audio of the currently loaded background
        /// </summary>
        internal void Mute()
        {
            _currentMediaLoader?.Mute();
        }

        /// <summary>
        ///     Unmute the audio of the currently loaded background
        /// </summary>
        internal void Unmute()
        {
            _currentMediaLoader?.Unmute();
        }

        /// <summary>
        ///     Set the volume of the audio from the currently loaded background
        /// </summary>
        internal void SetVolume(double value)
        {
            _currentMediaLoader?.SetVolume(value);
        }

        /// <summary>
        ///     Trigger the unfocused window event to the currently loaded background
        /// </summary>
        internal void WindowUnfocused()
        {
            _currentMediaLoader?.WindowUnfocused();
        }

        /// <summary>
        ///     Trigger the focused window event to the currently loaded background
        /// </summary>
        internal void WindowFocused()
        {
            _currentMediaLoader?.WindowFocused();
        }

        /// <summary>
        ///     Play/Resume the currently loaded background
        /// </summary>
        internal void Play()
        {
            _currentMediaLoader?.Play();
        }

        /// <summary>
        ///     Pause the currently loaded background
        /// </summary>
        internal void Pause()
        {
            _currentMediaLoader?.Pause();
        }

        public static FileStream? GetAlternativeFileStream()
        {
            FileStream? returnStream = _alternativeFileStream;
            _alternativeFileStream = null;
            return returnStream;
        }

        public static void SetAlternativeFileStream(FileStream stream)
        {
            _alternativeFileStream = stream;
        }

        private IBackgroundMediaLoader? GetImageLoader(MediaType mediaType)
        {
            return mediaType switch
                   {
                       MediaType.StillImage => _loaderStillImage,
                       MediaType.Media => _loaderMediaPlayer,
                       MediaType.Unknown => null,
                       _ => throw new NotImplementedException()
                   };
        }

        public static MediaType GetMediaType(string mediaPath)
        {
            string extension = Path.GetExtension(mediaPath);
            if (SupportedImageExt.Contains(extension, StringComparer.InvariantCultureIgnoreCase))
            {
                return MediaType.StillImage;
            }

            return SupportedMediaPlayerExt.Contains(extension, StringComparer.InvariantCultureIgnoreCase) ? MediaType.Media : MediaType.Unknown;
        }
    }
}