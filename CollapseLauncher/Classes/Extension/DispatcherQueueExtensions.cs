using Microsoft.UI.Dispatching;
using System;

namespace CollapseLauncher.Extension
{
    public static class DispatcherQueueExtensions
    {
        public static bool HasThreadAccessSafe(this DispatcherQueue queue)
        {
            if (queue == null) return false;

            try
            {
                return queue.HasThreadAccess;
            }
            catch (ObjectDisposedException)
            {
                return false; // Return false if an exception occurs
            }
        }
    }
}