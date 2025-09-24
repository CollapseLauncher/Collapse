using Hi3Helper;
using Microsoft.Win32;
using System;
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
        try
        {
            if (key == null)
            {
                LogWriteLine($"[RegistryExtension::TryGetValue] The provided RegistryKey is null. Cannot retrieve value {name}.",
                             LogType.Error, true);
                return null;
            }
            
            var value = key.GetValue(name, defaultValue);

            if (value == defaultValue)
            {
                throw new
                    NullReferenceException($"Value {name} returns null or does not exist in registry key {key.Name}.");
            }
            
            return value;
        }
        catch (Exception ex)
        {
            LogWriteLine($"[RegistryExtension::TryGetValue] Failed to get registry value {name} from {key?.Name ?? "null"} due to {ex.Message}" +
                         $"\r\n\t Attempting to reload the registry key after running function {reloadFunction.Method.Name}",
                         LogType.Error, true);
            
            var reloadedKey = reloadFunction();
            if (reloadedKey == null)
            {
                LogWriteLine($"[RegistryExtension::TryGetValue] Reload function {reloadFunction.Method.Name} returned null. Cannot retrieve value {name}.", LogType.Error, true);
                return defaultValue;
            }
            
            var value = reloadedKey.GetValue(name, defaultValue);
            if (value != defaultValue) return reloadedKey.GetValue(name, defaultValue);
            
            LogWriteLine($"[RegistryExtension::TryGetValue] Value {name} still returns null or does not exist in reloaded registry key {reloadedKey.Name}.", LogType.Error, true);
            return defaultValue;
        }
    }
}