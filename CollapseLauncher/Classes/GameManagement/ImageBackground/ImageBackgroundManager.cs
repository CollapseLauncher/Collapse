using CollapseLauncher.Extension;
using CollapseLauncher.Helper.Image;
using CollapseLauncher.Helper.LauncherApiLoader.HoYoPlay;
using CollapseLauncher.Helper.Metadata;
using CollapseLauncher.Interfaces.Class;
using CollapseLauncher.XAMLs.Theme.CustomControls.LayeredBackgroundImage;
using Hi3Helper.SentryHelper;
using Hi3Helper.Shared.Region;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Data;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Windows.UI;
using WinRT;
// ReSharper disable CheckNamespace
#pragma warning disable IDE0130

#nullable enable
namespace CollapseLauncher.GameManagement.ImageBackground;

[GeneratedBindableCustomProperty]
public partial class ImageBackgroundManager
    : NotifyPropertyChanged
{
    internal static ImageBackgroundManager Shared
    {
        get;
    } = new();

    #region Shared/Static Properties and Fields

    private const string GlobalCustomBackgroundConfigKey                  = "GlobalBg";
    private const string GlobalIsEnableCustomImageConfigKey               = "GlobalIsCustomImageEnabled";
    private const string GlobalIsBackgroundParallaxEffectEnabledConfigKey = "GlobalIsBackgroundParallaxEffectEnabled";
    private const string GlobalBackgroundParallaxPixelShiftConfigKey      = "GlobalBackgroundParallaxPixelShift";
    private const string GlobalBackgroundAudioVolumeConfigKey             = "GlobalBackgroundAudioVolume";
    private const string GlobalBackgroundAudioEnabledConfigKey            = "GlobalBackgroundAudioEnabled";

    public string? GlobalCustomBackgroundImagePath
    {
        get => LauncherConfig.GetAppConfigValue(GlobalCustomBackgroundConfigKey);
        set
        {
            LauncherConfig.SetAndSaveConfigValue(GlobalCustomBackgroundConfigKey, value);
            OnPropertyChanged();
        }
    }

    public bool GlobalIsEnableCustomImage
    {
        get => LauncherConfig.GetAppConfigValue(GlobalIsEnableCustomImageConfigKey);
        set
        {
            LauncherConfig.SetAndSaveConfigValue(GlobalIsEnableCustomImageConfigKey, value);
            OnPropertyChanged();
            InitializeCore();
        }
    }

    public bool GlobalIsWaifu2XEnabled
    {
        get => ImageLoaderHelper.IsWaifu2XEnabled && ImageLoaderHelper.IsWaifu2XUsable;
        set
        {
            ImageLoaderHelper.IsWaifu2XEnabled = value;
            OnPropertyChanged();
        }
    }

    public FrameworkElement? GlobalParallaxHoverSource
    {
        get;
        set
        {
            field = value;
            OnPropertyChanged();
        }
    }

    public bool GlobalIsBackgroundParallaxEffectEnabled
    {
        get => LauncherConfig.GetAppConfigValue(GlobalIsBackgroundParallaxEffectEnabledConfigKey);
        set
        {
            LauncherConfig.SetAndSaveConfigValue(GlobalIsBackgroundParallaxEffectEnabledConfigKey, value);
            OnPropertyChanged();
        }
    }

    public double GlobalBackgroundParallaxPixelShift
    {
        get => LauncherConfig.GetAppConfigValue(GlobalBackgroundParallaxPixelShiftConfigKey);
        set
        {
            LauncherConfig.SetAndSaveConfigValue(GlobalBackgroundParallaxPixelShiftConfigKey, value);
            OnPropertyChanged();
        }
    }

    public double GlobalBackgroundAudioVolume
    {
        get => LauncherConfig.GetAppConfigValue(GlobalBackgroundAudioVolumeConfigKey);
        set
        {
            LauncherConfig.SetAndSaveConfigValue(GlobalBackgroundAudioVolumeConfigKey, value);
            OnPropertyChanged();
        }
    }

    public bool GlobalBackgroundAudioEnabled
    {
        get => LauncherConfig.GetAppConfigValue(GlobalBackgroundAudioEnabledConfigKey);
        set
        {
            LauncherConfig.SetAndSaveConfigValue(GlobalBackgroundAudioEnabledConfigKey, value);
            OnPropertyChanged();
        }
    }

    public bool IsBackgroundElevated
    {
        get;
        set
        {
            field = value;
            OnPropertyChanged();
        }
    }

    public double ForegroundOpacity
    {
        get;
        set
        {
            field = value;
            OnPropertyChanged();
        }
    } = 1d;

    public double SmokeOpacity
    {
        get;
        set
        {
            field = value;
            OnPropertyChanged();
        }
    }

    #endregion

    #region Shared/Static Events

    internal event Action<Color>? ColorAccentChanged;

    #endregion


    #region This Instance Properties

    private string                    CurrentCustomBackgroundConfigKey    { get; set; } = "";
    private string                    CurrentIsEnableCustomImageConfigKey { get; set; } = "";
    private string                    CurrentSelectedBackgroundIndexKey   { get; set; } = "";
    private HypLauncherBackgroundApi? CurrentBackgroundApi                { get; set; }
    private Grid?                     PresenterGrid                       { get; set; }
    private PresetConfig?             PresetConfig                        { get; set; }

    public string? CurrentCustomBackgroundImagePath
    {
        get => LauncherConfig.GetAppConfigValue(CurrentCustomBackgroundConfigKey);
        set
        {
            LauncherConfig.SetAndSaveConfigValue(CurrentCustomBackgroundConfigKey, value);
            OnPropertyChanged();
        }
    }

    public bool CurrentIsEnableCustomImage
    {
        get => LauncherConfig.GetAppConfigValue(CurrentIsEnableCustomImageConfigKey);
        set
        {
            LauncherConfig.SetAndSaveConfigValue(CurrentIsEnableCustomImageConfigKey, value);
            OnPropertyChanged();
            InitializeCore();
        }
    }

    public int CurrentSelectedBackgroundIndex
    {
        get
        {
            int value = LauncherConfig.GetAppConfigValue(CurrentSelectedBackgroundIndexKey);
            if (value > ImageContextSources.Count - 1 ||
                value < 0)
            {
                return 0;
            }

            return value;
        }
        set
        {
            if (value > ImageContextSources.Count - 1 ||
                value < 0)
            {
                return;
            }

            LauncherConfig.SetAndSaveConfigValue(CurrentSelectedBackgroundIndexKey, value);
            OnPropertyChanged();
            LoadImageAtIndex(value, CancellationToken.None);
        }
    }

    public LayeredImageBackgroundContext? CurrentSelectedBackgroundContext
    {
        get
        {
            int index = CurrentSelectedBackgroundIndex;
            return index > ImageContextSources.Count - 1 ||
                   index < 0
                ? null
                : ImageContextSources[CurrentSelectedBackgroundIndex];
        }
    }

    public LayeredBackgroundImage? CurrentBackgroundElement
    {
        get => PresenterGrid?.Children.LastOrDefault() as LayeredBackgroundImage;
    }

    public bool CurrentSelectedBackgroundHasOverlayImage => !string.IsNullOrEmpty(CurrentSelectedBackgroundContext?.OriginOverlayImagePath);

    public int CurrentBackgroundCount
    {
        get => ImageContextSources.Count;
    }

    /// <summary>
    /// The collection of image context sources.<br/><br/>
    /// Notes to Dev:<br/>
    /// This collection SHOULDN'T BE SET outside this instance.<br/>
    /// To set the <c>ItemsSource</c> to any other ItemsTemplate's element, please use <see cref="Binding"/> to make sure the changes always be tracked!<br/>
    /// Also, the use of <see cref="ObservableCollection{T}"/> is intended and REQUIRED to make sure the changes are tracked properly.
    /// </summary>
    public ObservableCollection<LayeredImageBackgroundContext> ImageContextSources
    {
        get;
    } = [];

    #endregion

    /// <summary>
    /// Initialize background images of for a region. This method MUST be called everytime the region is loaded.
    /// </summary>
    /// <param name="presetConfig">The preset config of the current game region.</param>
    /// <param name="backgroundApi">The background API implementation of the current game region.</param>
    /// <param name="presenterGrid">Presenter Grid which the element of the background will be shown on.</param>
    /// <param name="token">Cancellation token to cancel asynchronous operations.</param>
    public void Initialize(PresetConfig?             presetConfig,
                           HypLauncherBackgroundApi? backgroundApi,
                           Grid?                     presenterGrid,
                           CancellationToken         token = default)
    {
        ArgumentNullException.ThrowIfNull(presetConfig);
        ArgumentNullException.ThrowIfNull(presenterGrid);

        CurrentCustomBackgroundConfigKey    = $"LastCustomBg-{presetConfig.GameName}-{presetConfig.ZoneName}";
        CurrentSelectedBackgroundIndexKey   = $"LastCustomBgIndex-{presetConfig.GameName}-{presetConfig.ZoneName}";
        CurrentIsEnableCustomImageConfigKey = $"LastIsCustomImageEnabled-{presetConfig.GameName}-{presetConfig.ZoneName}";

        PresetConfig         = presetConfig;
        PresenterGrid        = presenterGrid;
        CurrentBackgroundApi = backgroundApi;

        InitializeCore(token);
    }

    private void InitializeCore(CancellationToken token = default)
    {
        new Thread(Impl)
        {
            IsBackground = true,
        }.Start();

        return;

        async void Impl()
        {
            try
            {
                if (PresetConfig == null)
                {
                    throw new InvalidOperationException($"{nameof(PresetConfig)} is uninitialized!");
                }

                // -- Try to initialize custom image first
                //    -- A. Check for per-region custom background
                if (CurrentIsEnableCustomImage &&
                    await SetCurrentCustomBackground(CurrentCustomBackgroundImagePath, false, false, token))
                {
                    return;
                }

                //   -- B. Check for global custom background
                if (!CurrentIsEnableCustomImage &&
                    GlobalIsEnableCustomImage &&
                    await SetGlobalCustomBackground(GlobalCustomBackgroundImagePath, false, false, token))
                {
                    return;
                }

                // -- If no custom background is used, then fallback to background provided by API or fallback background.
                List<LayeredImageBackgroundContext> imageContexts = [];

                try
                {
                    // -- Check 1: Add placeholder ones if the API is not implemented.
                    if (CurrentBackgroundApi?.Data is not { GameContentList: { Count: > 0 } contextList })
                    {
                        string bgPlaceholderPath = GetPlaceholderBackgroundImageFrom(PresetConfig);
                        imageContexts.Add(new LayeredImageBackgroundContext
                        {
                            OriginBackgroundImagePath = bgPlaceholderPath,
                            BackgroundImagePath       = bgPlaceholderPath
                        });
                        return;
                    }

                    // -- Check 2: Use ones provided by the API
                    // ReSharper disable once LoopCanBeConvertedToQuery
                    foreach (HypLauncherBackgroundContentKindData contextEntry in contextList.SelectMany(x => x.Backgrounds))
                    {
                        string? overlayImagePath = contextEntry.BackgroundOverlay?.ImageUrl;
                        string? backgroundImagePath = contextEntry.BackgroundVideo?.ImageUrl ??
                                                      contextEntry.BackgroundImage?.ImageUrl;

                        imageContexts.Add(new LayeredImageBackgroundContext
                        {
                            OriginOverlayImagePath    = overlayImagePath,
                            OriginBackgroundImagePath = backgroundImagePath,
                            OverlayImagePath          = overlayImagePath,
                            BackgroundImagePath       = backgroundImagePath
                        });
                    }
                }
                finally
                {
                    UpdateContextListCore(token, false, imageContexts);
                }
            }
            catch (Exception ex)
            {
                SentryHelper.ExceptionHandler(ex);
                // ignore
            }
        }
    }

    public Task<bool> SetGlobalCustomBackground(string? imagePath, bool performCropRequest = true, bool skipPreviousContextCheck = true, CancellationToken token = default)
    {
        GlobalCustomBackgroundImagePath = imagePath;
        // For global custom background, pass CurrentIsEnableCustomImage status so when it's set to true,
        // this code will only perform cropping but not applying it.
        return SetCustomBackgroundCore(imagePath, performCropRequest, !CurrentIsEnableCustomImage, skipPreviousContextCheck, token);
    }

    public Task<bool> SetCurrentCustomBackground(string? imagePath, bool performCropRequest = true, bool skipPreviousContextCheck = true, CancellationToken token = default)
    {
        CurrentCustomBackgroundImagePath = imagePath;
        // For current custom background, since we put the priority at the top.
        // So if it's done performing cropping, apply the change no matter what's the status for global background.
        return SetCustomBackgroundCore(imagePath, performCropRequest, true, skipPreviousContextCheck, token);
    }

    private async Task<bool> SetCustomBackgroundCore(string?           imagePath,
                                                     bool              performCropRequest,
                                                     bool              applyChanges,
                                                     bool              skipPreviousContextCheck,
                                                     CancellationToken token)
    {
        try
        {
            // -- Return as cancelled if path is null
            if (string.IsNullOrEmpty(imagePath))
            {
                return false;
            }

            // -- Try to perform cropped or pass through processing
            (_, string? resultBackgroundPath, bool isCancelProcess) =
                await GetCroppedCustomImage(null,
                                            imagePath,
                                            performCropRequest,
                                            token);

            // -- Do not process custom background if cancelled.
            if (isCancelProcess)
            {
                return false;
            }

            // -- If the change does not require to be applied, then
            //    just ignore it. (Only perform cropping)
            if (!applyChanges)
            {
                return true;
            }

            // -- Apply background
            LayeredImageBackgroundContext context = new()
            {
                OriginBackgroundImagePath = imagePath,
                BackgroundImagePath       = resultBackgroundPath
            };

            UpdateContextListCore(token, skipPreviousContextCheck, context);
            return true;
        }
        catch (Exception ex)
        {
            SentryHelper.ExceptionHandler(ex);
            // Yeet! we won't do any processing for this custom background.

            return false;
        }
    }

#pragma warning disable CA1068
    private void UpdateContextListCore(
        CancellationToken                          token,
        bool                                       skipPreviousContextCheck,
        IEnumerable<LayeredImageBackgroundContext> imageContexts)
    {
        if (imageContexts is List<LayeredImageBackgroundContext> asList)
        {
            UpdateContextListCore(token, skipPreviousContextCheck, CollectionsMarshal.AsSpan(asList));
            return;
        }

        UpdateContextListCore(token, skipPreviousContextCheck, imageContexts.ToArray());
    }

    private void UpdateContextListCore(
        CancellationToken                                  token,
        bool                                               skipPreviousContextCheck,
        params ReadOnlySpan<LayeredImageBackgroundContext> imageContexts)
    {
        // Do not update if the previous contexts are equal
        if (!skipPreviousContextCheck &&
            IsContextEqual(imageContexts))
        {
            return;
        }

        ImageContextSources.Clear(); // Flush list
        ref IList<LayeredImageBackgroundContext>? backedList =
            ref ObservableCollectionExtension<LayeredImageBackgroundContext>
               .GetBackedCollectionList(ImageContextSources);

        // If backed list is List<T>, then use .AddRange
        if (backedList is List<LayeredImageBackgroundContext> backedListAsList)
        {
            backedListAsList.AddRange(imageContexts);

            // Raise collection event.
            ObservableCollectionExtension<LayeredImageBackgroundContext>.RefreshAllEvents(ImageContextSources);
        }
        // Otherwise, add item one-by-one (might cause flicker on the UI Element that bind to it).
        else
        {
            foreach (LayeredImageBackgroundContext imageContext in imageContexts)
            {
                ImageContextSources.Add(imageContext);
            }
        }

        // Update index based on current image context list content.
        OnPropertyChanged(nameof(CurrentSelectedBackgroundIndex));
        OnPropertyChanged(nameof(CurrentSelectedBackgroundContext));
        OnPropertyChanged(nameof(CurrentBackgroundCount));
        LoadImageAtIndex(CurrentSelectedBackgroundIndex, token);
    }
#pragma warning restore CA1068

    private bool IsContextEqual(ReadOnlySpan<LayeredImageBackgroundContext> imageContexts)
    {
        if (ImageContextSources.Count != imageContexts.Length)
        {
            return false;
        }

        HashSet<int> currentContextHashes =
            ImageContextSources
               .Select(x => x.GetHashCode())
               .ToHashSet();
        foreach (LayeredImageBackgroundContext imageContext in imageContexts)
        {
            if (!currentContextHashes.Contains(imageContext.GetHashCode()))
            {
                return false;
            }
        }

        return true;
    }
}

[GeneratedBindableCustomProperty]
public partial class LayeredImageBackgroundContext : NotifyPropertyChanged, IEquatable<LayeredImageBackgroundContext>
{
    public string? OriginOverlayImagePath
    {
        get;
        init;
    }

    public string? OriginBackgroundImagePath
    {
        get;
        init;
    }

    public string? OverlayImagePath
    {
        get;
        init;
    }

    public string? BackgroundImagePath
    {
        get;
        init;
    }

    public bool Equals(LayeredImageBackgroundContext? other) => other?.GetHashCode() == GetHashCode();

    public override bool Equals(object? obj)
    {
        if (ReferenceEquals(obj, this))
        {
            return true;
        }

        if (obj is not LayeredImageBackgroundContext other)
        {
            return false;
        }

        return Equals(other);
    }

    public override int GetHashCode() =>
        HashCode.Combine(OriginOverlayImagePath, OriginBackgroundImagePath, OverlayImagePath, BackgroundImagePath);
}