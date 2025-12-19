using CollapseLauncher.Helper.LauncherApiLoader.HoYoPlay;
using Hi3Helper.Plugin.Core.Management;
using System.Collections.Generic;

#nullable enable
namespace CollapseLauncher.Interfaces.Class;

internal class GamePackageResult(List<HypPackageData> mainPackage,
                                 List<HypPackageData> audioPackage,
                                 string?              uncompressedUrl = null,
                                 GameVersion          gameVersion     = default)
{
    public static GamePackageResult Empty => new([], []);

    public List<HypPackageData> MainPackage     { get; } = mainPackage;
    public List<HypPackageData> AudioPackage    { get; } = audioPackage;
    public string?              UncompressedUrl { get; } = uncompressedUrl;
    public GameVersion          Version         { get; } = gameVersion;
}
