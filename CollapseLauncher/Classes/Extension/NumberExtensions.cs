using Hi3Helper.Shared.Region;
using System;
using System.Runtime.CompilerServices;
#pragma warning disable IDE0130

namespace CollapseLauncher.Extension
{
    internal static class NumberExtensions
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static double ClampLimitedSpeedNumber(this double speed)
        {
            return speed;
            /*
            return LauncherConfig.DownloadSpeedLimit > 0 ?
                Math.Min(LauncherConfig.DownloadSpeedLimit, speed) :
                speed;
            */
        }
    }
}
