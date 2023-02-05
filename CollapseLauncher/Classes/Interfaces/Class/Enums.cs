namespace CollapseLauncher
{
    internal enum RepairAssetType
    {
        General,
        Block,
        Audio,
        Video,
        Chunk,
        Unused
    }

    internal enum CacheAssetType
    {
        Data,
        Event,
        AI,
        Unused,
    }

    internal enum CacheAssetStatus
    {
        New,
        Obsolete,
        Unused
    }
}
