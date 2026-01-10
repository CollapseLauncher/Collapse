using CollapseLauncher.Extension;
using CollapseLauncher.Helper.Image;
using CollapseLauncher.Helper.LauncherApiLoader.HoYoPlay;
using CollapseLauncher.Helper.Metadata;
using CollapseLauncher.Interfaces.Class;
using Hi3Helper.SentryHelper;
using Hi3Helper.Shared.Region;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Data;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using WinRT;
// ReSharper disable CheckNamespace
#pragma warning disable IDE0130

#nullable enable
namespace CollapseLauncher.GameManagement.ImageBackground;

[GeneratedBindableCustomProperty]
public partial class ImageBackgroundManager
    : NotifyPropertyChanged
{
    internal static readonly ImageBackgroundManager Shared = new();

    #region Shared/Static Properties and Fields

    private const string GlobalCustomBackgroundConfigKey                  = "globalBg";
    private const string GlobalIsEnableCustomImageConfigKey               = "globalIsCustomImageEnabled";
    private const string GlobalIsBackgroundParallaxEffectEnabledConfigKey = "globalIsBackgroundParallaxEffectEnabled";
    private const string GlobalBackgroundParallaxPixelShiftConfigKey      = "globalBackgroundParallaxPixelShift";
    private const string GlobalBackgroundAudioVolumeConfigKey             = "globalBackgroundAudioVolume";
    private const string GlobalBackgroundAudioEnabledConfigKey            = "globalBackgroundAudioEnabled";

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
            _ = Initialize(PresetConfig,
                       CurrentBackgroundApi,
                       PresenterGrid,
                       false,
                       CancellationToken.None);
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
            _ = Initialize(PresetConfig,
                           CurrentBackgroundApi,
                           PresenterGrid,
                           false,
                           CancellationToken.None);
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
    /// Initialize background images of the current region. This method MUST be called everytime the region is loaded.
    /// </summary>
    /// <param name="presetConfig">The preset config of the current game region.</param>
    /// <param name="backgroundApi">The background API implementation of the current game region.</param>
    /// <param name="presenterGrid">Presenter Grid which the element of the background will be shown on.</param>
    /// <param name="performCropRequest">Whether to request the launcher to spawn crop dialog if (say) a new custom image is used.</param>
    /// <param name="token">Cancellation token to cancel asynchronous operations.</param>
    public async Task Initialize(PresetConfig?             presetConfig,
                                 HypLauncherBackgroundApi? backgroundApi,
                                 Grid?                     presenterGrid,
                                 bool                      performCropRequest = false,
                                 CancellationToken         token              = default)
    {
        ArgumentNullException.ThrowIfNull(presetConfig);
        ArgumentNullException.ThrowIfNull(presenterGrid);

        CurrentCustomBackgroundConfigKey = $"lastCustomBg-{presetConfig.GameName}-{presetConfig.ZoneName}";
        CurrentSelectedBackgroundIndexKey = $"lastCustomBgIndex-{presetConfig.GameName}-{presetConfig.ZoneName}";
        CurrentIsEnableCustomImageConfigKey = $"lastIsCustomImageEnabled-{presetConfig.GameName}-{presetConfig.ZoneName}";

        PresetConfig         = presetConfig;
        PresenterGrid        = presenterGrid;
        CurrentBackgroundApi = backgroundApi;
        bool isCancelProcess = false;

        List<LayeredImageBackgroundContext> imageContexts = [];

        try
        {
            string? resultOverlayPath    = null;
            string? resultBackgroundPath = null;

            if (CurrentIsEnableCustomImage)
            {
                (resultOverlayPath, resultBackgroundPath, isCancelProcess) =
                    await GetCroppedCustomImage(null,
                                                CurrentCustomBackgroundImagePath,
                                                performCropRequest,
                                                token);

                if (isCancelProcess)
                {
                    return;
                }
            }

            if (GlobalIsEnableCustomImage &&
                string.IsNullOrEmpty(resultOverlayPath) &&
                string.IsNullOrEmpty(resultBackgroundPath))
            {
                (resultOverlayPath, resultBackgroundPath, isCancelProcess) =
                    await GetCroppedCustomImage(null,
                                                GlobalCustomBackgroundImagePath,
                                                performCropRequest,
                                                token);

                if (isCancelProcess)
                {
                    return;
                }
            }

            // If Current or Global custom image is enabled and
            // image paths (either overlay or background) is existed,
            // then use that custom path.
            if ((CurrentIsEnableCustomImage || GlobalIsEnableCustomImage) &&
                !string.IsNullOrEmpty(resultBackgroundPath))
            {
                imageContexts.Add(new LayeredImageBackgroundContext
                {
                    OriginBackgroundImagePath = CurrentCustomBackgroundImagePath ?? GlobalCustomBackgroundImagePath,
                    OverlayImagePath          = resultOverlayPath,
                    BackgroundImagePath       = resultBackgroundPath
                });
                return;
            }

            // Otherwise, add from launcher API's

            // -- Check 1: Add placeholder ones if the API is not implemented.
            if (CurrentBackgroundApi?.Data is not { GameContentList: { Count: > 0 } contextList })
            {
                string bgPlaceholderPath = GetPlaceholderBackgroundImageFrom(presetConfig);
                imageContexts.Add(new LayeredImageBackgroundContext
                {
                    OriginBackgroundImagePath = bgPlaceholderPath,
                    BackgroundImagePath       = bgPlaceholderPath
                });
                return;
            }

            // -- Check 2: Use ones provided by the API
            foreach (HypLauncherBackgroundContentKindData contextEntry in contextList.SelectMany(x => x.Backgrounds))
            {
                string? overlayImagePath = contextEntry.BackgroundOverlay?.ImageUrl;
                string? backgroundImagePath = contextEntry.BackgroundVideo?.ImageUrl ??
                                              contextEntry.BackgroundImage?.ImageUrl;

                imageContexts.Add(new LayeredImageBackgroundContext
                {
                    OverlayImagePath    = overlayImagePath,
                    BackgroundImagePath = backgroundImagePath
                });
            }
        }
        catch (Exception ex)
        {
            SentryHelper.ExceptionHandler(ex);
            // ignore
        }
        finally
        {
            if (!isCancelProcess)
            {

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
                LoadImageAtIndex(CurrentSelectedBackgroundIndex, token);
            }
        }
    }
}

[GeneratedBindableCustomProperty]
public partial class LayeredImageBackgroundContext
    : NotifyPropertyChanged
{
    public string? OriginOverlayImagePath
    {
        get;
        set
        {
            field = value;
            OnPropertyChanged();
        }
    }

    public string? OriginBackgroundImagePath
    {
        get;
        set
        {
            field = value;
            OnPropertyChanged();
        }
    }

    public string? OverlayImagePath
    {
        get;
        set
        {
            field = value;
            OnPropertyChanged();
        }
    }

    public string? BackgroundImagePath
    {
        get;
        set
        {
            field = value;
            OnPropertyChanged();
        }
    }
}