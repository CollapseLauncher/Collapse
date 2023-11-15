using System;
using System.Runtime.InteropServices;

namespace CollapseLauncher.FileDialogCOM
{
    [ComImport]
    [Guid(IIDGuid.IFileOpenDialog)]
    [CoClass(typeof(FileOpenDialogRCW))]
    public interface NativeFileOpenDialog : IFileOpenDialog
    { }

    [ComImport]
    [Guid(IIDGuid.IFileSaveDialog)]
    [CoClass(typeof(FileSaveDialogRCW))]
    public interface NativeFileSaveDialog : IFileSaveDialog
    { }

    [ComImport()]
    [Guid(IIDGuid.IModalWindow)]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    public interface IModalWindow
    {

        [PreserveSig]
        int Show(IntPtr parent);
    }

    [ComImport]
    [Guid(IIDGuid.IShellItemArray)]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    public interface IShellItemArray
    {
        // Not supported: IBindCtx

        void BindToHandler(IntPtr pbc, ref Guid rbhid, ref Guid riid, out IntPtr ppvOut);

        void GetPropertyStore(int Flags, ref Guid riid, out IntPtr ppv);

        void GetPropertyDescriptionList(ref PROPERTYKEY keyType, ref Guid riid, out IntPtr ppv);

        void GetAttributes(SIATTRIBFLAGS dwAttribFlags, uint sfgaoMask, out uint psfgaoAttribs);

        void GetCount(out uint pdwNumItems);

        void GetItemAt(uint dwIndex, out IShellItem ppsi);

        void EnumItems(out IntPtr ppenumShellItems);
    }

    [ComImport()]
    [Guid(IIDGuid.IFileDialog)]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    public partial interface IFileDialog
    {
        [PreserveSig]
        int Show(IntPtr parent);

        void SetFileTypes(uint cFileTypes, [MarshalAs(UnmanagedType.LPArray)] COMDLG_FILTERSPEC[] rgFilterSpec);

        void SetFileTypeIndex(uint iFileType);

        void GetFileTypeIndex(out uint piFileType);

        void Advise(IFileDialogEvents pfde, out uint pdwCookie);

        void Unadvise(uint dwCookie);

        void SetOptions(FOS fos);

        void GetOptions(out FOS pfos);

        void SetDefaultFolder(IShellItem psi);

        void SetFolder(IShellItem psi);

        void GetFolder(out IShellItem ppsi);

        void GetCurrentSelection(out IShellItem ppsi);

        void SetFileName([MarshalAs(UnmanagedType.LPWStr)] string pszName);

        void GetFileName([MarshalAs(UnmanagedType.LPWStr)] out string pszName);

        void SetTitle([MarshalAs(UnmanagedType.LPWStr)] string pszTitle);

        void SetOkButtonLabel([MarshalAs(UnmanagedType.LPWStr)] string pszText);

        void SetFileNameLabel([MarshalAs(UnmanagedType.LPWStr)] string pszLabel);

        void GetResult(out IShellItem ppsi);

        void AddPlace(IShellItem psi, int alignment);

        void SetDefaultExtension([MarshalAs(UnmanagedType.LPWStr)] string pszDefaultExtension);

        void Close([MarshalAs(UnmanagedType.Error)] int hr);

        void SetClientGuid(ref Guid guid);

        void ClearClientData();

        void SetFilter(IntPtr pFilter);
    }

    [ComImport()]
    [Guid(IIDGuid.IFileOpenDialog)]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    public interface IFileOpenDialog : IFileDialog
    {
        [PreserveSig]
        new int Show(IntPtr parent);

        void SetFileTypes(uint cFileTypes, ref COMDLG_FILTERSPEC rgFilterSpec);

        new void SetFileTypeIndex(uint iFileType);

        new void GetFileTypeIndex(out uint piFileType);

        new void Advise(IFileDialogEvents pfde, out uint pdwCookie);

        new void Unadvise(uint dwCookie);

        new void SetOptions(FOS fos);

        new void GetOptions(out FOS pfos);

        new void SetDefaultFolder(IShellItem psi);

        new void SetFolder(IShellItem psi);

        new void GetFolder(out IShellItem ppsi);

        new void GetCurrentSelection(out IShellItem ppsi);

        new void SetFileName([MarshalAs(UnmanagedType.LPWStr)] string pszName);

        new void GetFileName([MarshalAs(UnmanagedType.LPWStr)] out string pszName);

        new void SetTitle([MarshalAs(UnmanagedType.LPWStr)] string pszTitle);

        new void SetOkButtonLabel([MarshalAs(UnmanagedType.LPWStr)] string pszText);

        new void SetFileNameLabel([MarshalAs(UnmanagedType.LPWStr)] string pszLabel);

        new void GetResult(out IShellItem ppsi);

        void AddPlace(IShellItem psi, FileDialogCustomPlace fdcp);

        new void SetDefaultExtension([MarshalAs(UnmanagedType.LPWStr)] string pszDefaultExtension);

        new void Close([MarshalAs(UnmanagedType.Error)] int hr);

        new void SetClientGuid(ref Guid guid);

        new void ClearClientData();

        new void SetFilter(IntPtr pFilter);

        void GetResults(out IShellItemArray ppenum);

        void GetSelectedItems(out IShellItemArray ppsai);
    }

    [ComImport()]
    [Guid(IIDGuid.IFileSaveDialog)]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    public interface IFileSaveDialog : IFileDialog
    {
        [PreserveSig]
        new int Show(IntPtr parent);

        void SetFileTypes(uint cFileTypes, ref COMDLG_FILTERSPEC rgFilterSpec);

        new void SetFileTypeIndex(uint iFileType);

        new void GetFileTypeIndex(out uint piFileType);

        new void Advise(IFileDialogEvents pfde, out uint pdwCookie);

        new void Unadvise(uint dwCookie);

        new void SetOptions(FOS fos);

        new void GetOptions(out FOS pfos);

        new void SetDefaultFolder(IShellItem psi);

        new void SetFolder(IShellItem psi);

        new void GetFolder(out IShellItem ppsi);

        new void GetCurrentSelection(out IShellItem ppsi);

        new void SetFileName([MarshalAs(UnmanagedType.LPWStr)] string pszName);

        new void GetFileName([MarshalAs(UnmanagedType.LPWStr)] out string pszName);

        new void SetTitle([MarshalAs(UnmanagedType.LPWStr)] string pszTitle);

        new void SetOkButtonLabel([MarshalAs(UnmanagedType.LPWStr)] string pszText);

        new void SetFileNameLabel([MarshalAs(UnmanagedType.LPWStr)] string pszLabel);

        new void GetResult(out IShellItem ppsi);

        void AddPlace(IShellItem psi, FileDialogCustomPlace fdcp);

        new void SetDefaultExtension([MarshalAs(UnmanagedType.LPWStr)] string pszDefaultExtension);

        new void Close([MarshalAs(UnmanagedType.Error)] int hr);

        new void SetClientGuid(ref Guid guid);

        new void ClearClientData();

        new void SetFilter(IntPtr pFilter);

        void SetSaveAsItem(IShellItem psi);

        void SetProperties(IntPtr pStore);

        void SetCollectedProperties(IntPtr pList, int fAppendDefault);

        void GetProperties(out IntPtr ppStore);

        void ApplyProperties(IShellItem psi, IntPtr pStore, [ComAliasName("ShellObjects.wireHWND")] ref IntPtr hwnd, IntPtr pSink);
    }

    [ComImport]
    [Guid(IIDGuid.IFileDialogEvents)]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    public interface IFileDialogEvents
    {
        // NOTE: some of these callbacks are cancelable - returning S_FALSE means that 
        // the dialog should not proceed (e.g. with closing, changing folder); to
        // support this, we need to use the PreserveSig attribute to enable us to return 
        // the proper HRESULT 
        [PreserveSig]
        int OnFileOk(IFileDialog pfd);

        [PreserveSig]
        int OnFolderChanging(IFileDialog pfd, IShellItem psiFolder);

        void OnFolderChange(IFileDialog pfd);

        void OnSelectionChange(IFileDialog pfd);

        void OnShareViolation(IFileDialog pfd, IShellItem psi, out FDE_SHAREVIOLATION_RESPONSE pResponse);

        void OnTypeChange(IFileDialog pfd);

        void OnOverwrite(IFileDialog pfd, IShellItem psi, out FDE_OVERWRITE_RESPONSE pResponse);
    }

    [ComImport]
    [Guid(IIDGuid.IShellItem)]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    public partial interface IShellItem
    {
        void BindToHandler(IntPtr pbc, ref Guid bhid, ref Guid riid, out IntPtr ppv);

        void GetParent(out IShellItem ppsi);

        void GetDisplayName(SIGDN sigdnName, [MarshalAs(UnmanagedType.LPWStr)] out string ppszName);

        void GetAttributes(uint sfgaoMask, out uint psfgaoAttribs);

        void Compare(IShellItem psi, uint hint, out int piOrder);
    }
}
