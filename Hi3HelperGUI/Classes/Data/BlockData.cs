using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Hi3HelperGUI.Preset;

using static Hi3HelperGUI.Logger;

namespace Hi3HelperGUI.Data
{
    class BlockData
    {
        public BlockData(PresetConfigClasses i)
        {
            string bigDataURL = ConfigStore.GetMirrorAddressByIndex(i, ConfigStore.DataType.Bigfile);
            string dictionaryURL = ConfigStore.GetMirrorAddressByIndex(i, ConfigStore.DataType.BlockDictionaryAddress);
            LogWriteLine(bigDataURL);
            LogWriteLine(dictionaryURL);
        }
    }
}
