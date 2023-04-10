using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Hi3Helper.Preset
{
    public interface IAssetIndexSummary
    {
        string PrintSummary();
        long GetAssetSize();
    }
}
