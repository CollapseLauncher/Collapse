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

#nullable enable
namespace CollapseLauncher.PluginWrapper
{
    internal class PluginPresetConfigWrapper : PresetConfig, IDisposable
    {
        private readonly IPluginPresetConfig _config;

        private PluginPresetConfigWrapper(IPluginPresetConfig config) => _config = config;

        public static PluginPresetConfigWrapper Create(IPluginPresetConfig presetConfig)
            => new(presetConfig);

        public static bool TryCreate(IPluginPresetConfig                                presetConfig,
                                     [NotNullWhen(true)] out PluginPresetConfigWrapper? wrapper)
        {
            Unsafe.SkipInit(out wrapper);

            try
            {
                wrapper = Create(presetConfig);
                return true;
            }
            catch (Exception ex)
            {
                SharedStatic.InstanceLogger?.LogError(ex, "Failed while trying to create IPluginPresetConfig wrapper");
            }

            return false;
        }

        public override string GameName => _config.get_GameName();

        public override string ProfileName => _config.get_ProfileName();

        public override string ZoneDescription => _config.get_ZoneDescription();

        public override string ZoneName => _config.get_ZoneName();

        public override string ZoneFullname => _config.get_ZoneFullName();

        public override string ZoneLogoURL => _config.get_ZoneLogoUrl();

        public override string ZonePosterURL => _config.get_ZonePosterUrl();

        public override string ZoneURL => _config.get_ZoneHomePageUrl();

        public override string GameExecutableName => _config.get_GameExecutableName();

        public override string GameDirectoryName => _config.get_LauncherGameDirectoryName();

        [field: AllowNull, MaybeNull]
        public override List<string> GameSupportedLanguages
        {
            get => field ??= Mem
                            .CreateArrayFromSelector(_config.get_GameSupportedLanguagesCount,
                                                     _config.get_GameSupportedLanguages)
                            .ToList();
            init => throw new NotSupportedException();
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

        [field: AllowNull, MaybeNull]
        public ILauncherApiMedia PluginMediaApi => field ??= _config.get_LauncherApiMedia() ?? throw new NullReferenceException("ILauncherApiMedia interface cannot be null!");

        [field: AllowNull, MaybeNull]
        public ILauncherApiNews PluginNewsApi => field ??= _config.get_LauncherApiNews() ?? throw new NullReferenceException("ILauncherApiNews interface cannot be null!");

        [field: AllowNull, MaybeNull]
        public IGameManager PluginGameManager => field ??= _config.get_GameManager() ?? throw new NullReferenceException("IGameManager interface cannot be null!");

        public void Dispose()
        {
            _config.Dispose();
            GC.SuppressFinalize(this);
        }
    }
}
