using System;
using System.Diagnostics;
using System.IO;

namespace CollapseLauncher
{
    public class TakeOwnership
    {
        public void StartTakingOwnership(string target)
        {
            if (target == null) throw new ArgumentNullException(nameof(target));
            // ReSharper disable once LocalizableElement
            Console.WriteLine("Trying to append ownership of the folder. Please do not close this console window!");

            if (!Directory.Exists(target))
                Directory.CreateDirectory(target);

            Process takeOwner = new Process
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
