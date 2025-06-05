using CollapseLauncher.Extension;
using CollapseLauncher.Helper.Metadata;
using Hi3Helper.Plugin.Core;
using Hi3Helper.Plugin.Core.Management;
using Hi3Helper.Plugin.Core.Management.Api;
using Hi3Helper.Plugin.Core.Management.PresetConfig;
using Hi3Helper.Plugin.Core.Utility;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using ILauncherApi = CollapseLauncher.Helper.LauncherApiLoader.ILauncherApi;

#nullable enable
namespace CollapseLauncher.Plugins;

internal class PluginPresetConfigWrapper : PresetConfig, IDisposable
{
    public readonly  IPlugin             Plugin;
    private readonly IPluginPresetConfig _config;

    private PluginPresetConfigWrapper(IPlugin plugin, IPluginPresetConfig config)
    {
        Plugin  = plugin;
        _config = config;
    }

    public static PluginPresetConfigWrapper Create(IPlugin plugin, IPluginPresetConfig presetConfig)
        => new(plugin, presetConfig);

    public static bool TryCreate(IPlugin                                            plugin,
                                 IPluginPresetConfig                                presetConfig,
                                 [NotNullWhen(true)] out PluginPresetConfigWrapper? wrapper)
    {
        Unsafe.SkipInit(out wrapper);
        ArgumentNullException.ThrowIfNull(presetConfig, nameof(presetConfig));

        try
        {
            wrapper = Create(plugin, presetConfig);
            return true;
        }
        catch (Exception ex)
        {
            SharedStatic.InstanceLogger?.LogError(ex, "Failed while trying to create IPluginPresetConfig wrapper");
        }

        return false;
    }

    public override ILauncherApi? GameLauncherApi
    {
        get => field ??= new PluginLauncherApiWrapper(Plugin, this);
        set;
    }

    public override GameNameType   GameType           => GameNameType.Plugin;
    public override LauncherType   LauncherType       => LauncherType.Plugin;
    public override GameVendorType VendorType         => GameVendorType.CollapsePlugin;
    public          string         VendorTypeInString => _config.get_GameVendorName();

    public override string? InternalGameNameInConfig
    {
        get => field ??= _config.get_GameRegistryKeyName();
        init;
    }

    public override string GameName        => _config.get_GameName();
    public override string ProfileName     => _config.get_ProfileName();
    public override string ZoneDescription => _config.get_ZoneDescription();
    public override string ZoneName        => _config.get_ZoneName();
    public override string ZoneFullname    => _config.get_ZoneFullName();
    public override string ZoneLogoURL     => _config.get_ZoneLogoUrl();
    public override string ZonePosterURL   => _config.get_ZonePosterUrl();
    public override string ZoneURL         => _config.get_ZoneHomePageUrl();

    public override string GameExecutableName => _config.get_GameExecutableName();
    public override string GameDirectoryName  => _config.get_LauncherGameDirectoryName();

    public string? GameLogFileName => field ??= _config.get_GameLogFileName();
    public string? GameAppDataPath => field ??= _config.get_GameAppDataPath();

    [field: AllowNull, MaybeNull]
    public override List<string> GameSupportedLanguages
    {
        get => field ??= Mem
                        .CreateArrayFromSelector(_config.get_GameSupportedLanguagesCount,
                                                 _config.get_GameSupportedLanguages)
                        .ToList();
        init;
    }

    public override GameChannel GameChannel
    {
        get => _config.get_ReleaseChannel() switch
        {
            GameReleaseChannel.OpenBeta => GameChannel.Beta,
            GameReleaseChannel.ClosedBeta => GameChannel.DevRelease,
            _ => GameChannel.Stable
        };
    }

    public override string DefaultLanguage => _config.get_GameMainLanguage();

    private         int? _hashID;
    public override int HashID { get => _hashID ??= HashCode.Combine(GameName, ZoneName); set => _hashID = value; }


    [field: AllowNull, MaybeNull]
    public ILauncherApiMedia PluginMediaApi => field ??= _config.get_LauncherApiMedia() ?? throw new NullReferenceException("ILauncherApiMedia interface cannot be null!");

    [field: AllowNull, MaybeNull]
    public ILauncherApiNews PluginNewsApi => field ??= _config.get_LauncherApiNews() ?? throw new NullReferenceException("ILauncherApiNews interface cannot be null!");

    [field: AllowNull, MaybeNull]
    public IGameManager PluginGameManager => field ??= _config.get_GameManager() ?? throw new NullReferenceException("IGameManager interface cannot be null!");

    public async Task InitializeAsync(CancellationToken token = default)
    {
        int returnCode = await _config.InitAsync(Plugin.RegisterCancelToken(token)).WaitFromHandle<int>();
        if (returnCode != 0)
        {
            throw new InvalidOperationException($"Failed to initialize IPluginPresetConfig with return code: {returnCode}");
        }
    }

    public void Dispose()
    {
        _config.Dispose();
        GC.SuppressFinalize(this);
    }
}
