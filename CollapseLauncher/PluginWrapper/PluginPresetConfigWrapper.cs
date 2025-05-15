using CollapseLauncher.Helper.Metadata;
using Hi3Helper.Plugin.Core;
using Hi3Helper.Plugin.Core.Management;
using Hi3Helper.Plugin.Core.Utility;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.Marshalling;

#nullable enable
namespace CollapseLauncher.PluginWrapper
{
    internal unsafe class PluginPresetConfigWrapper : PresetConfig
    {
        private readonly IPluginPresetConfig _config;

        private PluginPresetConfigWrapper(IPluginPresetConfig config)
        {
            _config = config;
        }

        private PluginPresetConfigWrapper(void* handle)
        {
            IPluginPresetConfig? config = ComInterfaceMarshaller<IPluginPresetConfig>.ConvertToManaged(handle);
            _config = config ?? throw new COMException($"Cannot marshal handle: 0x{(nint)handle:x8} to IPluginPresetConfig");
        }

        public static PluginPresetConfigWrapper Create(IPluginPresetConfig presetConfig)
            => new(presetConfig);

        public static PluginPresetConfigWrapper Create(void* handle)
            => new(handle);

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

        public static bool TryCreate(void*                                              handle,
                                     [NotNullWhen(true)] out PluginPresetConfigWrapper? wrapper)
        {
            Unsafe.SkipInit(out wrapper);

            try
            {
                wrapper = Create(handle);
                return true;
            }
            catch (Exception ex)
            {
                SharedStatic.InstanceLogger?.LogError(ex, "Failed while trying to create IPluginPresetConfig wrapper from handle: 0x{handle:x8}", (nint)handle);
            }

            return false;
        }

        public override string GameName           => _config.get_GameName();
        public override string ProfileName        => _config.get_ProfileName();
        public override string ZoneDescription    => _config.get_ZoneDescription();
        public override string ZoneName           => _config.get_ZoneName();
        public override string ZoneFullname       => _config.get_ZoneFullName();
        public override string ZoneLogoURL        => _config.get_ZoneLogoUrl();
        public override string ZonePosterURL      => _config.get_ZonePosterUrl();
        public override string ZoneURL            => _config.get_ZoneHomePageUrl();
        public override string GameExecutableName => _config.get_GameExecutableName();
        public override string GameDirectoryName  => _config.get_LauncherGameDirectoryName();

        [field: AllowNull, MaybeNull]
        public override List<string> GameSupportedLanguages
        {
            get => field ??= PluginComUtility
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
    }
}
