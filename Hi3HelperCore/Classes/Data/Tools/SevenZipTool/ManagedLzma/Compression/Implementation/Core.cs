using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ManagedLzma.LZMA.Master
{
    internal static partial class LZMA
    {
        /* #define SHOW_STAT */
        /* #define SHOW_STAT2 */
        /* #define SHOW_DEBUG_INFO */
        /* #define PROTOTYPE */

        [System.Diagnostics.Conditional("SHOW_DEBUG_INFO")]
        internal static void DebugPrint(string format, params object[] args)
        {
#if BUILD_TESTING
            System.Diagnostics.Debug.Write(String.Format(format, args));
#endif
        }

        internal static void Print(string format, params object[] args)
        {
#if BUILD_TESTING
            System.Diagnostics.Debug.Write(String.Format(format, args));
#endif
        }
    }
}
