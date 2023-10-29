using Hi3Helper.Shared.ClassStruct;
using System;
using System.Collections.Generic;

namespace CollapseLauncher.Interfaces
{
    internal interface IRepairAssetIndex : IDisposable
    {
        List<FilePropertiesRemote> GetAssetIndex();
    }
}
