using CollapseLauncher.Helper.Background;
using CollapseLauncher.Helper.Image;
using CollapseLauncher.Helper.Metadata;
using Hi3Helper;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using static CollapseLauncher.InnerLauncherConfig;
using static CollapseLauncher.Statics.GamePropertyVault;
using static Hi3Helper.Logger;
using static Hi3Helper.Shared.Region.LauncherConfig;

// ReSharper disable CheckNamespace
// ReSharper disable RedundantExtendsListEntry
// ReSharper disable InconsistentNaming
// ReSharper disable UnusedMember.Local
// ReSharper disable SwitchStatementHandlesSomeKnownEnumValuesWithDefault
// ReSharper disable IdentifierTypo
// ReSharper disable AsyncVoidMethod
// ReSharper disable StringLiteralTypo
// ReSharper disable CommentTypo
// ReSharper disable SwitchStatementMissingSomeEnumCasesNoDefault

namespace CollapseLauncher;

public partial class MainPage : Page
{
    private static void BackgroundImg_IsImageHideEvent(object sender, bool e)
    {
        if (e) CurrentBackgroundHandler?.Dimm();
        else CurrentBackgroundHandler?.Undimm();
    }

    private readonly HashSet<string> _processingBackground = [];
    private async void CustomBackgroundChanger_Event(object sender, BackgroundImgProperty e)
    {
        if (_processingBackground.Contains(e.ImgPath))
        {
            LogWriteLine($"Background {e.ImgPath} is already being processed!", LogType.Warning, true);
            return;
        }

        try
        {
            _processingBackground.Add(e.ImgPath);
            var gameLauncherApi = LauncherMetadataHelper.CurrentMetadataConfig?.GameLauncherApi;
            if (gameLauncherApi == null)
            {
                return;
            }

            gameLauncherApi.GameBackgroundImgLocal = e.ImgPath;
            IsCustomBG                             = e.IsCustom;

            if (!File.Exists(gameLauncherApi.GameBackgroundImgLocal))
            {
                if (IsCustomBG)
                {
                    var customBGPath = e.ImgPath;
                    if (string.IsNullOrWhiteSpace(customBGPath))
                    {
                        // Check if using regional custom BG
                        if (GetCurrentGameProperty().GameSettings?.SettingsCollapseMisc.UseCustomRegionBG ?? false)
                        {
                            customBGPath = GetCurrentGameProperty().GameSettings?.SettingsCollapseMisc?.CustomRegionBGPath;
                        }
                        // check if using global custom BG
                        else
                        {
                            customBGPath = GetAppConfigValue("CustomBGPath").ToString();
                        }
                    }
                    
                    var missingImageEx =
                        new FileNotFoundException(Locale.Lang._UnhandledExceptionPage.CustomBackground_NotFound,
                                                  customBGPath);
                    DispatcherQueue.TryEnqueue(() =>
                                               {
                                                   try
                                                   {
                                                       ErrorSender.SendWarning(missingImageEx);
                                                   }
                                                   catch
                                                   {
                                                       // ignored
                                                   }
                                               });
                }
                
                LogWriteLine($"Custom background file {e.ImgPath} is missing!", LogType.Warning, true);
                gameLauncherApi.GameBackgroundImgLocal = BackgroundMediaUtility.GetDefaultRegionBackgroundPath();
            }

            var mType = BackgroundMediaUtility.GetMediaType(gameLauncherApi.GameBackgroundImgLocal);
            switch (mType)
            {
                case BackgroundMediaUtility.MediaType.Media:
                    BackgroundNewMediaPlayerGrid.Visibility = Visibility.Visible;
                    BackgroundNewBackGrid.Visibility        = Visibility.Collapsed;
                    break;
                case BackgroundMediaUtility.MediaType.StillImage:
                    FileStream imgStream =
                        await ImageLoaderHelper.LoadImage(gameLauncherApi.GameBackgroundImgLocal);
                    BackgroundMediaUtility.SetAlternativeFileStream(imgStream);
                    BackgroundNewMediaPlayerGrid.Visibility = Visibility.Collapsed;
                    BackgroundNewBackGrid.Visibility        = Visibility.Visible;
                    break;
                case BackgroundMediaUtility.MediaType.Unknown:
                default:
                    throw new InvalidCastException();
            }

            await InitBackgroundHandler();
            CurrentBackgroundHandler?.LoadBackground(gameLauncherApi.GameBackgroundImgLocal, e.IsRequestInit,
                                                     e.IsForceRecreateCache, ex =>
                                                                             {
                                                                                 gameLauncherApi.GameBackgroundImgLocal =
                                                                                     BackgroundMediaUtility.GetDefaultRegionBackgroundPath();
                                                                                 LogWriteLine($"An error occured while loading background {e.ImgPath}\r\n{ex}",
                                                                                     LogType.Error, true);
                                                                                 ErrorSender.SendException(ex);
                                                                             }, e.ActionAfterLoaded);
        }
        catch (Exception ex)
        {
            LogWriteLine($"An error occured while loading background {e.ImgPath}\r\n{ex}",
                         LogType.Error, true);
            ErrorSender.SendException(new Exception($"An error occured while loading background {e.ImgPath}", ex));
        }
        finally
        {
            _processingBackground.Remove(e.ImgPath);
        }
    }

    internal async Task ChangeBackgroundImageAsRegionAsync(bool ShowLoadingMsg = false)
    {
        var gameLauncherApi = LauncherMetadataHelper.CurrentMetadataConfig?.GameLauncherApi;
        if (gameLauncherApi == null)
        {
            return;
        }

        GamePresetProperty currentGameProperty = GetCurrentGameProperty();
        bool isUseCustomPerRegionBg = currentGameProperty.GameSettings?.SettingsCollapseMisc?.UseCustomRegionBG ?? false;

        IsCustomBG = GetAppConfigValue("UseCustomBG").ToBool();
        bool isAPIBackgroundAvailable =
            !string.IsNullOrEmpty(gameLauncherApi.GameBackgroundImg);

        var posterBg = currentGameProperty.GameVersion.GameType switch
                       {
                           GameNameType.Honkai => Path.Combine(AppExecutableDir,
                                                               @"Assets\Images\GameBackground\honkai.webp"),
                           GameNameType.Genshin => Path.Combine(AppExecutableDir,
                                                                @"Assets\Images\GameBackground\genshin.webp"),
                           GameNameType.StarRail => Path.Combine(AppExecutableDir,
                                                                 @"Assets\Images\GameBackground\starrail.webp"),
                           GameNameType.Zenless => Path.Combine(AppExecutableDir,
                                                                @"Assets\Images\GameBackground\zzz.webp"),
                           _ => BackgroundMediaUtility.GetDefaultRegionBackgroundPath()
                       };

        // Check if Regional Custom BG is enabled and available
        if (isUseCustomPerRegionBg)
        {
            var regionBgPath = currentGameProperty.GameSettings?.SettingsCollapseMisc?.CustomRegionBGPath;
            if (!string.IsNullOrEmpty(regionBgPath) && File.Exists(regionBgPath))
            {
                if (BackgroundMediaUtility.GetMediaType(regionBgPath) == BackgroundMediaUtility.MediaType.StillImage)
                {
                    FileStream imgStream = await ImageLoaderHelper.LoadImage(regionBgPath);
                    BackgroundMediaUtility.SetAlternativeFileStream(imgStream);
                }
                    
                gameLauncherApi.GameBackgroundImgLocal = regionBgPath;
            }
        }
        // If not, then check for global Custom BG
        else
        {
            var BGPath = IsCustomBG ? GetAppConfigValue("CustomBGPath").ToString() : null;
            if (!string.IsNullOrEmpty(BGPath))
            {
                gameLauncherApi.GameBackgroundImgLocal = BGPath;
            }
            // If it's still not, then check if API gives any background
            else if (isAPIBackgroundAvailable)
            {
                try
                {
                    await DownloadBackgroundImage(CancellationToken.None);
                    return; // Return after successfully loading
                }
                catch (Exception ex)
                {
                    ErrorSender.SendException(ex);
                    LogWriteLine($"Failed while downloading default background image!\r\n{ex}", LogType.Error, true);
                    gameLauncherApi.GameBackgroundImgLocal = BackgroundMediaUtility.GetDefaultRegionBackgroundPath();
                }
            }
            // IF ITS STILL NOT THERE, then use fallback game poster, IF ITS STILL NOT THEREEEE!! use paimon cute deadge pic :)
            else
            {
                gameLauncherApi.GameBackgroundImgLocal = posterBg;
            }
        }
            
        // Use default background if the API background is empty (in-case HoYo did something catchy)
        if (!isAPIBackgroundAvailable && !IsCustomBG && LauncherMetadataHelper.CurrentMetadataConfig is { GameLauncherApi: not null })
            gameLauncherApi.GameBackgroundImgLocal ??= posterBg;
            
        // If the custom per region is enabled, then execute below
        BackgroundImgChanger.ChangeBackground(gameLauncherApi.GameBackgroundImgLocal,
                                              () =>
                                              {
                                                  IsFirstStartup = false;
                                                  ColorPaletteUtility.ReloadPageTheme(this, CurrentAppTheme);
                                              },
                                              IsCustomBG || isUseCustomPerRegionBg, true, true);
    }
}
