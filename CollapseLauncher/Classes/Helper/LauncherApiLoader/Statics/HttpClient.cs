using CollapseLauncher.Helper.Metadata;
using Hi3Helper;
using Microsoft.Win32;
using System;
using System.Net;
using System.Net.Http;

// ReSharper disable IdentifierTypo
// ReSharper disable once CheckNamespace
// ReSharper disable PartialTypeWithSinglePart
// ReSharper disable InconsistentNaming
// ReSharper disable StringLiteralTypo
// ReSharper disable CommentTypo

namespace CollapseLauncher.Helper.LauncherApiLoader.Statics
{
    public static class HttpClientWrapper
    {
        public static HttpClient GeneralHttpClientFactory(PresetConfig presetConfig)
        {
            // Set the HttpClientBuilder for HoYoPlay's own General API.
            HttpClientBuilder apiGeneralHttpBuilder = new HttpClientBuilder()
                                                     .UseLauncherConfig()
                                                     .AllowUntrustedCert()
                                                     .SetHttpVersion(HttpVersion.Version30)
                                                     .SetAllowedDecompression()
                                                     .AddHeader("x-rpc-device_id", GetDeviceId(presetConfig));

            // If the metadata has user-agent defined, set the resource's HttpClient user-agent
            if (!string.IsNullOrEmpty(presetConfig.ApiGeneralUserAgent))
            {
                apiGeneralHttpBuilder.SetUserAgent(presetConfig.ApiGeneralUserAgent);
            }

            // Add other API general and resource headers from the metadata configuration
            presetConfig.AddApiGeneralAdditionalHeaders((key, value) => apiGeneralHttpBuilder.AddHeader(key, value));
            
            // Create HttpClient instances for both General and Resource APIs.
            return apiGeneralHttpBuilder.Create();
        }

        public static HttpClient ResourceHttpClientFactory(PresetConfig presetConfig)
        {
            // Set the HttpClientBuilder for HoYoPlay's own Resource API.
            HttpClientBuilder apiResourceHttpBuilder = new HttpClientBuilder()
                                                      .UseLauncherConfig()
                                                      .AllowUntrustedCert()
                                                      .SetHttpVersion(HttpVersion.Version30)
                                                      .SetAllowedDecompression(DecompressionMethods.None)
                                                      .AddHeader("x-rpc-device_id", GetDeviceId(presetConfig));

            // If the metadata has user-agent defined, set the resource's HttpClient user-agent
            if (!string.IsNullOrEmpty(presetConfig.ApiResourceUserAgent))
            {
                apiResourceHttpBuilder.SetUserAgent(string.Format(presetConfig.ApiResourceUserAgent, InnerLauncherConfig.m_isWindows11 ? "11" : "10"));
            }

            // Add other API general and resource headers from the metadata configuration
            presetConfig.AddApiResourceAdditionalHeaders((key, value) => apiResourceHttpBuilder.AddHeader(key, value));

            // Create HttpClient instances for both General and Resource APIs.
            return apiResourceHttpBuilder.Create();
        }
        
        private static string GetDeviceId(PresetConfig preset)
        {
            // Determine if the client is a mainland client based on the zone name
            bool isMainlandClient = (preset.ZoneName?.Equals("Mainland China") ?? false) || (preset.ZoneName?.Equals("Bilibili") ?? false);

            // Set the publisher name based on the client type
            string publisherName = isMainlandClient ? "miHoYo" : "Cognosphere";
            // Define the registry root path for the publisher
            string registryRootPath = $@"Software\{publisherName}\HYP";

            // Open the registry key for the root path
            RegistryKey? rootRegistryKey = Registry.CurrentUser.OpenSubKey(registryRootPath, true);
            // Find or create the HYP device ID
            string hypDeviceId = FindOrCreateHYPDeviceId(rootRegistryKey, isMainlandClient, registryRootPath);
            return hypDeviceId;
        }
        
                private static string FindOrCreateHYPDeviceId(RegistryKey? rootRegistryKey, bool isMainlandClient, string registryRootPath)
        {
            // Define default version keys for mainland and global clients
            const string HYPVerDefaultCN = "1_1";
            const string HYPVerDefaultGlb = "1_0";

            // Use the root registry key or create it if it doesn't exist
            using (rootRegistryKey ??= Registry.CurrentUser.CreateSubKey(registryRootPath, true))
            {
                // Get the subkey names under the root registry key
                string[] subKeyNames = rootRegistryKey.GetSubKeyNames();
                foreach (string subKeyNameString in subKeyNames)
                {
                    // Open each subkey and check for the HYPDeviceId value
                    using RegistryKey? subKeyNameKey = rootRegistryKey.OpenSubKey(subKeyNameString, true);
                    if (subKeyNameKey == null)
                    {
                        continue;
                    }

                    // Get the current HYP device ID from the subkey
                    string? currentHypDeviceId = (string?)subKeyNameKey.GetValue("HYPDeviceId", null);
                    if (string.IsNullOrEmpty(currentHypDeviceId))
                    {
                        continue;
                    }

                    // Return the current HYP device ID if found
                    return currentHypDeviceId;
                }

                // Open or create the subkey for the default version based on the client type
                using RegistryKey subRegistryKey = rootRegistryKey.OpenSubKey(isMainlandClient ? HYPVerDefaultCN : HYPVerDefaultGlb, true)
                    ?? rootRegistryKey.CreateSubKey(isMainlandClient ? HYPVerDefaultCN : HYPVerDefaultGlb, true);

                // Generate a new HYP device ID
                string newHypDeviceId = CreateNewDeviceId();
                // Set the new HYP device ID in the subkey
                subRegistryKey.SetValue("HYPDeviceId", newHypDeviceId, RegistryValueKind.String);

                return newHypDeviceId;
            }
        }

        private static string CreateNewDeviceId()
        {
            string guid;
            try
            {
                // Define the registry key path for cryptography settings
                const string regKeyCryptography = @"SOFTWARE\Microsoft\Cryptography";

                // Open the registry key for reading
                using var rootRegistryKey = Registry.LocalMachine.OpenSubKey(regKeyCryptography, true);
                // Retrieve the MachineGuid value from the registry, or generate a new GUID if it doesn't exist
                guid = ((string?)rootRegistryKey?.GetValue("MachineGuid", null) ??
                               Guid.NewGuid().ToString()).Replace("-", string.Empty);
            }
            catch (Exception ex)
            {
                Logger.LogWriteLine($"[HypApiLoader::CreateNewDeviceId] Failed to retrieve MachineGuid from registry, using a dummy GUID instead" +
                                    $"\r\n{ex}", LogType.Error, true);
                guid = Guid.NewGuid().ToString().Replace("-", string.Empty);
            }

            // Append the current Unix timestamp in milliseconds to the GUID
            return guid + DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        }
    }
}