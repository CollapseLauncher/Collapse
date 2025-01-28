using CollapseLauncher.Interfaces;

namespace CollapseLauncher.GameManagement.Versioning
{
    internal static class GameVersionExtension
    {
        /// <summary>
        /// Casting the IGameVersion as its origin or another version class.
        /// </summary>
        /// <typeparam name="TCast">The type of version class to cast</typeparam>
        /// <returns>The version class to get casted</returns>
        internal static TCast CastAs<TCast>(this IGameVersion versionCheck)
            where TCast : GameVersionBase, IGameVersion => (TCast)versionCheck;
    }
}
