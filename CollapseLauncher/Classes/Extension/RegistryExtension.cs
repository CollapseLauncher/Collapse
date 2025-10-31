using Hi3Helper;
using Microsoft.Win32;
using System;
using System.Diagnostics.CodeAnalysis;
using static Hi3Helper.Logger;

namespace CollapseLauncher.Extension;

public static class RegistryExtension
{
#nullable enable
    /// <summary>
    /// Attempts to retrieve a value from the specified registry key. If an exception occurs,
    /// the provided reload function is invoked to reload the registry key, and the value is
    /// retrieved from the reloaded key.
    /// </summary>
    /// <param name="registryKey">The <see cref="RegistryKey"/> to retrieve the value from. This string is not case-sensitive.</param>
    /// <param name="keyName">The name of the value to retrieve.</param>
    /// <param name="result">The return value of the specified key.</param>
    /// <param name="defaultValue">The value to return if name does not exist.</param>
    /// <param name="getReloadedRegistry">
    /// A function that reloads the registry key in case of an error. This function should return
    /// a new <see cref="RegistryKey"/> instance or null if the reload fails.
    /// </param>
    /// <returns>
    /// The value associated with the specified name, or null if the value does not exist or
    /// the reload function fails.
    /// </returns>
    /// <remarks>
    /// This method logs an error message if an exception occurs while attempting to retrieve
    /// the value from the registry key.
    /// </remarks>
    public static bool TryGetValue(
        this RegistryKey?               registryKey,
        string                          keyName,
        [NotNullWhen(true)] out object? result,
        object?                         defaultValue        = null,
        Func<RegistryKey?>?             getReloadedRegistry = null)
    {
        result      =   defaultValue;
        registryKey ??= getReloadedRegistry?.Invoke(); // Reload registry earlier if null and if the registry reload function is provided

        if (registryKey == null)
        {
            LogWriteLine($"[RegistryExtension::TryGetValue] The provided RegistryKey is null. Cannot retrieve value {keyName}.",
                         LogType.Error, true);

            return false;
        }

        try
        {
        TryGetValue:
            result = registryKey.GetValue(keyName, defaultValue);
            bool isValueFound = result != null;
            result ??= defaultValue;

            if (isValueFound || getReloadedRegistry == null)
            {
                return isValueFound;
            }

            LogWriteLine($"[RegistryExtension::TryGetValue] Value {keyName} not found in {registryKey.Name}. Attempting to reload the registry key...",
                         LogType.Warning,
                         true);

            registryKey = getReloadedRegistry();
            if (registryKey != null)
            {
                goto TryGetValue;
            }

            return isValueFound;
        }
        catch (Exception ex) when (getReloadedRegistry != null)
        {
            LogWriteLine($"[RegistryExtension::TryGetValue] Failed to get registry value {keyName} from {registryKey.Name} due to {ex.Message}" +
                         $"\r\n\t Attempting to reload the registry key after running function...",
                         LogType.Error,
                         true);

            registryKey = getReloadedRegistry();
            return registryKey.TryGetValue(keyName,
                                           out result,
                                           defaultValue);
        }
        catch (Exception ex) when (getReloadedRegistry == null)
        {
            LogWriteLine($"[RegistryExtension::TryGetValue] Failed to get registry value {keyName} from {registryKey.Name} due to {ex.Message}",
                         LogType.Error,
                         true);
        }

        return false;
    }

    /// <summary>
    /// Attempts to retrieve a value from the specified registry key. If an exception occurs,
    /// the provided reload function is invoked to reload the registry key, and the value is
    /// retrieved from the reloaded key.
    /// </summary>
    /// <param name="key">The <see cref="RegistryKey"/> to retrieve the value from. This string is not case-sensitive.</param>
    /// <param name="name">The name of the value to retrieve.</param>
    /// <param name="defaultValue">The value to return if name does not exist.</param>
    /// <param name="reloadFunction">
    /// A function that reloads the registry key in case of an error. This function should return
    /// a new <see cref="RegistryKey"/> instance or null if the reload fails.
    /// </param>
    /// <returns>
    /// The value associated with the specified name, or null if the value does not exist or
    /// the reload function fails.
    /// </returns>
    /// <remarks>
    /// This method logs an error message if an exception occurs while attempting to retrieve
    /// the value from the registry key.
    /// </remarks>
    public static object? TryGetValue(this RegistryKey? key, string name, object? defaultValue, Func<RegistryKey?> reloadFunction)
    {
        if (key.TryGetValue(name, out object? result, defaultValue, reloadFunction))
        {
            return result;
        }

        result ??= defaultValue;
        return result;
    }
}