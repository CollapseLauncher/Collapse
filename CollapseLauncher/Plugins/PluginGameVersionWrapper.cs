using CollapseLauncher.Helper.Metadata;
using CollapseLauncher.Interfaces;
using Hi3Helper.Data;
using Hi3Helper.Plugin.Core.Management;
using Hi3Helper.Shared.ClassStruct;
using Microsoft.UI.Xaml;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

using GameVersion = CollapseLauncher.GameVersion;
using PluginGameVersion = Hi3Helper.Plugin.Core.Management.GameVersion;

#nullable enable
namespace CollapseLauncher.Plugins;

internal class PluginGameVersionWrapper : IGameVersion
{
    private readonly PluginPresetConfigWrapper _pluginPresetConfig;
    private readonly IGameManager              _pluginGameManager;
    private          GameVendorProp?           _vendorTypeProp;

    internal PluginGameVersionWrapper(PluginPresetConfigWrapper presetConfig)
    {
        ArgumentNullException.ThrowIfNull(presetConfig, nameof(presetConfig));

        _pluginPresetConfig = presetConfig;
        _pluginGameManager  = presetConfig.PluginGameManager;
    }

    public string GameName => _pluginPresetConfig.GameName ?? throw new NullReferenceException("Game Name in Plugin Preset Config cannot be null!");
    public string GameRegion => _pluginPresetConfig.ZoneName ?? throw new NullReferenceException("Game Region/Zone Name in Plugin Preset Config cannot be null!");

    public IniSection? GameIniVersionSection => null;
    public IniSection? GameIniProfileSection => null;

    public string GameDirPath
    {
        get => _pluginGameManager.GetGamePath() ?? string.Empty;
        set => _pluginGameManager.SetGamePath(value);
    }

    public PresetConfig GamePreset => _pluginPresetConfig;

    public RegionResourceProp GameApiProp { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

    public GameNameType GameType => GameNameType.Plugin;

    public string GameDirAppDataPath => _pluginPresetConfig.GameAppDataPath ?? string.Empty;
    public string GameOutputLogName  => _pluginPresetConfig.GameLogFileName ?? string.Empty;

    public GameVendorProp VendorTypeProp => _vendorTypeProp ??= new GameVendorProp();

    public ValueTask<bool> EnsureGameConfigIniCorrectiveness(UIElement uiParentElement) => ValueTask.FromResult(true);

    public string? FindGameInstallationPath(string path) => null;

    public DeltaPatchProperty? GetDeltaPatchInfo() => null;

    public List<RegionResourceVersion>  GetGameLatestZip(GameInstallStateEnum gameState) => [];
    public List<RegionResourcePlugin>?  GetGamePluginZip()                               => null;
    public List<RegionResourceVersion>? GetGamePreloadZip()                              => null;
    public List<RegionResourcePlugin>?  GetGameSdkZip()                                  => null;


    // ReSharper disable ConvertIfStatementToReturnStatement
    ValueTask<GameInstallStateEnum> IGameVersion.GetGameState()
    {
        bool isHavePreload     = _pluginGameManager.IsGameHasPreload();
        bool isGameInstalled   = _pluginGameManager.IsGameInstalled();
        bool isGameNeedsUpdate = _pluginGameManager.IsGameHasUpdate();

        if (isGameInstalled && isHavePreload)
        {
            return ValueTask.FromResult(GameInstallStateEnum.InstalledHavePreload);
        }

        if (isGameNeedsUpdate)
        {
            return ValueTask.FromResult(GameInstallStateEnum.NeedsUpdate);
        }

        if (isGameInstalled)
        {
            return ValueTask.FromResult(GameInstallStateEnum.Installed);
        }

        return ValueTask.FromResult(GameInstallStateEnum.NotInstalled);
    }
    // ReSharper enable ConvertIfStatementToReturnStatement

    public GameVersion? GetGameExistingVersion()
    {
        _pluginGameManager.GetCurrentGameVersion(out PluginGameVersion gameVersion);
        if (gameVersion == PluginGameVersion.Empty)
        {
            return null;
        }

        return GameVersion.From(gameVersion);
    }

    public GameVersion? GetGameVersionApi()
    {
        _pluginGameManager.GetApiGameVersion(out PluginGameVersion gameVersion);
        if (gameVersion == PluginGameVersion.Empty)
        {
            return null;
        }

        return GameVersion.From(gameVersion);
    }

    public GameVersion? GetGameVersionApiPreload()
    {
        _pluginGameManager.GetApiPreloadGameVersion(out PluginGameVersion gameVersion);

        if (gameVersion == PluginGameVersion.Empty)
        {
            return null;
        }

        return GameVersion.From(gameVersion);
    }

    public void InitializeIniProp()
    {
        // NOP
    }

    public bool IsForceRedirectToSophon() => false;
    public bool IsGameHasDeltaPatch()     => false;

    public bool IsGameHasPreload()   => _pluginGameManager.IsGameHasPreload();
    public bool IsGameInstalled()    => _pluginGameManager.IsGameInstalled();
    public bool IsGameVersionMatch() => GetGameExistingVersion() == GetGameVersionApi();

    public ValueTask<bool> IsPluginVersionsMatch() => ValueTask.FromResult(true);
    public ValueTask<bool> IsSdkVersionsMatch()    => ValueTask.FromResult(true);

    void IGameVersion.Reinitialize()
    {
        // NOP
    }

    void IGameVersion.UpdateGameChannels(bool saveValue)
    {
        // NOP
    }

    public void UpdateGamePath(string? path, bool saveValue) => _pluginGameManager.SetGamePath(path ?? throw new ArgumentNullException(nameof(path), "path cannot be null!"), saveValue);

    public void UpdateGameVersion(GameVersion? version, bool saveValue)
    {
        if (!version.HasValue)
        {
            throw new ArgumentNullException(nameof(version), "version cannot be null!");
        }

        PluginGameVersion inGameVersion = new PluginGameVersion(version.Value.AsSpan());
        _pluginGameManager.SetCurrentGameVersion(in inGameVersion);
        
        string? currentPath = _pluginGameManager.GetGamePath();
        if (string.IsNullOrEmpty(currentPath))
        {
            return;
        }

        _pluginGameManager.SetGamePath(currentPath, saveValue);
    }

    public void UpdateGameVersionToLatest(bool saveValue) => UpdateGameVersion(GetGameVersionApi(), saveValue);

    public void UpdatePluginVersions(Dictionary<string, GameVersion> versions, bool saveValue)
    {
        // NOP
    }

    public void UpdateSdkVersion(GameVersion? version, bool saveValue)
    {
        // NOP
    }
}
