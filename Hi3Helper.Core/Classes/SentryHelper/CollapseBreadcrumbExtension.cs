using Microsoft.Win32;
using Sentry;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Net;
using static Hi3Helper.Shared.Region.LauncherConfig;

namespace Hi3Helper.SentryHelper;

// Migrated from main SentryHelper.cs file
public static partial class SentryHelper
{
    #nullable enable
        private static int CpuThreadsTotal => Environment.ProcessorCount;

        private static string CpuName
        {
            get
            {
                try
                {
                    string cpuName;
                    string    env = Environment.GetEnvironmentVariable("PROCESSOR_IDENTIFIER") ?? "Unknown CPU";
                    object? reg =
                        Registry.GetValue(@"HKEY_LOCAL_MACHINE\HARDWARE\DESCRIPTION\SYSTEM\CentralProcessor\0",
                                          "ProcessorNameString", null);
                    if (reg != null)
                    {
                        cpuName = reg.ToString() ?? env;
                        cpuName = cpuName.Trim();
                    }
                    else cpuName = env;

                    return cpuName;
                }
                catch (Exception ex)
                {
                    Logger.LogWriteLine("Failed when trying to get CPU Name!\r\n" + ex, LogType.Error, true);
                    return "Unknown CPU";
                }
            }
        }

        private static List<(string GpuName, string DriverVersion)> GetGpuInfo
        {
            get
            {
                List<(string GpuName, string DriverVersion)> gpuInfoList = [];
                try
                {
                    using RegistryKey? baseKey =
                        Registry.LocalMachine
                                .OpenSubKey(@"SYSTEM\CurrentControlSet\Control\Class\{4d36e968-e325-11ce-bfc1-08002be10318}\");
                    if (baseKey != null)
                    {
                        foreach (string subKeyName in baseKey.GetSubKeyNames())
                        {
                            if (!int.TryParse(subKeyName, out int subKeyInt) || subKeyInt < 0 || subKeyInt > 9999)
                                continue;

                            string? gpuName       = "Unknown GPU";
                            string? driverVersion = "Unknown Driver Version";
                            try
                            {
                                using RegistryKey? subKey = baseKey.OpenSubKey(subKeyName);
                                if (subKey == null) continue;

                                gpuName       = subKey.GetValue("DriverDesc") as string;
                                driverVersion = subKey.GetValue("DriverVersion") as string;
                                if (!string.IsNullOrEmpty(gpuName) && !string.IsNullOrEmpty(driverVersion))
                                {
                                    gpuInfoList.Add((subKeyName + gpuName, driverVersion));
                                }
                            }
                            catch (Exception)
                            {
                                gpuInfoList.Add((subKeyName + gpuName, driverVersion)!);
                                throw;
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Logger.LogWriteLine($"Failed to retrieve GPU info from registry: {ex.Message}", LogType.Error,
                                        true);
                }

                return gpuInfoList;
            }
        }

        private static Breadcrumb? _buildInfo;

        private static Breadcrumb BuildInfo =>
            _buildInfo ??= new Breadcrumb("Build Info", "commit", new Dictionary<string, string>
            {
                { "Branch", AppBuildBranch },
                { "Commit", AppBuildCommit },
                { "Repository", AppBuildRepo },
                { "IsPreview", IsPreview.ToString() }
            }, "BuildInfo");

        private static Breadcrumb? _cpuInfo;

        private static Breadcrumb CpuInfo =>
            _cpuInfo ??= new Breadcrumb("CPU Info", "system.cpu", new Dictionary<string, string>
            {
                { "CPU Name", CpuName },
                { "Total Thread", CpuThreadsTotal.ToString() }
            }, "CPUInfo");

        private static Breadcrumb? _gpuInfo;

        private static Breadcrumb GpuInfo =>
            _gpuInfo ??= new Breadcrumb("GPU Info", "system.gpu",
                                        GetGpuInfo.ToDictionary(item => item.GpuName,
                                                                item => item.DriverVersion),
                                        "GPUInfo");

        private static Breadcrumb GameInfo =>
            new("Current Loaded Game Info", "game", new Dictionary<string, string>
            {
                { "Category", CurrentGameCategory },
                { "Region", CurrentGameRegion },
                { "Installed", CurrentGameInstalled.ToString() },
                { "Updated", CurrentGameUpdated.ToString() },
                { "HasPreload", CurrentGameHasPreload.ToString() },
                { "HasDelta", CurrentGameHasDelta.ToString() },
                { "IsGameFromPlugin", CurrentGameIsPlugin.ToString() },
                { "Location", CurrentGameLocation },
                { "CdnOption", AppCdnOption }
            }, "GameInfo");


        [field: AllowNull, MaybeNull]
        private static Breadcrumb AppConfig
        {
            get
            {
                field ??= new Breadcrumb("Collapse App Config", "app.config", new Dictionary<string, string>
                {
                    { "Proxy", ProxyBreadcrumb.IsUsingProxy ?? "" },
                    { "Waifu2x", GetAppConfigValue("EnableWaifu2X") ? "enabled" : "disabled" },
                    {
                        "HttpCache",
                        AppNetworkCacheEnabled
                            ? "enabled" + (AppNetworkCacheAggressiveModeEnabled ? "+agg" : "")
                            : "disabled"
                    },
                    { "AppThread", $"{AppCurrentDownloadThread},{AppCurrentThread}" },
                    { "SophonThread", $"{GetAppConfigValue("SophonHttpConnInt")},{GetAppConfigValue("SophonCpuThread")}" },
                    { "DownloadPreallocated", (IsUsePreallocatedDownloader) ? "enabled" : "disabled" },
                    {
                        "HttpOpts", 
                        $"{(GetAppConfigValue("IsAllowHttpRedirections")? "Redirect" : "")}" +
                        $"{(GetAppConfigValue("IsAllowHttpCookies") ? "Cookie" : "" )}" +
                        $"{(GetAppConfigValue("IsAllowUntrustedCert") ? "Insecure" : "")}"
                    },
                    { "ExternalDNS", GetAppConfigValue("IsUseExternalDns") ? "enabled" : "disabled" }
                }, "AppConfig");
                return field;
            }
        }
}

#nullable disable
public static class PluginListBreadcrumb
{
    private static readonly Collection<(string Name, string Version, string StdVersion)> List = [];
    
    public static void Add(string name, string version, string stdVersion)
    {
        List.Add((name, version, stdVersion));
        
        SentryHelper.PluginInfo = new Breadcrumb("Plugin Info", "app.plugins",
                                                 List.ToDictionary(
                                                                   item => item.Name,
                                                                   item => $"{item.Version}-{item.StdVersion}"
                                                                  ),
                                                 "PluginInfo");
    }
}

#nullable enable
public static class ProxyBreadcrumb
{
    [field: AllowNull, MaybeNull]
    public static string IsUsingProxy
    {
        get
        {
            field ??= string.Concat(
                                  GetCollapseProxy() ? "cl" : "", 
                                  GetSystemProxy()   ? "+sys" : "" 
                                  );
            return field;
        }
    }
    
    private static bool GetSystemProxy()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Internet Settings");

            return (key?.GetValue("ProxyEnable") as int? ?? 0) == 1;
        }
        catch (Exception ex)
        {
            Logger.LogWriteLine($"[CollapseSentryExtension::GetSystemProxy] Failure when grabbing system proxy settings! returning false...\r\n" +
                                $"{ex}", LogType.Error, true);
            return false;
        }
    }
    
    private static bool GetCollapseProxy()
    {
        var useProxy = GetAppConfigValue("IsUseProxy").ToBool();
        if (!useProxy)
            return false;

        return !string.IsNullOrEmpty(GetAppConfigValue("HttpProxyUrl"));
    }
}