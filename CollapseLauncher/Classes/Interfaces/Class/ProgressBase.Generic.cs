using CollapseLauncher.CustomControls;
using CollapseLauncher.Dialogs;
using CollapseLauncher.Extension;
using Hi3Helper;
using Hi3Helper.Data;
using Hi3Helper.Preset;
using Hi3Helper.Shared.Region;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CollapseUIExtension = CollapseLauncher.Extension.UIElementExtensions;
#pragma warning disable IDE0130

#nullable enable
namespace CollapseLauncher.Interfaces;

internal class ProgressBase<T1> : ProgressBase where T1 : IAssetIndexSummary
{
    protected ProgressBase(
        UIElement      parentUI,
        IGameVersion?  gameVersionManager,
        IGameSettings? gameSettings,
        string?        gamePath,
        string?        gameRepoURL,
        string?        versionOverride)
        : base(parentUI,
               gameVersionManager,
               gameSettings,
               gamePath,
               gameRepoURL,
               versionOverride)
    {
    }

    protected ProgressBase(UIElement parentUI, IGameVersion? gameVersionManager, string? gamePath, string? gameRepoURL, string? versionOverride)
        : base(parentUI, gameVersionManager, null, gamePath, gameRepoURL, versionOverride) { }

    internal List<T1> AssetIndex { get; set; } = [];

    #region BaseTools
    protected override async Task TryRunExamineThrow(Task task)
    {
        try
        {
            await base.TryRunExamineThrow(task);
        }
        finally
        {
            // Clear the _assetIndex after that
            if (Status is { IsCompleted: false })
            {
                AssetIndex.Clear();
            }
        }
    }

    protected IEnumerable<(T1 AssetIndex, T2 AssetProperty)> PairEnumeratePropertyAndAssetIndexPackage<T2>
        (IEnumerable<T1> assetIndex, IEnumerable<T2> assetProperty)
        where T2 : IAssetProperty
    {
        using IEnumerator<T1> assetIndexEnumerator = assetIndex.GetEnumerator();
        using IEnumerator<T2> assetPropertyEnumerator = assetProperty.GetEnumerator();

        while (assetIndexEnumerator.MoveNext()
               && assetPropertyEnumerator.MoveNext())
        {
            yield return (assetIndexEnumerator.Current, assetPropertyEnumerator.Current);
        }
    }

    protected static IEnumerable<T1> EnforceHttpSchemeToAssetIndex(IEnumerable<T1> assetIndex)
    {
        const string httpsScheme = "https://";
        const string httpScheme = "http://";
        // Get the check if HTTP override is enabled
        bool isUseHttpOverride = LauncherConfig.GetAppConfigValue("EnableHTTPRepairOverride").ToBool();

        // Iterate the IAssetIndexSummary asset
        foreach (T1 asset in assetIndex)
        {
            // If the HTTP override is enabled, then start override the HTTPS scheme
            if (isUseHttpOverride)
            {
                // Get the remote url as span
                ReadOnlySpan<char> url = asset.GetRemoteURL().AsSpan();
                // If the url starts with HTTPS scheme, then...
                if (url.StartsWith(httpsScheme))
                {
                    // Get the trimmed URL without HTTPS scheme as span
                    ReadOnlySpan<char> trimmedURL = url[httpsScheme.Length..];
                    // Set the trimmed URL
                    asset.SetRemoteURL(string.Concat(httpScheme, trimmedURL));
                }

                // Yield it and continue to the next entry
                yield return asset;
                continue;
            }

            // If override not enabled, then just return the asset as is
            yield return asset;
        }
    }

    protected override void ResetStatusAndProgress()
    {
        AssetIndex.Clear();
        base.ResetStatusAndProgress();
    }
    #endregion

    #region DialogTools
    protected async Task SpawnRepairDialog(List<T1> assetIndex, Action? actionIfInteractiveCancel)
    {
        ArgumentNullException.ThrowIfNull(assetIndex);
        long totalSize = assetIndex.Sum(x => x.GetAssetSize());
        StackPanel content = CollapseUIExtension.CreateStackPanel();

        content.AddElementToStackPanel(new TextBlock
        {
            Text = string.Format(Locale.Lang._InstallMgmt.RepairFilesRequiredSubtitle, assetIndex.Count, ConverterTool.SummarizeSizeSimple(totalSize)),
            Margin = new Thickness(0, 0, 0, 16),
            TextWrapping = TextWrapping.Wrap
        });
        Button showBrokenFilesButton = content.AddElementToStackPanel(
            CollapseUIExtension.CreateButtonWithIcon<Button>(
                Locale.Lang._InstallMgmt.RepairFilesRequiredShowFilesBtn,
                "\uf550",
                "FontAwesomeSolid",
                "AccentButtonStyle"
            )
            .WithHorizontalAlignment(HorizontalAlignment.Center));

        showBrokenFilesButton.Click += async (_, _) =>
        {
            string tempPath = Path.GetTempFileName() + ".log";

            await using (FileStream fs = new(tempPath, FileMode.Create, FileAccess.Write))
            {
                await using StreamWriter sw = new(fs);
                await sw.WriteLineAsync($"Original Path: {GamePath}");
                await sw.WriteLineAsync($"Total size to download: {ConverterTool.SummarizeSizeSimple(totalSize)} ({totalSize} bytes)");
                await sw.WriteLineAsync();

                foreach (T1 fileList in assetIndex)
                {
                    await sw.WriteLineAsync(fileList.PrintSummary());
                }
            }

            Process proc = new()
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = tempPath,
                    UseShellExecute = true
                }
            };
            proc.Start();
            await proc.WaitForExitAsync();

            try
            {
                File.Delete(tempPath);
            }
            catch
            {
                // piped to parent
            }
        };

        if (totalSize == 0) return;

        ContentDialogResult result = await SimpleDialogs.SpawnDialog(
            string.Format(Locale.Lang._InstallMgmt.RepairFilesRequiredTitle, assetIndex.Count),
            content,
            ParentUI,
            Locale.Lang._Misc.Cancel,
            Locale.Lang._Misc.YesResume,
            null,
            ContentDialogButton.Primary,
            ContentDialogTheme.Warning);

        if (result == ContentDialogResult.None)
        {
            actionIfInteractiveCancel?.Invoke();
            throw new OperationCanceledException();
        }
    }
    #endregion
}
