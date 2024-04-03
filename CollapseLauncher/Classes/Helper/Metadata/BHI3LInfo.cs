using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CollapseLauncher.Helper.Metadata
{
    internal class BHI3LInfo
    {
        public BHI3LInfo_GameInfo game_info { get; set; }
    }

    internal class BHI3LInfo_GameInfo
    {
        public string version { get; set; }
        public string install_path { get; set; }
        public bool installed { get; set; }
    }
}
