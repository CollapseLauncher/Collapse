using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.IO;
using System.Windows;
using Hi3HelperGUI.Preset;
using Hi3HelperGUI.Data;

using static Hi3HelperGUI.Logger;
using static Hi3HelperGUI.ConverterTools;

namespace Hi3HelperGUI
{
    public partial class MainWindow
    {
        public void BlockCheckStart(object sender, RoutedEventArgs e) => CheckBlockData();

        public async void CheckBlockData()
        {
            await Task.Run(() =>
            {
                LogWriteLine($"Bruh");
            });
        }
    }
}
