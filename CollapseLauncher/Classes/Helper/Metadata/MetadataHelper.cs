using CollapseLauncher.Helper.Loading;
using Hi3Helper.EncTool;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
#pragma warning disable IDE0130

#nullable enable
namespace CollapseLauncher.Helper.Metadata;

internal static partial class MetadataHelper
{
    public static async Task InitializeAsync(bool showLoadingMessage = true)
    {
        try
        {
            Directory.CreateDirectory(LauncherMetadataDirectory);

            if (showLoadingMessage)
            {
                LoadingMessageHelper.ShowLoadingFrame();
                LoadingMessageHelper.SetMessage(Locale.Current.Lang?._MainPage?.Initializing, Locale.Current.Lang?._MainPage?.LoadingLauncherMetadata);
            }

            await InitializeStampAsync();
            await InitializeAllConfigsAsync(showLoadingMessage);
        }
        catch (Exception ex)
        {
            string message = $"Collapse Launcher cannot load its metadata config files due to its invalid states. Please remove these folders: \"{LauncherMetadataDirectory}\" and \"{CDNCacheUtil.CurrentCacheDir}\", then try restarting your launcher.";
            Exception parentExc = new InvalidOperationException(message, ex);
            ErrorSender.SendException(parentExc);
        }
    }

    private static partial class Util
    {
        public static string? GetStringTranslation(Dictionary<string, string>? dict, string? key)
        {
            if (string.IsNullOrEmpty(key) ||
                !(dict?.TryGetValue(key, out string? value) ?? false))
            {
                return key;
            }

            return value;
        }

        [return: NotNullIfNotNull(nameof(gameTitle))]
        public static unsafe string? GetNonSpaceGameTitle(string? gameTitle)
        {
            if (string.IsNullOrEmpty(gameTitle))
            {
                return gameTitle;
            }

            int        j      = 0;
            Span<char> buffer = stackalloc char[gameTitle.Length];

            // UNSAFELY iterate as writeable reference.
            ref char start = ref Unsafe.AsRef<char>(Unsafe.AsPointer(in gameTitle.AsSpan().GetPinnableReference()));
            ref char end   = ref Unsafe.Add(ref Unsafe.AsRef<char>(Unsafe.AsPointer(in start)), gameTitle.Length);

            while (Unsafe.IsAddressLessThan(in start, in end))
            {
                if (char.IsAsciiLetter(start) ||
                    char.IsAsciiDigit(start))
                {
                    buffer[j++] = start;
                }

                start = ref Unsafe.Add(ref start, 1);
            }

            return new string(buffer[..j]);
        }
    }
}
