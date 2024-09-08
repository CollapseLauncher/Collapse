using Hi3Helper.Shared.Region;
using System;
using System.Runtime.CompilerServices;

namespace CollapseLauncher.Extension
{
    internal static class NumberExtensions
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static double ClampLimitedSpeedNumber(this double speed)
            => LauncherConfig.DownloadSpeedLimitCached > 0 ?
            Math.Min(LauncherConfig.DownloadSpeedLimitCached, speed) :
            speed;
    }
}
