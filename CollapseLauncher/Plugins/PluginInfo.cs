using CollapseLauncher.Helper;
using Hi3Helper;
using Hi3Helper.Plugin.Core;
using Hi3Helper.Plugin.Core.Management;
using Hi3Helper.Plugin.Core.Management.PresetConfig;
using Hi3Helper.Plugin.Core.Update;
using Hi3Helper.Plugin.Core.Utility;
using Hi3Helper.Shared.Region;
using Hi3Helper.Win32.ManagedTools;
using Microsoft.Extensions.Logging;
using System;
using System.ComponentModel;
using System.IO;
using System.Net;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.Marshalling;
using System.Threading;
using System.Threading.Tasks;
using WinRT;

// ReSharper disable LoopCanBeConvertedToQuery

namespace CollapseLauncher.Plugins;

#nullable enable
[GeneratedBindableCustomProperty]
public partial class PluginInfo : INotifyPropertyChanged, IDisposable
{
    internal const string MarkDisabledFileName           = "_markDisabled";
    internal const string MarkPendingDeletionFileName    = "_markPendingDeletion";
    internal const string MarkPendingUpdateFileName      = "_markPendingUpdate";
    internal const string MarkPendingUpdateApplyFileName = "_markPendingUpdateApply";

    internal unsafe delegate void         DelegateGetPluginUpdateCdnList(int* count, ushort*** ptr);
    internal unsafe delegate GameVersion* DelegateGetPluginStandardVersion();
    internal unsafe delegate GameVersion* DelegateGetPluginVersion();
    internal unsafe delegate void*        DelegateGetPlugin();
    internal delegate        void         DelegateFreePlugin();
    internal delegate        void         DelegateSetCallback(nint callbackP);

    private bool _isDisposed;

    public event PropertyChangedEventHandler? PropertyChanged = delegate { };

    public GameVersion                 StandardVersion { get; }
    public GameVersion                 Version         { get; }
    public IPlugin?                    Instance        { get; }
    public nint                        Handle          { get; }
    public PluginPresetConfigWrapper[] PresetConfigs   { get; }
    public ILogger                     PluginLogger    { get; }

    public bool IsEnabled
    {
        get => !GetMarkState(PluginDirPath, MarkDisabledFileName);
        set
        {
            SetMarkState(PluginDirPath, MarkDisabledFileName, !value, out bool isValid);
            if (isValid)
            {
                OnPropertyChanged();
            }
        }
    }

    public bool IsMarkedForDeletion
    {
        get => GetMarkState(PluginDirPath, MarkPendingDeletionFileName);
        set
        {
            SetMarkState(PluginDirPath, MarkPendingDeletionFileName, value, out bool isValid);
            if (isValid)
            {
                OnPropertyChanged();
            }
        }
    }

    public bool IsLoaded { get; set; }

    public string         PluginDirPath  { get; }
    public string         PluginFilePath { get; }
    public string         PluginFileName { get; }
    public string         PluginKey      { get; }
    public PluginManifest PluginManifest { get; set; }
    public string?        Name           => field ?? Locale.Lang._SettingsPage.Plugin_PluginInfoNameUnknown;
    public string?        Description    => field ?? Locale.Lang._SettingsPage.Plugin_PluginInfoDescUnknown;
    public string?        Author         => field ?? Locale.Lang._SettingsPage.Plugin_PluginInfoAuthorUnknown;
    public DateTime?      CreationDate   => field ?? DateTime.MinValue;

    // ReSharper disable once PrivateFieldCanBeConvertedToLocalVariable
    // Reason: These fields must be defined in the object's instance to avoid early Garbage Collection to the delegate.
    //         Defining it as local variables will cause early Garbage Collection and raise ExecutionEngineException.
    private SharedLoggerCallback? _sharedLoggerCallback;
    private GCHandle              _sharedLoggerCallbackGcHandle;

    private static readonly SharedDnsResolverCallback      SharedDnsResolverCallback;
    private static readonly SharedDnsResolverCallbackAsync SharedDnsResolverCallbackAsync;

    static unsafe PluginInfo()
    {
        SharedDnsResolverCallback      = DnsResolverCallback;
        SharedDnsResolverCallbackAsync = DnsResolverCallbackAsync;
    }

    public unsafe PluginInfo(string pluginFilePath, string pluginRelName, PluginManifest manifest, bool load = false)
    {
        nint   pluginHandle   = nint.Zero;
        bool   isPluginLoaded = false;
        string pluginBaseDir  = Path.GetDirectoryName(pluginFilePath) ?? "";

        // Set callback
        _sharedLoggerCallback = LoggerCallback;

        ILogger pluginLogger = ILoggerHelper.GetILogger(pluginRelName);

        if (!load)
        {
            PresetConfigs   = [];
            StandardVersion = manifest.PluginStandardVersion;
            Version         = manifest.PluginVersion;
            PluginManifest  = manifest;
            PluginDirPath   = pluginBaseDir;
            PluginFilePath  = pluginFilePath;
            PluginFileName  = manifest.MainLibraryName;
            PluginKey       = pluginRelName;
            Name            = manifest.MainPluginName;
            Description     = manifest.MainPluginDescription;
            Author          = manifest.MainPluginAuthor;
            CreationDate    = manifest.PluginCreationDate.DateTime;
            PluginLogger    = pluginLogger;
            IsLoaded        = false;

            return;
        }

        try
        {
            if (!NativeLibrary.TryLoad(pluginFilePath, out nint libraryHandle))
            {
                Marshal.ThrowExceptionForHR(Marshal.GetHRForLastWin32Error());
            }

            if (!libraryHandle.TryGetExport("GetPluginStandardVersion", out DelegateGetPluginStandardVersion getPluginStandardVersionHandle) ||
                !libraryHandle.TryGetExport("GetPluginVersion", out DelegateGetPluginVersion getPluginVersionHandle) ||
                !libraryHandle.TryGetExport("GetPlugin", out DelegateGetPlugin getPluginHandle) ||
                !libraryHandle.TryGetExport("SetLoggerCallback", out DelegateSetCallback setLoggerCallbackHandle))
            {
                throw new InvalidOperationException($"Plugin: {Path.GetFileName(pluginFilePath)} is missing some required exports. Plugin won't be loaded!");
            }

            _sharedLoggerCallbackGcHandle = GCHandle.Alloc(_sharedLoggerCallback); // Prevent the delegate from getting GCed
            nint callbackForLogger = Marshal.GetFunctionPointerForDelegate(_sharedLoggerCallback);
            if (callbackForLogger != nint.Zero)
            {
                setLoggerCallbackHandle(callbackForLogger);
            }

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

            // Get Self Updater
            pluginInstance.GetPluginSelfUpdater(out IPluginSelfUpdate? selfUpdater);
            Updater = selfUpdater;

            // Get Managed Update CDN List
            if (libraryHandle.TryGetExport("GetPluginUpdateCdnList", out DelegateGetPluginUpdateCdnList getPluginUpdateCdnList))
            {
                int      pluginCdnListCount = 0;
                ushort** urlsPtr            = null;

                getPluginUpdateCdnList(&pluginCdnListCount, &urlsPtr);

                if (pluginCdnListCount != 0 && urlsPtr != null)
                {
                    string[] urlList = GC.AllocateUninitializedArray<string>(pluginCdnListCount);
                    for (int i = 0; i < pluginCdnListCount; i++)
                    {
                        ushort* ptr = urlsPtr[i];
                        urlList[i] = Utf16StringMarshaller.ConvertToManaged(ptr) ?? "";
                        Utf16StringMarshaller.Free(ptr);
                    }

                    UpdateCdnList = urlList;
                }
            }

            // Get preset configs
            pluginInstance.GetPresetConfigCount(out int presetConfigCount);
            if (presetConfigCount <= 0)
            {
                throw new InvalidOperationException($"Plugin: {pluginRelName} doesn't have IPluginPresetConfig definition!");
            }

            // Set this PluginInfo properties
            pluginInstance.GetPluginName(out string? pluginName);
            pluginInstance.GetPluginDescription(out string? pluginDescription);
            pluginInstance.GetPluginAuthor(out string? pluginAuthor);
            pluginInstance.GetPluginCreationDate(out DateTime* pluginCreationDate);

            Instance        = pluginInstance;
            StandardVersion = pluginStandardVersion;
            Version         = pluginVersion;
            PluginManifest  = manifest;
            PluginDirPath   = pluginBaseDir;
            PluginFilePath  = pluginFilePath;
            PluginFileName  = manifest.MainLibraryName;
            PluginKey       = pluginRelName;
            Handle          = libraryHandle;
            Name            = pluginName ?? manifest.MainPluginName;
            Description     = pluginDescription ?? manifest.MainPluginDescription;
            Author          = pluginAuthor ?? manifest.MainPluginAuthor;
            CreationDate    = pluginCreationDate == null ? manifest.PluginCreationDate.DateTime : *pluginCreationDate;
            PluginLogger    = pluginLogger;
            IsLoaded        = true;

            pluginInstance.SetPluginLocaleId(LauncherConfig.GetAppConfigValue("AppLanguage"));

            Logger.LogWriteLine($"[PluginInfo] Successfully loaded plugin: {Name} from: {pluginRelName}@0x{libraryHandle:x8} with version {Version} and standard version {StandardVersion}.", LogType.Debug, true);

            PresetConfigs = new PluginPresetConfigWrapper[presetConfigCount];
            for (int i = 0; i < presetConfigCount; i++)
            {
                pluginInstance.GetPresetConfig(i, out IPluginPresetConfig presetConfig);
                if (!PluginPresetConfigWrapper.TryCreate(this, presetConfig, out PluginPresetConfigWrapper? presetConfigWrapper))
                {
                    throw new InvalidOperationException($"Plugin: {pluginRelName} returns an invalid IPluginPresetConfig at index {i}!");
                }
                PresetConfigs[i] = presetConfigWrapper;
            }

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

    internal void EnableDnsResolver()
    {
        if (!IsLoaded)
        {
            return;
        }

        if (Handle.TryGetExport("SetDnsResolverCallback",
                                out DelegateSetCallback setDnsResolverCallbackHandle))
        {
            nint dnsCallback = Marshal.GetFunctionPointerForDelegate(SharedDnsResolverCallback);
            setDnsResolverCallbackHandle(dnsCallback);
        }

        if (!Handle.TryGetExport("SetDnsResolverCallbackAsync",
                                 out DelegateSetCallback setDnsResolverCallbackAsyncHandle))
        {
            return;
        }

        nint dnsCallbackAsync = Marshal.GetFunctionPointerForDelegate(SharedDnsResolverCallbackAsync);
        setDnsResolverCallbackAsyncHandle(dnsCallbackAsync);
    }

    internal void DisableDnsResolver()
    {
        if (!IsLoaded)
        {
            return;
        }

        if (Handle.TryGetExport("SetDnsResolverCallback",
                                out DelegateSetCallback setDnsResolverCallbackHandle))
        {
            setDnsResolverCallbackHandle(nint.Zero);
        }

        if (Handle.TryGetExport("SetDnsResolverCallbackAsync",
                                out DelegateSetCallback setDnsResolverCallbackAsyncHandle))
        {
            setDnsResolverCallbackAsyncHandle(nint.Zero);
        }
    }

    internal async Task Initialize(CancellationToken token = default)
    {
        if (!IsLoaded)
        {
            return;
        }

        if (HttpClientBuilder.SharedExternalDnsServers != null)
        {
            EnableDnsResolver();
        }

        foreach (PluginPresetConfigWrapper preset in PresetConfigs)
        {
            await preset.InitializeAsync(token);
        }
    }

    internal void SetPluginLocaleId(string localeId) => Instance?.SetPluginLocaleId(localeId);

    public void Dispose()
    {
        if (_isDisposed)
        {
            return;
        }

        try
        {
            // Dispose loaded preset config
            foreach (PluginPresetConfigWrapper presetConfig in PresetConfigs)
            {
                presetConfig.Dispose();
            }

            if (Handle.TryGetExport("SetLoggerCallback", out DelegateSetCallback setLoggerCallbackHandle))
            {
                setLoggerCallbackHandle(nint.Zero);
                Logger.LogWriteLine($"[PluginInfo] Plugin: {Name} Logger Callbacks have been detached!", LogType.Debug, true);
            }

            DisableDnsResolver();
            Logger.LogWriteLine($"[PluginInfo] Plugin: {Name} DNS Resolver Callbacks have been detached!", LogType.Debug, true);

            // Try to dispose the IPlugin instance using the plugin's safe FreePlugin method first.
            if (Handle.TryGetExport("FreePlugin", out DelegateFreePlugin freePluginCallback))
            {
                // Try call the free function.
                freePluginCallback();

                // Log the successful unload of the plugin.
                Logger.LogWriteLine($"[PluginInfo] Successfully unloaded plugin: {Name} ({Path.GetFileName(PluginFilePath)}@0x{Handle:x8}) using graceful free function.", LogType.Debug, true);
                return;
            }

            // If the graceful free function is not available, try to call Dispose method directly.
            Instance?.Free();
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

            // Free GCHandle and nullify the delegate.
            _sharedLoggerCallbackGcHandle.Free();
            Interlocked.Exchange(ref _sharedLoggerCallback, null);
        }
        GC.SuppressFinalize(this);
    }

    private static unsafe nint DnsResolverCallbackAsync(char* hostnameP, int hostnameLength, void** cancelCallbackP)
    {
        string hostnameString = new string(new ReadOnlySpan<char>(hostnameP, hostnameLength));

        CancellationTokenSource tcs            = new CancellationTokenSource();
        VoidCallback            cancelCallback = CancelDelegate;
        GCHandle                handle         = GCHandle.Alloc(cancelCallback); // Lock the callback from getting GCed

        *cancelCallbackP = (void*)Marshal.GetFunctionPointerForDelegate(cancelCallback);

        Task<nint> task = DnsResolverCallbackAsync(hostnameString, tcs.Token);
        task.GetAwaiter()
            .OnCompleted(() =>
                         {
                             handle.Free(); // Allow the GC to free the callback
                         });

        return task.AsResult();

        void CancelDelegate()
        {
            tcs.Cancel();
            tcs.Dispose();
        }
    }

    private static async Task<nint> DnsResolverCallbackAsync(string hostname, CancellationToken token)
    {
        ArgumentNullException.ThrowIfNull(HttpClientBuilder.SharedExternalDnsServers, nameof(HttpClientBuilder.SharedExternalDnsServers));

        IPAddress[] resolvedIpAddresses =
            await HttpClientBuilder.ResolveHostToIpAsync(hostname,
                                                         HttpClientBuilder.SharedExternalDnsServers,
                                                         token);

        nint ptr = DnsARecordResult.CreateToIntPtr(resolvedIpAddresses);
        return ptr;
    }

    private static unsafe void DnsResolverCallback(char* hostnameP, char* ipResolvedWriteBuffer, int ipResolvedWriteBufferLength, int* ipResolvedWriteCount)
    {
        ArgumentNullException.ThrowIfNull(HttpClientBuilder.SharedExternalDnsServers, nameof(HttpClientBuilder.SharedExternalDnsServers));

        string hostname = MemoryMarshal.CreateReadOnlySpanFromNullTerminated(hostnameP).ToString();
        if (string.IsNullOrEmpty(hostname))
        {
            *ipResolvedWriteCount = 0;
            return;
        }

        IPAddress[] resolvedIpAddresses =
            HttpClientBuilder.ResolveHostToIpAsync(hostname, HttpClientBuilder.SharedExternalDnsServers, CancellationToken.None)
            .GetAwaiter()
            .GetResult();

        if (resolvedIpAddresses.Length == 0)
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

    private static bool GetMarkState(string dir, string stateName)
    {
        string markPath = Path.Combine(dir, stateName);
        return File.Exists(markPath);
    }

    private static void SetMarkState(string dir, string stateName, bool state, out bool isValid)
    {
        try
        {
            string markPath    = Path.Combine(dir, stateName);
            bool   isFileExist = File.Exists(markPath);

            isValid = (state && !isFileExist) || (!state && isFileExist);

            switch (state)
            {
                case true when !isFileExist:
                    File.WriteAllText(markPath, stateName);
                    return;
                case false when isFileExist:
                    File.Delete(markPath);
                    break;
            }
        }
        catch (Exception e)
        {
            Logger.LogWriteLine($"[PluginInfo::SetMarkState()] Failed to set plugin state: {stateName} to {state} due to unexpected error: {e}", LogType.Error, true);
            throw;
        }
    }

#pragma warning disable CA2254, CS8500
    private unsafe void LoggerCallback(LogLevel* logLevel, EventId* eventId, char* messageBuffer, int messageLength)
        => PluginLogger.Log(logLevel: *logLevel, eventId: *eventId, message: new ReadOnlySpan<char>(messageBuffer, messageLength).ToString());
#pragma warning restore CA2254, CS8500

    public override string ToString() => $"{Name} (by {Author}) - {Description}";

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        InnerLauncherConfig
           .m_mainPage?
           .DispatcherQueue
           .TryEnqueue(() => PropertyChanged?
                          .Invoke(this, new PropertyChangedEventArgs(propertyName)));
    }
}
