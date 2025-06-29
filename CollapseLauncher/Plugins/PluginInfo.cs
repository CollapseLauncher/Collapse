using CollapseLauncher.Helper;
using Hi3Helper;
using Hi3Helper.Plugin.Core;
using Hi3Helper.Plugin.Core.Management;
using Hi3Helper.Plugin.Core.Management.PresetConfig;
using Hi3Helper.Shared.Region;
using Hi3Helper.Win32.Native.ManagedTools;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.Marshalling;
using System.Threading;
using System.Threading.Tasks;
using TurnerSoftware.DinoDNS;
// ReSharper disable LoopCanBeConvertedToQuery

namespace CollapseLauncher.Plugins;

#nullable enable
internal class PluginInfo : IDisposable
{
    internal unsafe delegate GameVersion* DelegateGetPluginStandardVersion();
    internal unsafe delegate GameVersion* DelegateGetPluginVersion();
    internal unsafe delegate void*        DelegateGetPlugin();
    internal delegate        void         DelegateFreePlugin();
    internal delegate        void         DelegateSetLoggerCallback(nint      loggerCallback);
    internal delegate        void         DelegateSetDnsResolverCallback(nint dnsResolverCallback);

    private bool _isDisposed;

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
    public NameServer[]?               NameServers     { get; private set; }

    private readonly DelegateSetDnsResolverCallback _setDnsResolverCallback;

    // ReSharper disable PrivateFieldCanBeConvertedToLocalVariable
    // Reason: These fields must be defined in the object's instance to avoid early Garbage Collection to the delegate.
    //         Defining it as local variables will cause early Garbage Collection and raise ExecutionEngineException.
    private readonly SharedLoggerCallback?      _sharedLoggerCallback;
    private readonly SharedDnsResolverCallback? _sharedDnsResolverCallback;
    // ReSharper enable PrivateFieldCanBeConvertedToLocalVariable

    public unsafe PluginInfo(string pluginFilePath)
    {
        nint   pluginHandle   = nint.Zero;
        bool   isPluginLoaded = false;
        string pluginRelName  = Path.Combine(Path.GetFileName(Path.GetDirectoryName(pluginFilePath)) ?? "", Path.GetFileName(pluginFilePath));

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

            // Set logger and DNS resolver callbacks
            _sharedLoggerCallback      = LoggerCallback;
            _sharedDnsResolverCallback = DnsResolverCallback;

            nint callbackForLogger = Marshal.GetFunctionPointerForDelegate(_sharedLoggerCallback);
            if (callbackForLogger != nint.Zero)
            {
                setLoggerCallbackHandle(callbackForLogger);
            }

            _setDnsResolverCallback = setDnsResolverCallbackHandle;

            // TODO: Add versioning check.
            GameVersion pluginStandardVersion = *getPluginStandardVersionHandle();
            GameVersion pluginVersion         = *getPluginVersionHandle();
            void*       pluginInstancePtr     = getPluginHandle();

            if (pluginInstancePtr == null)
            {
                throw new NullReferenceException($"Plugin's \"GetPlugin\" ({pluginRelName}) export function returns a null pointer!");
            }

            IPlugin? pluginInstance = ComInterfaceMarshaller<IPlugin>.ConvertToManaged(pluginInstancePtr);
            if (pluginInstance == null)
            {
                throw new NullReferenceException($"Plugin's \"GetPlugin\" ({pluginRelName}) export returns an invalid interface contract! Make sure that the plugin returns the valid interface instance!");
            }

            // Get preset configs
            pluginInstance.GetPresetConfigCount(out int presetConfigCount);
            if (presetConfigCount <= 0)
            {
                throw new InvalidOperationException($"Plugin: {pluginRelName} doesn't have IPluginPresetConfig definition!");
            }

            PresetConfigs = new PluginPresetConfigWrapper[presetConfigCount];
            for (int i = 0; i < presetConfigCount; i++)
            {
                pluginInstance.GetPresetConfig(i, out IPluginPresetConfig presetConfig);
                if (!PluginPresetConfigWrapper.TryCreate(pluginInstance, presetConfig, out PluginPresetConfigWrapper? presetConfigWrapper))
                {
                    throw new InvalidOperationException($"Plugin: {pluginRelName} returns an invalid IPluginPresetConfig at index {i}!");
                }
                PresetConfigs[i] = presetConfigWrapper;
            }

            // Set this PluginInfo properties
            pluginInstance.GetPluginName(out string? pluginName);
            pluginInstance.GetPluginDescription(out string? pluginDescription);
            pluginInstance.GetPluginAuthor(out string? pluginAuthor);
            pluginInstance.GetPluginCreationDate(out DateTime* pluginCreationDate);
            ILogger pluginLogger = ILoggerHelper.GetILogger(pluginRelName);

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

            pluginInstance.SetPluginLocaleId(LauncherConfig.GetAppConfigValue("AppLanguage"));

            Logger.LogWriteLine($"[PluginInfo] Successfully loaded plugin: {Name} from: {pluginRelName}@0x{libraryHandle:x8} with version {Version} and standard version {StandardVersion}.", LogType.Debug, true);

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

    internal async Task Initialize(CancellationToken token = default)
    {
        nint dnsCallback = !LauncherConfig.GetAppConfigValue("IsUseExternalDns") || _sharedDnsResolverCallback == null ?
            nint.Zero :
            Marshal.GetFunctionPointerForDelegate(_sharedDnsResolverCallback);

        if (dnsCallback != nint.Zero)
        {
            string? lExternalDnsAddresses = LauncherConfig.GetAppConfigValue("ExternalDnsAddresses");
            HttpClientBuilder.ParseDnsSettings(lExternalDnsAddresses, out string[]? nameServers, out DnsConnectionType dnsConnectionType);

            if (nameServers != null && nameServers.Length != 0)
            {
                List<NameServer> nameServerList = [];
                foreach (IPAddress currentHost in HttpClientBuilder
                    .EnumerateHostAsIp(nameServers)
                    .Distinct(HttpClientBuilder.IPAddressComparer))
                {
                    nameServerList.Add(new NameServer(currentHost, dnsConnectionType switch
                                                                   {
                                                                       DnsConnectionType.Udp => ConnectionType.Udp,
                                                                       DnsConnectionType.DoT => ConnectionType.DoT,
                                                                       _ => ConnectionType.DoH
                                                                   }));
                }

                if (nameServerList.Count > 0)
                {
                    NameServers = nameServerList.ToArray();
                    _setDnsResolverCallback(dnsCallback);
                }
            }
        }

        foreach (PluginPresetConfigWrapper preset in PresetConfigs)
        {
            await preset.InitializeAsync(token);
        }
    }

    internal void SetPluginLocaleId(string localeId) => Instance.SetPluginLocaleId(localeId);

    private static unsafe bool TryGetExport<T>(nint handle, string exportName, out T callback)
        where T : Delegate
    {
        const string tryGetApiExportName = "TryGetApiExport";

        Unsafe.SkipInit(out callback);

        if (!NativeLibrary.TryGetExport(handle, tryGetApiExportName, out nint tryGetApiExportP) ||
            tryGetApiExportP == nint.Zero)
        {
            return false;
        }

        delegate* unmanaged[Cdecl]<char*, void**, int> tryGetApiExportCallback = (delegate* unmanaged[Cdecl]<char*, void**, int>)tryGetApiExportP;

        nint exportP   = nint.Zero;
        int  tryResult = tryGetApiExportCallback(GetExportPtr(), (void**)&exportP);

        if (tryResult != 0 ||
            exportP == nint.Zero)
        {
            return false;
        }

        callback = Marshal.GetDelegateForFunctionPointer<T>(exportP);
        return true;

        char* GetExportPtr()
        {
            fixed (char* charP = exportName)
            {
                return charP;
            }
        }
    }

    public void Dispose()
    {
        if (_isDisposed)
        {
            return;
        }

        try
        {
            // Dispose loaded preset config
            foreach (var presetConfig in PresetConfigs)
            {
                presetConfig.Dispose();
            }

            if (TryGetExport(Handle, "SetLoggerCallback", out DelegateSetLoggerCallback setLoggerCallbackHandle) &&
                TryGetExport(Handle, "SetDnsResolverCallback", out DelegateSetDnsResolverCallback setDnsResolverCallbackHandle))
            {
                setLoggerCallbackHandle(nint.Zero);
                setDnsResolverCallbackHandle(nint.Zero);
                Logger.LogWriteLine($"[PluginInfo] Plugin: {Name} DNS and Logger Callbacks have been detached!", LogType.Debug, true);
            }

            // Try to dispose the IPlugin instance using the plugin's safe FreePlugin method first.
            if (TryGetExport(Handle, "FreePlugin", out DelegateFreePlugin freePluginCallback))
            {
                // Try call the free function.
                freePluginCallback();

                // Log the successful unload of the plugin.
                Logger.LogWriteLine($"[PluginInfo] Successfully unloaded plugin: {Name} ({Path.GetFileName(PluginFilePath)}@0x{Handle:x8}) using graceful free function.", LogType.Debug, true);
                return;
            }

            // If the graceful free function is not available, try to call Dispose method directly.
            Instance.Free();
            Logger.LogWriteLine($"[PluginInfo] Successfully unloaded plugin: {Name} ({Path.GetFileName(PluginFilePath)}@0x{Handle:x8}) using explicit function.", LogType.Debug, true);

            // Mark as disposed
            _isDisposed = true;
        }
        catch (Exception ex)
        {
            Logger.LogWriteLine($"[PluginInfo] Cannot dispose IPlugin instance from plugin: {Name} ({Path.GetFileName(PluginFilePath)}@0x{Handle:x8}) due to unexpected error: {ex}", LogType.Warning, true);
        }
        finally
        {
            // Free the plugin handle and remove it from the dictionary.
            ComMarshal.FreeInstance(Instance);
            NativeLibrary.Free(Handle);
        }
    }

    private unsafe void DnsResolverCallback(char* hostnameP, char* ipResolvedWriteBuffer, int ipResolvedWriteBufferLength, int* ipResolvedWriteCount)
    {
        ArgumentNullException.ThrowIfNull(NameServers, nameof(NameServers));

        string? hostname = MemoryMarshal.CreateReadOnlySpanFromNullTerminated(hostnameP).ToString();
        if (hostname == null)
        {
            *ipResolvedWriteCount = 0;
            return;
        }

        IPAddress[] resolvedIpAddresses =
            HttpClientBuilder.ResolveHostToIpAsync(hostname, NameServers, CancellationToken.None)
            .GetAwaiter()
            .GetResult();

        if (resolvedIpAddresses == null || resolvedIpAddresses.Length == 0)
        {
            *ipResolvedWriteCount = 0;
            return;
        }

        *ipResolvedWriteCount = resolvedIpAddresses.Length;

        int offset = 0;
        foreach (IPAddress ipAddress in resolvedIpAddresses)
        {
            string ipAsString = ipAddress.ToString();
            ipAsString.CopyTo(new Span<char>(ipResolvedWriteBuffer + offset, ipAsString.Length));
            ipResolvedWriteBuffer[offset += ipAsString.Length] = '\0';
            offset++;
        }
    }

#pragma warning disable CA2254, CS8500
    private unsafe void LoggerCallback(LogLevel* logLevel, EventId* eventId, char* messageBuffer, int messageLength)
        => PluginLogger.Log(logLevel: *logLevel, eventId: *eventId, message: new ReadOnlySpan<char>(messageBuffer, messageLength).ToString());
#pragma warning restore CA2254, CS8500

    public override string ToString() => $"{Name} (by {Author}) - {Description}";
}
