using Microsoft.Win32;

namespace CollapseLauncher.GameSettings
{
    internal static class Statics
    {
#nullable enable
        internal const string RegistryRootPath = @"Software\miHoYo";
        internal static string? RegistryPath;
        internal static RegistryKey? RegistryRoot;
#nullable disable
    }
}
