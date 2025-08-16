using Hi3Helper;
using System;
using System.Linq;
using System.Diagnostics;
using System.Reflection;
using static Hi3Helper.Logger;

namespace CollapseLauncher.Helper;

public static class AssemblyAttributes
{
    private static readonly Assembly Assembly = typeof(MainEntryPoint).Assembly;
    
    /// <summary>
    /// Retrieves the value of a specified assembly metadata attribute.
    /// </summary>
    /// <param name="attributeName">The name of the assembly metadata attribute to retrieve.</param>
    /// <returns>
    /// The value of the specified assembly metadata attribute if found; 
    /// otherwise, returns null if the attribute is not present or an error occurs.
    /// </returns>
    private static string GetAttributeValue(string attributeName)
    {
        try
        {
            var attr = Assembly.GetCustomAttributes<AssemblyMetadataAttribute>()
                                .FirstOrDefault(a =>
                                                    a.Key.Equals(attributeName,
                                                                 StringComparison.OrdinalIgnoreCase))?.Value;
            return attr;
        }
        catch (Exception ex)
        {
            LogWriteLine($"[AssemblyAttributes] Error retrieving assembly attribute {attributeName}\r\n{ex}", LogType.Error, true);
            return null;
        }
    }
    
    public static string GetVersion()
    {
        try
        {
            return Assembly.GetName().Version?.ToString() ?? "Unknown Version";
        }
        catch (Exception ex)
        {
            LogWriteLine($"[AssemblyAttributes] Error retrieving assembly version\r\n{ex}", LogType.Error, true);
            return "Unknown Version";
        }
    }
    
    public static string GetProductVersion()
    {
        try
        {
            return FileVersionInfo.GetVersionInfo(Assembly.Location).ProductVersion ?? "Unknown Product Version";
        }
        catch (Exception ex)
        {
            LogWriteLine($"[AssemblyAttributes] Error retrieving product version\r\n{ex}", LogType.Error, true);
            return "Unknown Product Version";
        }
    }
    
    public static string TryGetCommitHash()
    {
        try
        {
            return GetAttributeValue("GitCommitHash") ?? "Unknown Commit Hash";
        }
        catch (Exception ex)
        {
            LogWriteLine($"[AssemblyAttributes] Error retrieving commit hash\r\n{ex}", LogType.Error, true);
            return "Unknown Commit Hash";
        }
    }
}


