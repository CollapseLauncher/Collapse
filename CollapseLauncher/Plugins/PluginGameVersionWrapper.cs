using CollapseLauncher.GameManagement.Versioning;
using CollapseLauncher.Helper.Metadata;
using CollapseLauncher.Interfaces;
using Hi3Helper.Data;
using Hi3Helper.Plugin.Core;
using Hi3Helper.Plugin.Core.Management;
using Hi3Helper.Plugin.Core.Utility;
using Hi3Helper.Shared.ClassStruct;
using Microsoft.UI.Xaml;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

#nullable enable
namespace CollapseLauncher.Plugins;

internal class PluginGameVersionWrapper : GameVersionBase, IGameVersion
{
    private readonly PluginPresetConfigWrapper _pluginPresetConfig;
    private readonly IGameManager              _pluginGameManager;
    private          GameVendorProp?           _vendorTypeProp;

    internal PluginGameVersionWrapper(PluginPresetConfigWrapper presetConfig)
    {
        ArgumentNullException.ThrowIfNull(presetConfig, nameof(presetConfig));

        _pluginPresetConfig = presetConfig;
        _pluginGameManager  = presetConfig.PluginGameManager;

        // Initialize INI Prop ahead of other operations
        // ReSharper disable once VirtualMemberCallInConstructor
        InitializeIniProp();

        // Initialize the Game Path into the plugin
        _pluginGameManager.SetGamePath(base.GameDirPath);
        _pluginGameManager.LoadConfig();
    }

    public override void Reinitialize()
    {
        // Initialize the Game Path into the plugin
        _pluginGameManager.SetGamePath(base.GameDirPath);
        _pluginGameManager.LoadConfig();

        base.Reinitialize();
    }

    public override string GameName
    {
        get => _pluginPresetConfig.GameName ??
                       throw new NullReferenceException("Game Name in Plugin Preset Config cannot be null!");
    }

    public override string GameRegion => _pluginPresetConfig.ZoneName ?? throw new NullReferenceException("Game Region/Zone Name in Plugin Preset Config cannot be null!");

    public override IniSection? GameIniVersionSection => null;
    // public override IniSection GameIniProfileSection => null;

    public override string GameDirPath
    {
        get
        {
            _pluginGameManager.GetGamePath(out string? path);
            return path ?? string.Empty;
        }
        set
        {
            _pluginGameManager.SetGamePath(value);
            base.GameDirPath = value; // Ensure the path is set in the base class as well
        }
    }

    public override PresetConfig GamePreset => _pluginPresetConfig;

    // public override RegionResourceProp? GameApiProp { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

    public override GameNameType GameType => GameNameType.Plugin;

    public override string GameDirAppDataPath => _pluginPresetConfig.GameAppDataPath ?? string.Empty;
    public override string GameOutputLogName  => _pluginPresetConfig.GameLogFileName ?? string.Empty;

    public override GameVendorProp VendorTypeProp => _vendorTypeProp ??= new GameVendorProp
    {
        GameName   = _pluginPresetConfig.GameName,
        VendorType = _pluginPresetConfig.VendorTypeInString
    };

    public override ValueTask<bool> EnsureGameConfigIniCorrectiveness(UIElement uiParentElement) => ValueTask.FromResult(true);

    public override async Task<string?> FindGameInstallationPath(string path)
    {
        Guid cancelToken = Guid.CreateVersion7();
        _pluginGameManager
           .FindExistingInstallPathAsync(in cancelToken, out nint asyncFindExistingInstallPathResult);
        string? gameInstallPath = await asyncFindExistingInstallPathResult.WaitFromHandle<PluginDisposableMemoryMarshal>();

        return gameInstallPath;
    }

    public override DeltaPatchProperty? GetDeltaPatchInfo() => null;

    public override List<RegionResourceVersion>  GetGameLatestZip(GameInstallStateEnum gameState) => [];
    public override List<RegionResourcePlugin>?  GetGamePluginZip()                               => null;
    public override List<RegionResourceVersion>? GetGamePreloadZip()                              => null;
    public override List<RegionResourcePlugin>?  GetGameSdkZip()                                  => null;


    // ReSharper disable ConvertIfStatementToReturnStatement
    ValueTask<GameInstallStateEnum> IGameVersion.GetGameState()
    {
        _pluginGameManager.IsGameHasPreload(out bool isHavePreload);
        _pluginGameManager.IsGameInstalled(out bool isGameInstalled);
        _pluginGameManager.IsGameHasUpdate(out bool isGameNeedsUpdate);

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

    public override GameVersion? GetGameExistingVersion()
    {
        _pluginGameManager.GetCurrentGameVersion(out GameVersion gameVersion);
        if (gameVersion == GameVersion.Empty)
        {
            return null;
        }

        return gameVersion;
    }

    public override GameVersion? GetGameVersionApi()
    {
        _pluginGameManager.GetApiGameVersion(out GameVersion gameVersion);
        if (gameVersion == GameVersion.Empty)
        {
            return null;
        }

        return gameVersion;
    }

    public override GameVersion? GetGameVersionApiPreload()
    {
        _pluginGameManager.GetApiPreloadGameVersion(out GameVersion gameVersion);

        if (gameVersion == GameVersion.Empty)
        {
            return null;
        }

        return gameVersion;
    }

    /*
    public override void InitializeIniProp()
    {
        // NOP
    }
    */

    public override bool IsForceRedirectToSophon() => false;
    public override bool IsGameHasDeltaPatch()     => false;

    public override bool IsGameHasPreload()
    {
        _pluginGameManager.IsGameHasPreload(out bool result);
        return result;
    }
    public override bool IsGameInstalled()
    {
        _pluginGameManager.IsGameInstalled(out bool result);
        return result;
    }
    public override bool IsGameVersionMatch() => GetGameExistingVersion() == GetGameVersionApi();

    public override ValueTask<bool> IsPluginVersionsMatch() => ValueTask.FromResult(true);
    public override ValueTask<bool> IsSdkVersionsMatch()    => ValueTask.FromResult(true);

    /*
    public override void Reinitialize()
    {
        // NOP
    }
    */

    public override void UpdateGameChannels(bool saveValue = true)
    {
        // NOP
    }

    public void UpdateGamePath() => UpdateGamePath(GameDirPath);

    public override void UpdateGamePath(string? path, bool saveValue = true)
    {
        _pluginGameManager.SetGamePath(path ?? throw new ArgumentNullException(nameof(path), "path cannot be null!"));
        if (saveValue)
        {
            _pluginGameManager.SaveConfig();
        }
        base.UpdateGamePath(path, saveValue);
    }

    public override void UpdateGameVersion(GameVersion? version, bool saveValue = true)
    {
        if (!version.HasValue)
        {
            throw new ArgumentNullException(nameof(version), "version cannot be null!");
        }

        _pluginGameManager.SetCurrentGameVersion(version.Value);
        
        _pluginGameManager.GetGamePath(out string? currentPath);
        if (string.IsNullOrEmpty(currentPath))
        {
            return;
        }

        _pluginGameManager.SetGamePath(currentPath);
        if (saveValue)
        {
            _pluginGameManager.SaveConfig();
        }
    }

    public override void UpdateGameVersionToLatest(bool saveValue = true) => UpdateGameVersion(GetGameVersionApi(), saveValue);

    public override void UpdatePluginVersions(Dictionary<string, GameVersion> versions, bool saveValue = true)
    {
        // NOP
    }

    public override void UpdateSdkVersion(GameVersion? version, bool saveValue = true)
    {
        // NOP
    }
}
