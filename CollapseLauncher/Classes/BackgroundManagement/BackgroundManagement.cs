using CollapseLauncher.Helper.Image;
﻿using CollapseLauncher.Helper.Animation;
using ColorThiefDotNet;
using CommunityToolkit.WinUI.Animations;
using Hi3Helper;
using Hi3Helper.Data;
using Hi3Helper.Preset;
using Microsoft.UI.Composition;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using Microsoft.UI.Xaml.Media.Imaging;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Numerics;
using System.Threading.Tasks;
using static CollapseLauncher.InnerLauncherConfig;
using static CollapseLauncher.RegionResourceListHelper;
using static Hi3Helper.Logger;
using static Hi3Helper.Shared.Region.LauncherConfig;

namespace CollapseLauncher
{
    [System.Diagnostics.CodeAnalysis.SuppressMessageAttribute("ReSharper", "PossibleNullReferenceException")]
    [System.Diagnostics.CodeAnalysis.SuppressMessageAttribute("ReSharper", "AssignNullToNotNullAttribute")]
    public sealed partial class MainPage
    {
        private RegionResourceProp _gameAPIProp { get; set; }

        private BitmapImage BackgroundBitmap;
        private Bitmap PaletteBitmap;
        private bool BGLastState = true;
        private bool IsFirstStartup = true;

        internal async void ChangeBackgroundImageAsRegionAsync(bool ShowLoadingMsg = false)
        {
            IsCustomBG = GetAppConfigValue("UseCustomBG").ToBool();
            if (IsCustomBG)
            {
                string BGPath = GetAppConfigValue("CustomBGPath").ToString();
                regionBackgroundProp.imgLocalPath = string.IsNullOrEmpty(BGPath) ? AppDefaultBG : BGPath;
            }
            else
            {
                if (!await TryLoadResourceInfo(ResourceLoadingType.DownloadBackground, ConfigV2Store.CurrentConfigV2, ShowLoadingMsg))
                {
                    regionBackgroundProp.imgLocalPath = AppDefaultBG;
                }
            }

            if (!IsCustomBG || IsFirstStartup)
            {
                BackgroundImgChanger.ChangeBackground(regionBackgroundProp.imgLocalPath, IsCustomBG);
                await BackgroundImgChanger.WaitForBackgroundToLoad();
            }

            IsFirstStartup = false;

            ReloadPageTheme(this, ConvertAppThemeToElementTheme(CurrentAppTheme));
        }

        public static async void ApplyAccentColor(Page page, Bitmap bitmapInput, string bitmapPath)
        {
            bool IsLight = IsAppThemeLight;

            Windows.UI.Color[] _colors = await TryGetCachedPalette(bitmapInput, IsLight, bitmapPath);

            SetColorPalette(page, _colors);
        }

        private static async ValueTask<Windows.UI.Color[]> TryGetCachedPalette(Bitmap bitmapInput, bool isLight, string bitmapPath)
        {
            string cachedPalettePath = bitmapPath + $".palette{(isLight ? "Light" : "Dark")}";
            string cachedFileHash = ConverterTool.BytesToCRC32Simple(cachedPalettePath);
            cachedPalettePath = Path.Combine(AppGameImgCachedFolder, cachedFileHash);
            if (File.Exists(cachedPalettePath))
            {
                byte[] data = await File.ReadAllBytesAsync(cachedPalettePath);
                if (!ConverterTool.TryDeserializeStruct(data, 4, out Windows.UI.Color[] output))
                {
                    return await TryGenerateNewCachedPalette(bitmapInput, isLight, cachedPalettePath);
                }

                return output;
            }

            return await TryGenerateNewCachedPalette(bitmapInput, isLight, cachedPalettePath);
        }

        private static async ValueTask<Windows.UI.Color[]> TryGenerateNewCachedPalette(Bitmap bitmapInput, bool IsLight, string cachedPalettePath)
        {
            byte[] buffer = new byte[1 << 10];

            string cachedPaletteDirPath = Path.GetDirectoryName(cachedPalettePath);
            if (!Directory.Exists(cachedPaletteDirPath)) Directory.CreateDirectory(cachedPaletteDirPath);

            Windows.UI.Color[] _colors = await GetPaletteList(bitmapInput, 10, IsLight, 1);

            if (!ConverterTool.TrySerializeStruct(_colors, buffer, out int read))
            {
                byte DefVal = (byte)(IsLight ? 80 : 255);
                Windows.UI.Color defColor = DrawingColorToColor(new QuantizedColor(Color.FromArgb(255, DefVal, DefVal, DefVal), 1));
                return new Windows.UI.Color[] { defColor, defColor, defColor, defColor };
            }

            await File.WriteAllBytesAsync(cachedPalettePath, buffer[..read]);
            return _colors;
        }

        public static void SetColorPalette(Page page, Windows.UI.Color[] palette = null)
        {
            if (palette == null || palette.Length < 2)
                palette = EnsureLengthCopyLast(new Windows.UI.Color[] { (Windows.UI.Color)Application.Current.Resources["TemplateAccentColor"] }, 2);

            if (IsAppThemeLight)
            {
                Application.Current.Resources["SystemAccentColor"] = palette[0];
                Application.Current.Resources["SystemAccentColorDark1"] = palette[0];
                Application.Current.Resources["SystemAccentColorDark2"] = palette[1];
                Application.Current.Resources["SystemAccentColorDark3"] = palette[1];
                Application.Current.Resources["AccentColor"] = new SolidColorBrush(palette[1]);
            }
            else
            {
                Application.Current.Resources["SystemAccentColor"] = palette[0];
                Application.Current.Resources["SystemAccentColorLight1"] = palette[0];
                Application.Current.Resources["SystemAccentColorLight2"] = palette[1];
                Application.Current.Resources["SystemAccentColorLight3"] = palette[0];
                Application.Current.Resources["AccentColor"] = new SolidColorBrush(palette[0]);
            }

            ReloadPageTheme(page, ConvertAppThemeToElementTheme(CurrentAppTheme));
        }

        private static List<QuantizedColor> _generatedColors = new List<QuantizedColor>();
        private static async Task<Windows.UI.Color[]> GetPaletteList(Bitmap bitmapinput, int ColorCount, bool IsLight, int quality)
        {
            byte DefVal = (byte)(IsLight ? 80 : 255);

            try
            {
                LumaUtils.DarkThreshold = IsLight ? 200f : 400f;
                LumaUtils.IgnoreWhiteThreshold = IsLight ? 900f : 800f;
                if (!IsLight)
                    LumaUtils.ChangeCoeToBT709();
                else
                    LumaUtils.ChangeCoeToBT601();

                return await Task.Run(() =>
                {
                    _generatedColors.Clear();

                    while (true)
                    {
                        try
                        {
                            IEnumerable<QuantizedColor> averageColors = ColorThief.GetPalette(bitmapinput, ColorCount, quality, !IsLight)
                                .Where(x => IsLight ? x.IsDark : !x.IsDark)
                                .OrderBy(x => x.Population);

                            IEnumerable<QuantizedColor> quantizedColors = averageColors.ToArray();
                            QuantizedColor dominatedColor = new QuantizedColor(
                                  Color.FromArgb(
                                      255,
                                      (byte)quantizedColors.Average(a => a.Color.R),
                                      (byte)quantizedColors.Average(a => a.Color.G),
                                      (byte)quantizedColors.Average(a => a.Color.B)
                                     ), (int)quantizedColors.Average(a => a.Population));

                            _generatedColors.Add(dominatedColor);
                            _generatedColors.AddRange(quantizedColors);

                            break;
                        }
                        catch (InvalidOperationException)
                        {
                            if (ColorCount > 100) throw;
                            LogWriteLine($"Regenerating colors by adding 20 more colors to generate: {ColorCount} to {ColorCount + 20}", LogType.Warning, true);
                            ColorCount += 20;
                        }
                    }

                    return EnsureLengthCopyLast(_generatedColors
                        .Select(DrawingColorToColor)
                        .ToArray(), 4);
                }).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                LogWriteLine($"{ex}", LogType.Warning, true);
            }

            Windows.UI.Color defColor = DrawingColorToColor(new QuantizedColor(Color.FromArgb(255, DefVal, DefVal, DefVal), 1));
            return new Windows.UI.Color[] { defColor, defColor, defColor, defColor };
        }

        private static T[] EnsureLengthCopyLast<T>(T[] array, int toLength)
        {
            if (array.Length == 0) throw new IndexOutOfRangeException("Array has no content in it");
            if (array.Length >= toLength) return array;

            T lastArray = array[array.Length - 1];
            T[] newArray = new T[toLength];
            Array.Copy(array, newArray, array.Length);

            for (int i = array.Length; i < newArray.Length; i++)
            {
                newArray[i] = lastArray;
            }

            return newArray;
        }

        private static Windows.UI.Color DrawingColorToColor(QuantizedColor i) => new Windows.UI.Color { R = i.Color.R, G = i.Color.G, B = i.Color.B, A = i.Color.A };

        private async Task ApplyBackground(bool _isFirstStartup)
        {
            BitmapImage ReplacementBitmap;
            (PaletteBitmap, ReplacementBitmap) = await ImageLoaderHelper.GetResizedBitmapNew(regionBackgroundProp.imgLocalPath);
            if (PaletteBitmap == null || ReplacementBitmap == null) return;

            ApplyAccentColor(this, PaletteBitmap, regionBackgroundProp.imgLocalPath);

            if (!_isFirstStartup)
                FadeSwitchAllBg(0.125f, ReplacementBitmap);
            else
                FadeInAllBg(0.125f, ReplacementBitmap);
        }

        private async void FadeInAllBg(double duration, BitmapImage ReplacementImage)
        {
            Storyboard storyBefore = new Storyboard();
            AddDoubleAnimationFadeToObject(BackgroundFrontBuffer, "Opacity", 0.125, BackgroundFrontBuffer.Opacity, 0f, ref storyBefore);
            AddDoubleAnimationFadeToObject(BackgroundBackBuffer, "Opacity", 0.125, BackgroundBackBuffer.Opacity, 0f, ref storyBefore);
            AddDoubleAnimationFadeToObject(BackgroundFront, "Opacity", 0.125, BackgroundFront.Opacity, 0f, ref storyBefore);
            AddDoubleAnimationFadeToObject(BackgroundBack, "Opacity", 0.125, BackgroundBack.Opacity, 0f, ref storyBefore);
            storyBefore.Begin();
            await Task.Delay(250);

            BackgroundBack.Source = ReplacementImage;
            BackgroundFront.Source = ReplacementImage;

            Storyboard storyAfter = new Storyboard();
            if (m_appMode != AppMode.Hi3CacheUpdater)
                AddDoubleAnimationFadeToObject(BackgroundFront, "Opacity", duration, 0f, 1f, ref storyAfter);
            AddDoubleAnimationFadeToObject(BackgroundBack, "Opacity", duration, 0f, 1f, ref storyAfter);
            storyAfter.Begin();
            await Task.Delay((int)(duration * 1000));

            BackgroundBitmap = ReplacementImage;
        }

        private async void FadeSwitchAllBg(double duration, BitmapImage ReplacementImage)
        {
            Storyboard storyBuf = new Storyboard();

            BackgroundBackBuffer.Source = BackgroundBitmap;
            BackgroundBackBuffer.Opacity = 1f;

            BackgroundFrontBuffer.Source = BackgroundBitmap;
            if (m_appCurrentFrameName == "HomePage")
            {
                BackgroundFrontBuffer.Opacity = 1f;
            }

            BackgroundBack.Opacity = 1f;

            if (m_appCurrentFrameName == "HomePage")
            {
                BackgroundFront.Opacity = 1f;
                AddDoubleAnimationFadeToObject(BackgroundFrontBuffer, "Opacity", duration, 1, 0, ref storyBuf);
            }

            AddDoubleAnimationFadeToObject(BackgroundBackBuffer, "Opacity", duration, 1, 0, ref storyBuf);
            BackgroundBack.Source = ReplacementImage;
            BackgroundFront.Source = ReplacementImage;

            storyBuf.Begin();
            await Task.Delay((int)duration * 1000);

            BackgroundBitmap = ReplacementImage;
        }

        private void AddDoubleAnimationFadeToObject<T>(T objectToAnimate, string targetProperty,
            double duration, double valueFrom, double valueTo, ref Storyboard storyboard)
            where T : DependencyObject
        {
            DoubleAnimation Animation = new DoubleAnimation();
            Animation.Duration = new Duration(TimeSpan.FromSeconds(duration));
            Animation.From = valueFrom; Animation.To = valueTo;

            Storyboard.SetTarget(Animation, objectToAnimate);
            Storyboard.SetTargetProperty(Animation, targetProperty);
            storyboard.Children.Add(Animation);
        }

        private async void HideBackgroundImage(bool hideImage = true)
        {
            while (IsCurrentHideBGAnimRun) { await Task.Delay(100); }

            Compositor currentCompositor = this.GetElementCompositor();

            if (hideImage != BGLastState)
            {
                TimeSpan duration = TimeSpan.FromSeconds(hideImage ? 0.25d : 0.5d);
                TimeSpan durationSlow = TimeSpan.FromSeconds(0.25d);

                float fromScale = !hideImage ? 0.95f : 1f;
                Vector3 fromTranslate = new Vector3(-((float)BackgroundFront.ActualWidth * (fromScale - 1f) / 2), -((float)BackgroundFront.ActualHeight * (fromScale - 1f) / 2), 0);
                float toScale = hideImage ? 1.1f : 1f;
                Vector3 toTranslate = new Vector3(-((float)BackgroundFront.ActualWidth * (toScale - 1f) / 2), -((float)BackgroundFront.ActualHeight * (toScale - 1f) / 2), 0);

                IsCurrentHideBGAnimRun = true;
                CurrentHideBGAnimQueue.Add(Task.WhenAll(
                    BackgroundBack.StartAnimation(
                        durationSlow,
                        currentCompositor.CreateScalarKeyFrameAnimation("Opacity", hideImage ? 0.4f : 1f, hideImage ? 1f : 0.4f)
                    ),
                    IsFirstStartup ? Task.CompletedTask :
                    BackgroundFront.StartAnimation(
                        duration,
                        currentCompositor.CreateScalarKeyFrameAnimation("Opacity", hideImage ? 0f : 1f, hideImage ? 1f : 0f),
                        currentCompositor.CreateVector3KeyFrameAnimation("Scale", new Vector3(toScale), new Vector3(fromScale)),
                        currentCompositor.CreateVector3KeyFrameAnimation("Translation", toTranslate, fromTranslate)
                    )
                ));

                BGLastState = hideImage;
                IsCurrentHideBGAnimRun = false;
            }
        }

        private static async void RunHideBackgroundAnimQueue()
        {
            while (!App.IsAppKilled)
            {
                while (CurrentHideBGAnimQueue.Count > 0)
                {
                    await CurrentHideBGAnimQueue[0];
                    CurrentHideBGAnimQueue.RemoveAt(0);
                }
                await Task.Delay(250);
            }
        }
    }
}
