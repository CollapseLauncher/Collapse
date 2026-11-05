using Hi3Helper.Log;
using System;
using System.Diagnostics;
using System.IO;

namespace CollapseLauncher
{
    public class TakeOwnership
    {
        public static void StartTakingOwnership(string target)
        {
            ArgumentException.ThrowIfNullOrEmpty(target);

            // ReSharper disable once LocalizableElement
            Logger.WriteLine("Trying to append ownership of the folder. Please do not close this console window!", LogType.Warning, true);

            if (!Directory.Exists(target))
                Directory.CreateDirectory(target);

            Process takeOwner = new()
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "cmd.exe",
                    UseShellExecute = false,
                    Arguments = $"/c icacls \"{target}\" /T /Q /C /RESET"
                }
            };

            takeOwner.Start();
            takeOwner.WaitForExit();

            takeOwner = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "cmd.exe",
                    UseShellExecute = false,
                    Arguments = $"/c takeown /f \"{target}\" /r /d y"
                }
            };

            takeOwner.Start();
            takeOwner.WaitForExit();
        }
    }
}
