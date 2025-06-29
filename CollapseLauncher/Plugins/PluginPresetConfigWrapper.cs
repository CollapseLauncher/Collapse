using CollapseLauncher.Extension;
using CollapseLauncher.Helper.Metadata;
using Hi3Helper.Plugin.Core;
using Hi3Helper.Plugin.Core.Management;
using Hi3Helper.Plugin.Core.Management.Api;
using Hi3Helper.Plugin.Core.Management.PresetConfig;
using Hi3Helper.Plugin.Core.Utility;
using Hi3Helper.Win32.Native.ManagedTools;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
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
            SharedStatic.InstanceLogger.LogError(ex, "Failed while trying to create IPluginPresetConfig wrapper");
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
    public string VendorTypeInString
    {
        get
        {
            _config.comGet_GameVendorName(out string result);
            return result;
        }
    }

    public override string? InternalGameNameInConfig
    {
        get
        {
            if (field != null)
            {
                return field;
            }
            _config.comGet_GameRegistryKeyName(out field);

            return field;
        }
        init;
    }

    public override string GameName
    {
        get
        {
            _config.comGet_GameName(out string result);
            return result;
        }
    }

    public override string ProfileName
    {
        get
        {
            _config.comGet_ProfileName(out string result);
            return result;
        }
    }

    public override string ZoneDescription
    {
        get
        {
            _config.comGet_ZoneDescription(out string result);
            return result;
        }
    }

    public override string ZoneName
    {
        get
        {
            _config.comGet_ZoneName(out string result);
            return result;
        }
    }

    public override string ZoneFullname
    {
        get
        {
            _config.comGet_ZoneFullName(out string result);
            return result;
        }
    }

    public override string ZoneLogoURL
    {
        get
        {
            _config.comGet_ZoneLogoUrl(out string result);
            return result;
        }
    }

    public override string ZonePosterURL
    {
        get
        {
            _config.comGet_ZonePosterUrl(out string result);
            return result;
        }
    }

    public override string ZoneURL
    {
        get
        {
            _config.comGet_ZoneHomePageUrl(out string result);
            return result;
        }
    }

    public override string GameExecutableName
    {
        get
        {
            _config.comGet_GameExecutableName(out string result);
            return result;
        }
    }

    public override string GameDirectoryName
    {
        get
        {
            _config.comGet_LauncherGameDirectoryName(out string result);
            return result;
        }
    }

    public string? GameLogFileName
    {
        get
        {
            if (field != null)
            {
                return field;
            }
            _config.comGet_GameLogFileName(out field);
            return field;
        }
    }

    public string? GameAppDataPath
    {
        get
        {
            if (field != null)
            {
                return field;
            }
            _config.comGet_GameAppDataPath(out field);
            return field;
        }
    }

    [field: AllowNull, MaybeNull]
    public override List<string> GameSupportedLanguages
    {
        get
        {
            if (field != null)
            {
                return field;
            }

            _config.comGet_GameSupportedLanguagesCount(out int count);
            field = [];
            for (int i = 0; i < count; i++)
            {
                _config.comGet_GameSupportedLanguages(i, out string result);
                field.Add(result);
            }

            return field;
        }
        init;
    }

    public override GameChannel GameChannel
    {
        get
        {
            _config.comGet_ReleaseChannel(out GameReleaseChannel result);
            return result switch
                   {
                       GameReleaseChannel.OpenBeta => GameChannel.Beta,
                       GameReleaseChannel.ClosedBeta => GameChannel.DevRelease,
                       _ => GameChannel.Stable
                   };
        }
    }

    public override string DefaultLanguage
    {
        get
        {
            _config.comGet_GameMainLanguage(out string result);
            return result;
        }
    }

    private         int? _hashID;
    public override int  HashID { get => _hashID ??= HashCode.Combine(GameName, ZoneName); set => _hashID = value; }


    [field: AllowNull, MaybeNull]
    public ILauncherApiMedia PluginMediaApi
    {
        get
        {
            if (field != null)
            {
                return field;
            }

            _config.comGet_LauncherApiMedia(out field);
            return field ?? throw new NullReferenceException("ILauncherApiMedia interface cannot be null!");
        }
    }

    [field: AllowNull, MaybeNull]
    public ILauncherApiNews PluginNewsApi
    {
        get
        {
            if (field != null)
            {
                return field;
            }

            _config.comGet_LauncherApiNews(out field);
            return field ?? throw new NullReferenceException("ILauncherApiNews interface cannot be null!");
        }
    }

    [field: AllowNull, MaybeNull]
    public IGameManager PluginGameManager
    {
        get
        {
            if (field != null)
            {
                return field;
            }

            _config.comGet_GameManager(out field);
            return field ?? throw new NullReferenceException("IGameManager interface cannot be null!");
        }
    }

    [field: AllowNull, MaybeNull]
    public IGameInstaller PluginGameInstaller
    {
        get
        {
            if (field != null)
            {
                return field;
            }

            _config.comGet_GameInstaller(out field);
            return field ?? throw new NullReferenceException("IGameInstaller interface cannot be null!");
        }
    }

    public async Task InitializeAsync(CancellationToken token = default)
    {
        _config.InitAsync(Plugin.RegisterCancelToken(token), out nint asyncResult);
        int returnCode = await asyncResult.WaitFromHandle<int>();
        if (returnCode != 0)
        {
            throw new InvalidOperationException($"Failed to initialize IPluginPresetConfig with return code: {returnCode}");
        }
    }

    public void Dispose()
    {
        _config.Free();

        ComMarshal.FreeInstance(PluginNewsApi);
        ComMarshal.FreeInstance(PluginMediaApi);
        ComMarshal.FreeInstance(PluginGameManager);
        ComMarshal.FreeInstance(PluginGameInstaller);
        ComMarshal.FreeInstance(_config);

        GC.SuppressFinalize(this);
    }
}
