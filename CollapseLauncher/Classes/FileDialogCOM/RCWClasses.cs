using System;
using System.Runtime.InteropServices;

namespace CollapseLauncher.FileDialogCOM
{
    [ComImport]
    [ClassInterface(ClassInterfaceType.None)]
    [TypeLibType(TypeLibTypeFlags.FCanCreate)]
    [Guid(CLSIDGuid.FileOpenDialog)]
    public partial class FileOpenDialogRCW
    { }

    [ComImport]
    [ClassInterface(ClassInterfaceType.None)]
    [TypeLibType(TypeLibTypeFlags.FCanCreate)]
    [Guid(CLSIDGuid.FileSaveDialog)]
    public partial class FileSaveDialogRCW
    { }

    public class IIDGuid
    {
        private IIDGuid() { } // Avoid FxCop violation AvoidUninstantiatedInternalClasses
                              // IID GUID strings for relevant COM interfaces 
        internal const string IModalWindow = "b4db1657-70d7-485e-8e3e-6fcb5a5c1802";
        internal const string IFileDialog = "42f85136-db7e-439c-85f1-e4075d135fc8";
        internal const string IFileOpenDialog = "d57c7288-d4ad-4768-be02-9d969532d960";
        internal const string IFileSaveDialog = "84bccd23-5fde-4cdb-aea4-af64b83d78ab";
        internal const string IFileDialogEvents = "973510DB-7D7F-452B-8975-74A85828D354";
        internal const string IShellItem = "43826D1E-E718-42EE-BC55-A1E261C37BFE";
        internal const string IShellItemArray = "B63EA76D-1F85-456F-A19C-48159EFA858B";
    }

    public class CLSIDGuid
    {
        private CLSIDGuid() { } // Avoid FxCop violation AvoidUninstantiatedInternalClasses
        internal const string FileOpenDialog = "DC1C5A9C-E88A-4dde-A5A1-60F82A20AEF7";
        internal const string FileSaveDialog = "C0B4E2F3-BA21-4773-8DBA-335EC946EB8B";
    }
}
