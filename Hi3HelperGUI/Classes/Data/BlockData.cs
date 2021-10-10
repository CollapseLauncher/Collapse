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
        public event EventHandler<ReadingBlockProgressChanged> ProgressChanged;
        public event EventHandler<ReadingBlockProgressCompleted> Completed;
        public BlockData(in MemoryStream i) {
            i.Position = 0;
            util = new XMFUtils(i, XMFFileFormat.Dictionary);
            util.Read();
        }

        public void CheckIntegrity()
        {

        }

        protected virtual void OnProgressChanged(ReadingBlockProgressChanged e)
        {
            var handler = ProgressChanged;
            if (handler != null)
            {
                handler(this, e);
            }
        }

        protected virtual void OnCompleted(ReadingBlockProgressCompleted e)
        {
            var handler = Completed;
            if (handler != null)
            {
                handler(this, e);
            }
        }

        public class ReadingBlockProgressChanged : EventArgs
        {
            public ReadingBlockProgressChanged(long totalRead, long blockSize, double totalSecond)
            {
                BytesRead = totalRead;
                BlockSize = blockSize;
                ReadSpeed = (long)(totalRead / totalSecond);
            }
            public long CurrentRead { get; set; }
            public long BytesRead { get; private set; }
            public long BlockSize { get; private set; }
            public long ReadSpeed { get; private set; }
        }

        public class ReadingBlockProgressCompleted : EventArgs
        {
            public bool ReadCompleted { get; set; }
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
