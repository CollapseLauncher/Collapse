using CollapseLauncher.Extension;
using CollapseLauncher.Helper.Metadata;
using Hi3Helper;
using Hi3Helper.Plugin.Core;
using Hi3Helper.Plugin.Core.Management;
using Hi3Helper.Plugin.Core.Management.Api;
using Hi3Helper.Plugin.Core.Management.PresetConfig;
using Hi3Helper.Plugin.Core.Utility;
using Hi3Helper.Shared.Region;
using Hi3Helper.Win32.ManagedTools;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using ILauncherApi = CollapseLauncher.Helper.LauncherApiLoader.ILauncherApi;

#nullable enable
namespace CollapseLauncher.Plugins;

public partial class PluginPresetConfigWrapper : PresetConfig, IDisposable
{
    public GameManagerExtension.RunGameFromGameManagerContext RunGameContext { get; }
    public readonly  PluginInfo                                         PluginInfo;
    public readonly  IPlugin                                            Plugin;
    private readonly IPluginPresetConfig                                _config;

    private bool _isWriteGameLog = true;

    private unsafe PluginPresetConfigWrapper(PluginInfo pluginInfo, IPluginPresetConfig config)
    {
        PluginInfo = pluginInfo;
        Plugin     = pluginInfo.Instance ?? throw new NullReferenceException("IPlugin interface cannot be null!");
        _config    = config;

        config.comGet_GameManager(out IGameManager? gameManager);
        PluginGameManager = gameManager ?? throw new NullReferenceException("IGameManager interface cannot be null!");

        RunGameContext = new GameManagerExtension.RunGameFromGameManagerContext
        {
            Plugin               = pluginInfo.Instance,
            PluginHandle         = pluginInfo.Handle,
            GameManager          = gameManager,
            PresetConfig         = config,
            PrintGameLogCallback = PrintGameLogCallback
        };
    }

    public unsafe GameManagerExtension.RunGameFromGameManagerContext UseToggledGameLaunchContext()
    {
        bool isUseConsole = LauncherConfig.GetAppConfigValue("EnableConsole");
        RunGameContext.PrintGameLogCallback = isUseConsole ? PrintGameLogCallback : null!;

        _isWriteGameLog = LauncherConfig.GetAppConfigValue("IncludeGameLogs");
        return RunGameContext;
    }

    public static PluginPresetConfigWrapper Create(PluginInfo pluginInfo, IPluginPresetConfig presetConfig)
        => new(pluginInfo, presetConfig);

    public static bool TryCreate(
        PluginInfo                                         pluginInfo,
        IPluginPresetConfig                                presetConfig,
        [NotNullWhen(true)] out PluginPresetConfigWrapper? wrapper)
    {
        Unsafe.SkipInit(out wrapper);
        ArgumentNullException.ThrowIfNull(presetConfig, nameof(presetConfig));

        try
        {
            wrapper = Create(pluginInfo, presetConfig);
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

    [field: AllowNull, MaybeNull]
    private string PrintGameLogName
    {
        get
        {
            if (field != null)
            {
                return field;
            }

            return field = $"{GameName} - {ZoneName}";
        }
    }

    public async Task InitializeAsync(CancellationToken token = default)
    {
        _config.InitAsync(Plugin.RegisterCancelToken(token), out nint asyncResult);
        int returnCode = await asyncResult.AsTask<int>();
        if (returnCode != 0)
        {
            throw new InvalidOperationException($"Failed to initialize IPluginPresetConfig with return code: {returnCode}");
        }
    }

    private unsafe void PrintGameLogCallback(char* logString, int logStringLen, int isStringCanFree)
    {
        Logger.LogWrite(new ReadOnlySpan<char>(logString, logStringLen),
                        LogType.Game,
                        appendNewLine: true,
                        writeToLog: _isWriteGameLog,
                        writeTypeTag: true,
                        writeTimestampOnLogFile: true);

        if (isStringCanFree == 1)
        {
            Marshal.FreeCoTaskMem((nint)logString);
        }
    }

    public void Dispose()
    {
        _config.Free();

        ReleaseComObject(PluginNewsApi);
        ReleaseComObject(PluginMediaApi);
        ReleaseComObject(PluginGameManager);
        ReleaseComObject(PluginGameInstaller);
        ReleaseComObject(_config);

        GC.SuppressFinalize(this);
        return;

        static void ReleaseComObject<T>(T obj)
            where T : class
        {
            if (!ComMarshal<T>.TryReleaseComObject(obj, out Exception? ex))
            {
                Logger.LogWriteLine($"[PluginPresetConfigWrapper::Dispose] Cannot release COM Instance of {typeof(T).Name}\r\n{ex}", LogType.Error, true);
            }
        }
    }
}
