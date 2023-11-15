using CollapseLauncher.FileDialogCOM;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace CollapseLauncher.FileDialogCOM
{
    // Reference:
    // https://www.dotnetframework.org/default.aspx/4@0/4@0/DEVDIV_TFS/Dev10/Releases/RTMRel/ndp/fx/src/WinForms/Managed/System/WinForms/FileDialog_Vista_Interop@cs/1305376/FileDialog_Vista_Interop@cs
    public static class FileDialogNative
    {
        private static IntPtr parentHandler = IntPtr.Zero;
        public static void InitHandlerPointer(IntPtr handle) => parentHandler = handle;

        public static async Task<List<string>> GetMultiFilePicker(Dictionary<string, string> FileTypeFilter = null) => await Task.Run(() =>
        {
            IFileOpenDialog dialog = null;
            IShellItemArray resShell;

            try
            {
                dialog = new NativeFileOpenDialog();
                dialog.SetOptions(FOS.FOS_NOREADONLYRETURN | FOS.FOS_DONTADDTORECENT | FOS.FOS_ALLOWMULTISELECT);
                SetFileTypeFiler(ref dialog, FileTypeFilter);

                if (dialog.Show(parentHandler) < 0) return null;

                dialog.GetResults(out resShell);
                return GetIShellItemArray(dialog, resShell);
            }
            catch (COMException)
            {
                return null;
            }
            finally
            {
                if (dialog != null) Marshal.FinalReleaseComObject(dialog);
            }
        }).ConfigureAwait(false);

        public static async Task<string> GetFilePicker(Dictionary<string, string> FileTypeFilter = null, string title = null) => await Task.Run(() =>
        {
            IFileOpenDialog dialog = null;
            IShellItem resShell;
            string result;

            try
            {
                dialog = new NativeFileOpenDialog();
                if (title != null)
                {
                    dialog.SetTitle(title);
                }
                SetFileTypeFiler(ref dialog, FileTypeFilter);
                dialog.SetOptions(FOS.FOS_NOREADONLYRETURN | FOS.FOS_DONTADDTORECENT);

                if (dialog.Show(parentHandler) < 0) return null;

                dialog.GetResult(out resShell);
                resShell.GetDisplayName(SIGDN.SIGDN_FILESYSPATH, out result);
                return result;
            }
            catch (COMException)
            {
                return null;
            }
            finally
            {
                if (dialog != null) Marshal.FinalReleaseComObject(dialog);
            }
        }).ConfigureAwait(false);

        public static async Task<string> GetFileSavePicker(Dictionary<string, string> FileTypeFilter = null, string title = null) => await Task.Run(() =>
        {
            IFileSaveDialog dialog = null;
            IShellItem resShell;
            string result;

            try
            {
                dialog = new NativeFileSaveDialog();
                if (title != null)
                {
                    dialog.SetTitle(title);
                }
                SetFileTypeSaveFiler(ref dialog, FileTypeFilter);
                dialog.SetOptions(FOS.FOS_NOREADONLYRETURN | FOS.FOS_DONTADDTORECENT);

                if (dialog.Show(parentHandler) < 0) return null;

                dialog.GetResult(out resShell);
                resShell.GetDisplayName(SIGDN.SIGDN_FILESYSPATH, out result);
                return result;
            }
            catch (COMException)
            {
                return null;
            }
            finally
            {
                if (dialog != null) Marshal.FinalReleaseComObject(dialog);
            }
        }).ConfigureAwait(false);

        public static async Task<List<string>> GetMultiFolderPicker() => await Task.Run(() =>
        {
            IFileOpenDialog dialog = null;
            IShellItemArray resShell;

            try
            {
                dialog = new NativeFileOpenDialog();
                dialog.SetOptions(FOS.FOS_NOREADONLYRETURN | FOS.FOS_ALLOWMULTISELECT | FOS.FOS_DONTADDTORECENT | FOS.FOS_PICKFOLDERS);

                if (dialog.Show(parentHandler) < 0) return null;

                dialog.GetResults(out resShell);
                return GetIShellItemArray(dialog, resShell);
            }
            catch (COMException)
            {
                return null;
            }
            finally
            {
                if (dialog != null) Marshal.FinalReleaseComObject(dialog);
            }
        }).ConfigureAwait(false);

        public static async Task<string> GetFolderPicker() => await Task.Run(() =>
        {
            IFileDialog dialog = null;
            IShellItem resShell;
            string result;

            try
            {
                dialog = new NativeFileOpenDialog();
                dialog.SetOptions(FOS.FOS_NOREADONLYRETURN | FOS.FOS_DONTADDTORECENT | FOS.FOS_PICKFOLDERS);

                if (dialog.Show(parentHandler) < 0) return null;

                dialog.GetFolder(out resShell);
                resShell.GetDisplayName(SIGDN.SIGDN_DESKTOPABSOLUTEPARSING, out result);
                return result;
            }
            catch (COMException)
            {
                return null;
            }
            finally
            {
                if (dialog != null) Marshal.FinalReleaseComObject(dialog);
            }
        }).ConfigureAwait(false);

        private static void SetFileTypeSaveFiler(ref IFileSaveDialog dialog, Dictionary<string, string> FileTypeFilter)
        {
            List<COMDLG_FILTERSPEC> fileTypes = new List<COMDLG_FILTERSPEC>();

            if (FileTypeFilter != null)
            {
                foreach (KeyValuePair<string, string> entry in FileTypeFilter)
                    fileTypes.Add(new COMDLG_FILTERSPEC { pszName = entry.Key, pszSpec = entry.Value });

                dialog.SetFileTypes((uint)fileTypes.Count, fileTypes.ToArray());
            }
        }

        private static void SetFileTypeFiler(ref IFileOpenDialog dialog, Dictionary<string, string> FileTypeFilter)
        {
            List<COMDLG_FILTERSPEC> fileTypes = new List<COMDLG_FILTERSPEC>();

            if (FileTypeFilter != null)
            {
                foreach (KeyValuePair<string, string> entry in FileTypeFilter)
                    fileTypes.Add(new COMDLG_FILTERSPEC { pszName = entry.Key, pszSpec = entry.Value });

                dialog.SetFileTypes((uint)fileTypes.Count, fileTypes.ToArray());
            }
        }

        private static List<string> GetIShellItemArray(in IFileOpenDialog dialog, in IShellItemArray itemArray)
        {
            List<string> results = new List<string>();
            IShellItem item;
            uint fileCount;
            string _res;

            itemArray.GetCount(out fileCount);
            for (uint i = 0; i < fileCount; i++)
            {
                itemArray.GetItemAt(i, out item);
                item.GetDisplayName(SIGDN.SIGDN_FILESYSPATH, out _res);
                results.Add(_res);
            }

            return results;
        }
    }
}
