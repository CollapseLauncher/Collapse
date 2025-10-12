// ReSharper disable InconsistentNaming
// ReSharper disable UnusedMember.Global

using System;

namespace CollapseLauncher
{
    internal enum RepairAssetType
    {
        Generic,
        Block,
        BlockUpdate,
        Audio,
        AudioUpdate,
        Video,
        Chunk,

        Unused
    }

    [Flags]
    internal enum CacheAssetType
    {
        Data       = 0b_00000000_00000001,
        Event      = 0b_00000000_00000010,
        AI         = 0b_00000000_00000100,
        Unused     = 0b_00000000_00001000,
        Dispatcher = 0b_00000000_00010000,
        Gateway    = 0b_00000000_00100000,
        General    = 0b_00000000_01000000, // Additional for HSR
        IFix       = 0b_00000000_10000000, // Additional for HSR
        DesignData = 0b_00000001_00000000, // Additional for HSR
        Lua        = 0b_00000010_00000000  // Additional for HSR
    }

    internal enum CacheAssetStatus
    {
        New,
        Obsolete,
        Unused
    }

    internal enum GameInstallPackageType
    {
        General,
        Audio,
        Plugin,
        Utilities
    }

    internal enum PostInstallBehaviour
    {
        Nothing,
        StartGame,
        Hibernate,
        Restart,
        Shutdown
    }
}
