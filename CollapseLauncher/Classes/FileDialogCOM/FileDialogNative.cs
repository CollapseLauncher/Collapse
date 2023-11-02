using Hi3Helper;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Windows.Win32;

namespace CollapseLauncher.FileDialogCOM
{
    /*
     * Reference:
     * https://www.dotnetframework.org/default.aspx/4@0/4@0/DEVDIV_TFS/Dev10/Releases/RTMRel/ndp/fx/src/WinForms/Managed/System/WinForms/FileDialog_Vista_Interop@cs/1305376/FileDialog_Vista_Interop@cs
     * 
     * UPDATE: 2023-11-01
     * This code has been modified to support ILTrimming and Native AOT
     * by using Source-generated COM Wrappers on .NET 8.
     * 
     * Please refer to this link for more information:
     * https://learn.microsoft.com/en-us/dotnet/standard/native-interop/comwrappers-source-generation
     */
    public static class FileDialogNative
    {
        private static IntPtr parentHandler = IntPtr.Zero;
        public static void InitHandlerPointer(IntPtr handle) => parentHandler = handle;

        public static async Task<string[]> GetMultiFilePicker(Dictionary<string, string> FileTypeFilter = null) => await Task.Run(() =>
        {
            PInvoke.CoCreateInstance(
                new Guid(CLSIDGuid.FileOpenDialog),
                null,
                Windows.Win32.System.Com.CLSCTX.CLSCTX_INPROC_SERVER,
                out IFileOpenDialog dialog).ThrowOnFailure();

            IShellItemArray resShell;

            try
            {
                dialog.SetOptions(FOS.FOS_NOREADONLYRETURN | FOS.FOS_DONTADDTORECENT | FOS.FOS_ALLOWMULTISELECT);
                SetFileTypeFilter(dialog, FileTypeFilter);

                if (dialog.Show(parentHandler) < 0) return null;

                dialog.GetResults(out resShell);
                return GetIShellItemArray(resShell);
            }
            catch (COMException)
            {
                return null;
            }
#if !NET8_0_OR_GREATER
            finally
            {
                if (dialog != null) Marshal.FinalReleaseComObject(dialog);
            }
#endif
        }).ConfigureAwait(false);

        public static async Task<string> GetFilePicker(Dictionary<string, string> FileTypeFilter = null, string title = null) => await Task.Run(() =>
        {
            PInvoke.CoCreateInstance(
                new Guid(CLSIDGuid.FileOpenDialog),
                null,
                Windows.Win32.System.Com.CLSCTX.CLSCTX_INPROC_SERVER,
                out IFileOpenDialog dialog).ThrowOnFailure();

            IShellItem resShell;
            IntPtr titlePtr = IntPtr.Zero;

            try
            {
                if (title != null)
                {
                    dialog.SetTitle(titlePtr = UnicodeStringToCOMPtr(title));
                }
                SetFileTypeFilter(dialog, FileTypeFilter);
                dialog.SetOptions(FOS.FOS_NOREADONLYRETURN | FOS.FOS_DONTADDTORECENT);

                if (dialog.Show(parentHandler) < 0) return null;

                dialog.GetResult(out resShell);
                resShell.GetDisplayName(SIGDN.SIGDN_FILESYSPATH, out IntPtr resultPtr);
                return COMPtrToUnicodeString(resultPtr);
            }
            catch (COMException ex)
            {
                Logger.LogWriteLine($"COM Exception: {ex}", LogType.Error, true);
                return null;
            }
            finally
            {
                if (titlePtr != IntPtr.Zero) Marshal.FreeCoTaskMem(titlePtr);
            }
        }).ConfigureAwait(false);

        public static async Task<string> GetFileSavePicker(Dictionary<string, string> FileTypeFilter = null, string title = null) => await Task.Run(() =>
        {
            PInvoke.CoCreateInstance(
                new Guid(CLSIDGuid.FileSaveDialog),
                null,
                Windows.Win32.System.Com.CLSCTX.CLSCTX_INPROC_SERVER,
                out IFileSaveDialog dialog).ThrowOnFailure();

            IShellItem resShell;
            IntPtr titlePtr = IntPtr.Zero;

            try
            {
                if (title != null)
                {
                    dialog.SetTitle(titlePtr = UnicodeStringToCOMPtr(title));
                }
                SetFileTypeFilter(dialog, FileTypeFilter);
                dialog.SetOptions(FOS.FOS_NOREADONLYRETURN | FOS.FOS_DONTADDTORECENT);

                if (dialog.Show(parentHandler) < 0) return null;

                dialog.GetResult(out resShell);
                resShell.GetDisplayName(SIGDN.SIGDN_FILESYSPATH, out IntPtr resultPtr);
                return COMPtrToUnicodeString(resultPtr);
            }
            catch (COMException)
            {
                return null;
            }
            finally
            {
#if !NET8_0_OR_GREATER
                if (dialog != null) Marshal.FinalReleaseComObject(dialog);
#endif
                if (titlePtr != IntPtr.Zero) Marshal.FreeCoTaskMem(titlePtr);
            }
        }).ConfigureAwait(false);

        public static async Task<string[]> GetMultiFolderPicker() => await Task.Run(() =>
        {
            PInvoke.CoCreateInstance(
                new Guid(CLSIDGuid.FileOpenDialog),
                null,
                Windows.Win32.System.Com.CLSCTX.CLSCTX_INPROC_SERVER,
                out IFileOpenDialog dialog).ThrowOnFailure();

            IShellItemArray resShell;

            try
            {
                dialog.SetOptions(FOS.FOS_NOREADONLYRETURN | FOS.FOS_ALLOWMULTISELECT | FOS.FOS_DONTADDTORECENT | FOS.FOS_PICKFOLDERS);

                if (dialog.Show(parentHandler) < 0) return null;

                dialog.GetResults(out resShell);
                return GetIShellItemArray(resShell);
            }
            catch (COMException)
            {
                return null;
            }
#if !NET8_0_OR_GREATER
            finally
            {
                if (dialog != null) Marshal.FinalReleaseComObject(dialog);
            }
#endif
        }).ConfigureAwait(false);

        public static async Task<string> GetFolderPicker() => await Task.Run(() =>
        {
            PInvoke.CoCreateInstance(
                new Guid(CLSIDGuid.FileOpenDialog),
                null,
                Windows.Win32.System.Com.CLSCTX.CLSCTX_INPROC_SERVER,
                out IFileOpenDialog dialog).ThrowOnFailure();

            IShellItem resShell;

            try
            {
                dialog.SetOptions(FOS.FOS_NOREADONLYRETURN | FOS.FOS_DONTADDTORECENT | FOS.FOS_PICKFOLDERS);

                if (dialog.Show(parentHandler) < 0) return null;

                dialog.GetFolder(out resShell);
                resShell.GetDisplayName(SIGDN.SIGDN_DESKTOPABSOLUTEPARSING, out IntPtr resultPtr);
                return COMPtrToUnicodeString(resultPtr);
            }
            catch (COMException)
            {
                return null;
            }
#if !NET8_0_OR_GREATER
            finally
            {
                if (dialog != null) Marshal.FinalReleaseComObject(dialog);
            }
#endif
        }).ConfigureAwait(false);

        private static void SetFileTypeFilter(IFileOpenDialog dialog, Dictionary<string, string> FileTypeFilter)
        {
            if (FileTypeFilter != null)
            {
                int len = FileTypeFilter.Count;
                int i = 0;
                COMDLG_FILTERSPEC[] array = new COMDLG_FILTERSPEC[len];
                foreach (KeyValuePair<string, string> entry in FileTypeFilter)
                    array[i++] = new COMDLG_FILTERSPEC { pszName = entry.Key, pszSpec = entry.Value };

                IntPtr structPtr = ArrayToHGlobalPtr(array);
                dialog.SetFileTypes((uint)len, structPtr);
                Marshal.FreeHGlobal(structPtr);
            }
        }

        private static void SetFileTypeFilter(IFileSaveDialog dialog, Dictionary<string, string> FileTypeFilter)
        {
            if (FileTypeFilter != null)
            {
                int len = FileTypeFilter.Count;
                int i = 0;
                COMDLG_FILTERSPEC[] array = new COMDLG_FILTERSPEC[len];
                foreach (KeyValuePair<string, string> entry in FileTypeFilter)
                    array[i++] = new COMDLG_FILTERSPEC { pszName = entry.Key, pszSpec = entry.Value };

                IntPtr structPtr = ArrayToHGlobalPtr(array);
                dialog.SetFileTypes((uint)len, structPtr);
                Marshal.FreeHGlobal(structPtr);
            }
        }

        private static IntPtr ArrayToHGlobalPtr<T>(T[] array)
        {
            int sizeOf = Marshal.SizeOf<T>();

            IntPtr structPtr = Marshal.AllocHGlobal(sizeOf * array.Length);
            long partPtrLong = structPtr.ToInt64();
            for (int i = 0; i < array.Length; i++)
            {
                IntPtr partPtr = new IntPtr(partPtrLong);
                Marshal.StructureToPtr(array[i], partPtr, false);
                partPtrLong += sizeOf;
            }

            return structPtr;
        }

        private static IntPtr UnicodeStringToCOMPtr(string str) => Marshal.StringToCoTaskMemUni(str);

        private static string COMPtrToUnicodeString(IntPtr ptr)
        {
            try
            {
                string result = Marshal.PtrToStringUni(ptr);
                return result;
            }
            catch (Exception e)
            {
                Logger.LogWriteLine($"Error while marshalling COM Pointer {ptr} to string!\r\n{e}");
                throw;
            }
            finally
            {
                if (ptr != IntPtr.Zero) Marshal.FreeCoTaskMem(ptr);
            }
        }

        private static string[] GetIShellItemArray(IShellItemArray itemArray)
        {
            IShellItem item;
            uint fileCount;

            itemArray.GetCount(out fileCount);
            string[] results = new string[fileCount];

            for (uint i = 0; i < fileCount; i++)
            {
                itemArray.GetItemAt(i, out item);
                item.GetDisplayName(SIGDN.SIGDN_FILESYSPATH, out IntPtr _resPtr);
                results[0] = COMPtrToUnicodeString(_resPtr);
            }

            return results;
        }
    }
}
