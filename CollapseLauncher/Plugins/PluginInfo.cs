using CollapseLauncher.Helper;
using Hi3Helper;
using Hi3Helper.Plugin.Core;
using Hi3Helper.Plugin.Core.Management.PresetConfig;
using Hi3Helper.Shared.Region;
using Microsoft.Extensions.Logging;
using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.Marshalling;
using System.Threading.Tasks;
using PluginGameVersion = Hi3Helper.Plugin.Core.Management.GameVersion;

namespace CollapseLauncher.Plugins;

#nullable enable
internal class PluginInfo : IDisposable
{
    internal unsafe delegate PluginGameVersion* DelegateGetPluginStandardVersion();
    internal unsafe delegate PluginGameVersion* DelegateGetPluginVersion();
    internal unsafe delegate void*              DelegateGetPlugin();
    internal delegate        void               DelegateFreePlugin();
    internal delegate        void               DelegateSetLoggerCallback(nint      loggerCallback);
    internal delegate        void               DelegateSetDnsResolverCallback(nint dnsResolverCallback);

    public GameVersion                 StandardVersion { get; }
    public GameVersion                 Version         { get; }
    public IPlugin                     Instance        { get; }
    public string                      PluginFilePath  { get; }
    public nint                        Handle          { get; }
    public string                      Name            { get; }
    public string                      Description     { get; }
    public string                      Author          { get; }
    public DateTime                    CreationDate    { get; }
    public PluginPresetConfigWrapper[] PresetConfigs   { get; }
    public ILogger                     PluginLogger    { get; }

    public unsafe PluginInfo(string pluginFilePath)
    {
        nint   pluginHandle   = nint.Zero;
        bool   isPluginLoaded = false;
        string pluginFileName = Path.GetFileName(pluginFilePath);

        try
        {
            if (!NativeLibrary.TryLoad(pluginFilePath, out nint libraryHandle))
            {
                Marshal.ThrowExceptionForHR(Marshal.GetHRForLastWin32Error());
            }

            if (!TryGetExport(libraryHandle, "GetPluginStandardVersion", out DelegateGetPluginStandardVersion getPluginStandardVersionHandle) ||
                !TryGetExport(libraryHandle, "GetPluginVersion", out DelegateGetPluginVersion getPluginVersionHandle) ||
                !TryGetExport(libraryHandle, "GetPlugin", out DelegateGetPlugin getPluginHandle) ||
                !TryGetExport(libraryHandle, "SetLoggerCallback", out DelegateSetLoggerCallback setLoggerCallbackHandle) ||
                !TryGetExport(libraryHandle, "SetDnsResolverCallback", out DelegateSetDnsResolverCallback setDnsResolverCallbackHandle))
            {
                throw new InvalidOperationException($"Plugin: {Path.GetFileName(pluginFilePath)} is missing some required exports. Plugin won't be loaded!");
            }

            // TODO: Add versioning check.
            GameVersion pluginStandardVersion = GameVersion.From(getPluginStandardVersionHandle()->AsSpan());
            GameVersion pluginVersion         = GameVersion.From(getPluginVersionHandle()->AsSpan());
            void*       pluginInstancePtr     = getPluginHandle();

            if (pluginInstancePtr == null)
            {
                throw new NullReferenceException("Plugin's \"GetPlugin\" export function returns a null pointer!");
            }

            IPlugin? pluginInstance = ComInterfaceMarshaller<IPlugin>.ConvertToManaged(pluginInstancePtr);
            if (pluginInstance == null)
            {
                throw new NullReferenceException("Plugin's \"GetPlugin\" export returns an invalid interface contract! Make sure that the plugin returns the valid interface instance!");
            }

            // Get preset configs
            int presetConfigCount = pluginInstance.GetPresetConfigCount();
            if (presetConfigCount <= 0)
            {
                throw new InvalidOperationException($"Plugin: {pluginFileName} doesn't have IPluginPresetConfig definition!");
            }

            PresetConfigs = new PluginPresetConfigWrapper[presetConfigCount];
            for (int i = 0; i < presetConfigCount; i++)
            {
                IPluginPresetConfig presetConfig = pluginInstance.GetPresetConfig(i);
                if (!PluginPresetConfigWrapper.TryCreate(presetConfig, out PluginPresetConfigWrapper? presetConfigWrapper))
                {
                    throw new InvalidOperationException($"Plugin: {pluginFileName} returns an invalid IPluginPresetConfig at index {i}!");
                }
                PresetConfigs[i] = presetConfigWrapper;
            }

            // Set this PluginInfo properties
            string    pluginName         = pluginInstance.GetPluginName();
            string    pluginDescription  = pluginInstance.GetPluginDescription();
            string    pluginAuthor       = pluginInstance.GetPluginAuthor();
            DateTime* pluginCreationDate = pluginInstance.GetPluginCreationDate();
            ILogger   pluginLogger       = ILoggerHelper.GetILogger(pluginFileName);

            Instance        = pluginInstance;
            StandardVersion = pluginStandardVersion;
            Version         = pluginVersion;
            PluginFilePath  = pluginFilePath;
            Handle          = libraryHandle;
            Name            = !string.IsNullOrEmpty(pluginName) ? pluginName : "Unknown";
            Description     = !string.IsNullOrEmpty(pluginDescription) ? pluginDescription : "No description provided.";
            Author          = !string.IsNullOrEmpty(pluginAuthor) ? pluginAuthor : "Unknown Author";
            CreationDate    = pluginCreationDate == null ? DateTime.MinValue : *pluginCreationDate;
            PluginLogger    = pluginLogger;

            // Set logger and DNS resolver callbacks
            nint callbackForDnsResolver = LauncherConfig.GetAppConfigValue("IsUseExternalDns") ? nint.Zero : Marshal.GetFunctionPointerForDelegate(DnsResolverCallback);
            nint callbackForLogger = Marshal.GetFunctionPointerForDelegate(LoggerCallback);

            setDnsResolverCallbackHandle(callbackForDnsResolver);
            if (callbackForLogger != nint.Zero)
            {
                setLoggerCallbackHandle(callbackForLogger);
            }

            Logger.LogWriteLine($"[PluginInfo] Successfully loaded plugin: {Name} @0x{libraryHandle:x8} with version {Version} and standard version {StandardVersion}.", LogType.Debug, true);

            isPluginLoaded = true;
        }
        catch
        {
            if (pluginHandle != nint.Zero && !isPluginLoaded)
            {
                NativeLibrary.Free(pluginHandle);
            }

            throw;
        }
    }

    internal async Task InitializePresetConfigs()
    {
        foreach (PluginPresetConfigWrapper preset in PresetConfigs)
        {
            await preset.InitializeAsync();
        }
    }

    private static bool TryGetExport<T>(nint handle, string exportName, out T callback)
        where T : Delegate
    {
        Unsafe.SkipInit(out callback);

        if (!NativeLibrary.TryGetExport(handle, exportName, out nint exportHandle) ||
            exportHandle == nint.Zero)
        {
            return false;
        }

        callback = Marshal.GetDelegateForFunctionPointer<T>(exportHandle);
        return true;
    }

    public void Dispose()
    {
        try
        {
            // Try to dispose the IPlugin instance using the plugin's safe FreePlugin method first.
            if (TryGetExport(Handle, "FreePlugin", out DelegateFreePlugin freePluginCallback))
            {
                // Try call the free function.
                freePluginCallback();

                // Log the successful unload of the plugin.
                Logger.LogWriteLine($"[LauncherMetadataHelper] Successfully unloaded plugin: {Name} using graceful free function.", LogType.Debug, true);
                return;
            }

            // If the graceful free function is not available, try to call Dispose method directly.
            Instance.Dispose();
            Logger.LogWriteLine($"[LauncherMetadataHelper] Successfully unloaded plugin: {Name} using explicit function.", LogType.Debug, true);
        }
        catch (Exception ex)
        {
            Logger.LogWriteLine($"[LauncherMetadataHelper] Cannot dispose IPlugin instance from plugin: {Name} @0x{Handle:x8} due to unexpected error: {ex}", LogType.Warning, true);
        }
        finally
        {
            // Free the plugin handle and remove it from the dictionary.
            NativeLibrary.Free(Handle);
        }
    }

    private static void DnsResolverCallback(string domain, out string[] resolvedIp)
    {
        HttpClientBuilder.TryGetCachedIp(domain, out IPAddress[]? resolvedIpAddresses);
        if (resolvedIpAddresses == null || resolvedIpAddresses.Length == 0)
        {
            resolvedIp = [];
            return;
        }

        resolvedIp = resolvedIpAddresses.Select(ip => ip.ToString()).ToArray();
    }

#pragma warning disable CA2254
    private void LoggerCallback(LogLevel logLevel, EventId eventId, string message)
        => PluginLogger.Log(logLevel: logLevel, eventId: eventId, message: message);
#pragma warning restore CA2254
}
