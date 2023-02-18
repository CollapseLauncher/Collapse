using Microsoft.Win32;

namespace CollapseLauncher.GameSettings
{
    internal static class Statics
    {
#nullable enable
        internal static string RegistryRootPath { get; set; } = @"Software\miHoYo";
        internal static string? RegistryPath;
        internal static RegistryKey? RegistryRoot;
#nullable disable
    }
}
