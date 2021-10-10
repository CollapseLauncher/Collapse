using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Runtime.InteropServices;
using System.Reflection;
using System.IO;
using Hi3HelperGUI;
using Hi3HelperGUI.Preset;

using static Hi3HelperGUI.Logger;

namespace Hi3HelperGUI.Data
{
    public class BlockData
    {
        XMFUtils util;
        public BlockData(in MemoryStream i) {
            i.Position = 0;
            util = new XMFUtils(i, XMFFileFormat.Dictionary);
            util.Read();
        }
        
        /*
        public BlockData(PresetConfigClasses i)
        {
            string bigDataURL = ConfigStore.GetMirrorAddressByIndex(i, ConfigStore.DataType.Bigfile);
            string dictionaryURL = ConfigStore.GetMirrorAddressByIndex(i, ConfigStore.DataType.BlockDictionaryAddress);
            LogWriteLine(bigDataURL);
            LogWriteLine(dictionaryURL);
        }
        */
    }
}
