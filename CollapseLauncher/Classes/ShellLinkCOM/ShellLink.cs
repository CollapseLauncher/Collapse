using Hi3Helper;
using System;
using System.Buffers;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;

#nullable enable
#pragma warning disable CS8500 // This takes the address of, gets the size of, or declares a pointer to a managed type
namespace CollapseLauncher.ShellLinkCOM
{
    public class ShellLink : IDisposable
    {
        private unsafe delegate void ToDelegateInvoke(char* buffer, int length);
        private unsafe delegate void ToDelegateWithW32FindDataInvoke(char* buffer, nint findDataPtr, int length);

        public enum LinkDisplayMode : uint
        {
            edmNormal = EShowWindowFlags.SW_NORMAL,
            edmMinimized = EShowWindowFlags.SW_SHOWMINNOACTIVE,
            edmMaximized = EShowWindowFlags.SW_MAXIMIZE
        }

        // Use Unicode (W) under NT, otherwise use ANSI      
        IShellLinkW? linkW;
        IPersistFile? persistFileW;
        IPersist? persistW;
        IPropertyStore? propertyStoreW;
        string shortcutFile = "";

        /// <summary>
        /// Creates an instance of the Shell Link object.
        /// </summary>
        public ShellLink()
        {
            PInvoke.CoCreateInstance(
                CLSIDGuid.ClsId_ShellLink,
                IntPtr.Zero,
                CLSCTX.CLSCTX_INPROC_SERVER,
                out IShellLinkW? shellLink
                ).ThrowOnFailure();

            linkW = shellLink;
            persistFileW = shellLink?.CastComInterfaceAs<IShellLinkW, IPersistFile>(in CLSIDGuid.IGuid_IPersistFile);
            persistW = shellLink?.CastComInterfaceAs<IShellLinkW, IPersist>(in CLSIDGuid.IGuid_IPersist);
            propertyStoreW = shellLink?.CastComInterfaceAs<IShellLinkW, IPropertyStore>(in CLSIDGuid.IGuid_IPropertyStore);
        }

        /// <summary>
        /// Creates an instance of a Shell Link object
        /// from the specified link file
        /// </summary>
        /// <param name="linkFile">The Shortcut file to open</param>
        public ShellLink(string linkFile) : this() => Open(linkFile);

        /// <summary>
        /// Call dispose just in case it hasn't happened yet
        /// </summary>
        ~ShellLink() => Dispose();

        /// <summary>
        /// Dispose the object, releasing the COM ShellLink object
        /// </summary>
        public void Dispose() => PInvoke.Free(linkW);

        public string ShortCutFile
        {
            get => shortcutFile;
            set => shortcutFile = value;
        }

        /// <summary>
        /// This pointer must be destroyed with DistroyIcon when you are done with it.
        /// </summary>
        /// <param name="large">Whether to return the small or large icon</param>
        public IntPtr GetIcon(bool large)
        {
            // Get icon index and path:
            string iconFile = IconPath;
            int iconIndex = IconIndex;

            // If there are no details set for the icon, then we must use
            // the shell to get the icon for the target:
            if (iconFile.Length == 0)
            {
                // Use the FileIcon object to get the icon:
                SHGetFileInfoConstants flags =
                    SHGetFileInfoConstants.SHGFI_ICON |
                        SHGetFileInfoConstants.SHGFI_ATTRIBUTES;
                if (large)
                {
                    flags = flags | SHGetFileInfoConstants.SHGFI_LARGEICON;
                }
                else
                {
                    flags = flags | SHGetFileInfoConstants.SHGFI_SMALLICON;
                }
                FileIcon fileIcon = new FileIcon(Target, flags);
                return fileIcon.ShellIcon;
            }
            else
            {
                // Use ExtractIconEx to get the icon:
                IntPtr[] hIconEx = new IntPtr[1] { IntPtr.Zero };
                uint iconCount = 0;
                if (large)
                {
                    iconCount = InvokeProp.ExtractIconEx(
                        iconFile,
                        iconIndex,
                        hIconEx,
                        null,
                        1);
                }
                else
                {
                    iconCount = InvokeProp.ExtractIconEx(
                        iconFile,
                        iconIndex,
                        null,
                        hIconEx,
                        1);
                }

                return hIconEx[0];
            }
        }

        private string? _iconPath;
        /// <summary>
        /// Gets the path to the file containing the icon for this shortcut.
        /// </summary>
        public unsafe string IconPath
        {
            get => _iconPath ??= GetStringFromIMethod(260, (ptr, len) => linkW?.GetIconLocation(ptr, len, out _));
            set => linkW?.SetIconLocation(_iconPath = value, IconIndex);
        }

        private int? _iconIndex;
        /// <summary>
        /// Gets the index of this icon within the icon path's resources
        /// </summary>
        public unsafe int IconIndex
        {
            get
            {
                if (_iconIndex == null)
                {
                    int iconIndex = 0;
                    _ = GetStringFromIMethod(260, (ptr, len) => linkW?.GetIconLocation(ptr, len, out iconIndex));
                    _iconIndex = iconIndex;
                }
                return _iconIndex ?? 0;
            }
            set => linkW?.SetIconLocation(IconPath, value);
        }

        private string? _target;
        /// <summary>
        /// Gets/sets the fully qualified path to the link's target
        /// </summary>
        public unsafe string Target
        {
            get => _target ??= GetStringAndW32FindDataFromIMethod(260, out _, (ptr, fd, len) => linkW?.GetPath(ptr, len, fd, (uint)EShellLinkGP.SLGP_UNCPRIORITY));
            set => linkW?.SetPath(_target = value);
        }

        private string? _workingDirectory;
        /// <summary>
        /// Gets/sets the Working Directory for the Link
        /// </summary>
        public unsafe string WorkingDirectory
        {
            get => _workingDirectory ??= GetStringFromIMethod(260, (ptr, len) => linkW?.GetWorkingDirectory(ptr, len));
            set => linkW?.SetWorkingDirectory(_workingDirectory = value);
        }

        private string? _description;
        /// <summary>
        /// Gets/sets the description of the link
        /// </summary>
        public unsafe string Description
        {
            get => _description ??= GetStringFromIMethod(1024, (ptr, len) => linkW?.GetDescription(ptr, len));
            set => linkW?.SetDescription(_description = value);
        }

        private string? _arguments;
        /// <summary>
        /// Gets/sets any command line arguments associated with the link
        /// </summary>
        public unsafe string Arguments
        {
            get => _arguments ??= GetStringFromIMethod(260, (ptr, len) => linkW?.GetArguments(ptr, len));
            set => linkW?.SetArguments(_arguments = value);
        }

        /// <summary>
        /// Gets/sets the initial display mode when the shortcut is
        /// run
        /// </summary>
        public LinkDisplayMode DisplayMode
        {
            get
            {
                uint cmd = 0;
                linkW?.GetShowCmd(out cmd);
                return (LinkDisplayMode)cmd;
            }
            set => linkW?.SetShowCmd((uint)value);
        }

        /// <summary>
        /// Gets/sets the HotKey to start the shortcut (if any)
        /// </summary>
        public short HotKey
        {
            get
            {
                short key = 0;
                linkW?.GetHotkey(out key);
                return key;
            }
            set => linkW?.SetHotkey(value);
        }

        private unsafe Win32FindDataW* GetWin32FindDataFromBuffer(byte[] buffer)
        {
            fixed (void* refPtr = &buffer[0])
            {
                return (Win32FindDataW*)refPtr;
            }
        }

        private unsafe string GetStringAndW32FindDataFromIMethod(int length, out byte[] win32FindDataBuffer, ToDelegateWithW32FindDataInvoke toInvokeDelegate)
        {
            int sizeOfFindData = Marshal.SizeOf<Win32FindDataW>();

            win32FindDataBuffer = new byte[sizeOfFindData];
            char[] buffer = ArrayPool<char>.Shared.Rent(length);
            try
            {
                fixed (char* bufferPtr = &buffer[0])
                fixed (void* findDataPtr = &win32FindDataBuffer[0])
                {
                    nint findDataSafe = (nint)findDataPtr;
                    toInvokeDelegate(bufferPtr, findDataSafe, length);

                    return GetStringFromNullTerminatedPtr(bufferPtr);
                }
            }
            finally
            {
                ArrayPool<char>.Shared.Return(buffer);
            }
        }

        private unsafe string GetStringFromIMethod(int length, ToDelegateInvoke toInvokeDelegate)
        {
            char[] buffer = ArrayPool<char>.Shared.Rent(length);
            try
            {
                fixed (char* bufferPtr = &buffer[0])
                {
                    toInvokeDelegate(bufferPtr, length);
                    return GetStringFromNullTerminatedPtr(bufferPtr);
                }
            }
            finally
            {
                ArrayPool<char>.Shared.Return(buffer);
            }
        }

        private static unsafe string GetStringFromNullTerminatedPtr(char* bufferPtr)
        {
            ReadOnlySpan<char> returnString = MemoryMarshal.CreateReadOnlySpanFromNullTerminated(bufferPtr);
            string outString = returnString.ToString();
            return outString;
        }

        private unsafe string GetFileNameFromDataPtr(Win32FindDataW* win32FindData)
        {
            byte* fdPtr = (byte*)win32FindData;
            void* offset = fdPtr + 44; // Fixed pos of cFileName field
            char* offsetField = (char*)offset;

            ReadOnlySpan<char> spanNullTerminated = MemoryMarshal.CreateReadOnlySpanFromNullTerminated(offsetField);
            return spanNullTerminated.ToString();
        }

        private unsafe string GetAlternativeFileNameFromDataPtr(Win32FindDataW* win32FindData)
        {
            byte* fdPtr = (byte*)win32FindData;
            void* offset = fdPtr + 44 + 520; // Fixed pos of cFileName + sizeof(cFileName) field
            char* offsetField = (char*)offset;

            ReadOnlySpan<char> spanNullTerminated = MemoryMarshal.CreateReadOnlySpanFromNullTerminated(offsetField);
            return spanNullTerminated.ToString();
        }

        /// <summary>
        /// Sets the appUserModelId
        /// </summary>
        public void SetAppUserModelId(string appId)
        {
            var pkey = PropertyKey.PKEY_AppUserModel_ID;
            var str = PropVariant.FromString(appId);
            propertyStoreW?.SetValue(ref pkey, ref str);
        }

        /// <summary>
        /// Sets the ToastActivatorCLSID
        /// </summary>
        public void SetToastActivatorCLSID(string clsid)
        {
            var guid = Guid.Parse(clsid);
            SetToastActivatorCLSID(guid);
        }

        /// <summary>
        /// Sets the ToastActivatorCLSID
        /// </summary>
        public void SetToastActivatorCLSID(Guid clsid)
        {
            PropertyKey pkey = PropertyKey.PKEY_AppUserModel_ToastActivatorCLSID;
            PropVariant varGuid = PropVariant.FromGuid(clsid);
            try
            {
                int errCode = propertyStoreW?.SetValue(ref pkey, ref varGuid) ?? unchecked((int)0x80004003);
                Marshal.ThrowExceptionForHR(errCode);

                errCode = propertyStoreW?.Commit() ?? unchecked((int)0x80004003);
                Marshal.ThrowExceptionForHR(errCode);
            }
            finally
            {
                varGuid.Clear();
            }
        }

        /// <summary>
        /// Saves the shortcut to ShortCutFile.
        /// </summary>
        public void Save()
        {
            Save(shortcutFile);
        }

        /// <summary>
        /// Saves the shortcut to the specified file
        /// </summary>
        /// <param name="linkFile">The shortcut file (.lnk)</param>
        public void Save(string linkFile) => persistFileW?.Save(linkFile, true);

        /// <summary>
        /// Loads a shortcut from the specified file
        /// </summary>
        /// <param name="linkFile">The shortcut file (.lnk) to load</param>
        public void Open(string linkFile) => Open(linkFile, IntPtr.Zero, EShellLinkResolveFlags.SLR_ANY_MATCH | EShellLinkResolveFlags.SLR_NO_UI, 1);

        /// <summary>
        /// Loads a shortcut from the specified file, and allows flags controlling
        /// the UI behaviour if the shortcut's target isn't found to be set.
        /// </summary>
        /// <param name="linkFile">The shortcut file (.lnk) to load</param>
        /// <param name="hWnd">The window handle of the application's UI, if any</param>
        /// <param name="resolveFlags">Flags controlling resolution behaviour</param>
        public void Open(string linkFile, IntPtr hWnd, EShellLinkResolveFlags resolveFlags) => Open(linkFile, hWnd, resolveFlags, 1);

        /// <summary>
        /// Loads a shortcut from the specified file, and allows flags controlling
        /// the UI behaviour if the shortcut's target isn't found to be set.  If
        /// no SLR_NO_UI is specified, you can also specify a timeout.
        /// </summary>
        /// <param name="linkFile">The shortcut file (.lnk) to load</param>
        /// <param name="hWnd">The window handle of the application's UI, if any</param>
        /// <param name="resolveFlags">Flags controlling resolution behaviour</param>
        /// <param name="timeOut">Timeout if SLR_NO_UI is specified, in ms.</param>
        public void Open(string linkFile, IntPtr hWnd, EShellLinkResolveFlags resolveFlags, ushort timeOut)
        {
            uint flags;

            if ((resolveFlags & EShellLinkResolveFlags.SLR_NO_UI)
                == EShellLinkResolveFlags.SLR_NO_UI)
            {
                flags = (uint)((int)resolveFlags | (timeOut << 16));
            }
            else
            {
                flags = (uint)resolveFlags;
            }

            persistFileW?.Load(linkFile, 0);
            this.shortcutFile = linkFile;
        }
    }

    /// <summary>
    /// Enables extraction of icons for any file type from
    /// the Shell.
    /// </summary>
    [ExcludeFromCodeCoverage]
    [SupportedOSPlatform("windows")]
    public class FileIcon
    {
        const int MAX_PATH = 260;

        [StructLayout(LayoutKind.Sequential)]
        struct SHFILEINFO
        {
            public IntPtr hIcon;
            public int iIcon;
            public int dwAttributes;

            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = MAX_PATH)]
            public string szDisplayName;

            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 80)]
            public string szTypeName;
        }

        [DllImport("shell32")]
        static extern IntPtr SHGetFileInfo(
            string pszPath,
            int dwFileAttributes,
            ref SHFILEINFO psfi,
            uint cbFileInfo,
            uint uFlags);

        [DllImport("user32.dll")]
        static extern int DestroyIcon(IntPtr hIcon);

        const int FORMAT_MESSAGE_ALLOCATE_BUFFER = 0x100;
        const int FORMAT_MESSAGE_ARGUMENT_ARRAY = 0x2000;
        const int FORMAT_MESSAGE_FROM_HMODULE = 0x800;
        const int FORMAT_MESSAGE_FROM_STRING = 0x400;
        const int FORMAT_MESSAGE_FROM_SYSTEM = 0x1000;
        const int FORMAT_MESSAGE_IGNORE_INSERTS = 0x200;
        const int FORMAT_MESSAGE_MAX_WIDTH_MASK = 0xFF;

        [DllImport("kernel32")]
        static extern int FormatMessage(
            int dwFlags,
            IntPtr lpSource,
            int dwMessageId,
            int dwLanguageId,
            string lpBuffer,
            uint nSize,
            IntPtr argumentsLong);

        [DllImport("kernel32")]
        static extern int GetLastError();

        string? fileName;
        string? displayName;
        string? typeName;
        SHGetFileInfoConstants flags;
        IntPtr fileIcon;

        /// <summary>
        /// Gets/sets the flags used to extract the icon
        /// </summary>
        public SHGetFileInfoConstants Flags
        {
            get => flags;
            set => flags = value;
        }

        /// <summary>
        /// Gets/sets the filename to get the icon for
        /// </summary>
        public string? FileName
        {
            get => fileName;
            set => fileName = value;
        }

        /// <summary>
        /// Gets the icon for the chosen file
        /// </summary>
        public IntPtr ShellIcon => fileIcon;

        /// <summary>
        /// Gets the display name for the selected file
        /// if the SHGFI_DISPLAYNAME flag was set.
        /// </summary>
        public string? DisplayName => displayName;

        /// <summary>
        /// Gets the type name for the selected file
        /// if the SHGFI_TYPENAME flag was set.
        /// </summary>
        public string? TypeName => typeName;

        /// <summary>
        ///  Gets the information for the specified 
        ///  file name and flags.
        /// </summary>
        private void GetInfo()
        {
            fileIcon = IntPtr.Zero;
            typeName = "";
            displayName = "";

            SHFILEINFO shfi = new SHFILEINFO();
            uint shfiSize = (uint)Marshal.SizeOf<SHFILEINFO>();

            IntPtr ret = SHGetFileInfo(
                fileName ?? string.Empty, 0, ref shfi, shfiSize, (uint)(flags));
            if (ret != IntPtr.Zero)
            {
                if (shfi.hIcon != IntPtr.Zero)
                {
                    fileIcon = shfi.hIcon; // need to dispose this
                }
                typeName = shfi.szTypeName;
                displayName = shfi.szDisplayName;
            }
            else
            {
                int err = GetLastError();
                Console.WriteLine("Error {0}", err);
                string txtS = new string('\0', 256);
                int len = FormatMessage(
                    FORMAT_MESSAGE_FROM_SYSTEM | FORMAT_MESSAGE_IGNORE_INSERTS,
                    IntPtr.Zero, err, 0, txtS, 256, IntPtr.Zero);
                Console.WriteLine("Len {0} text {1}", len, txtS);

                // throw exception
            }
        }

        /// <summary>
        /// Constructs a new, default instance of the FileIcon
        /// class.  Specify the filename and call GetInfo()
        /// to retrieve an icon.
        /// </summary>
        public FileIcon()
        {
            flags = SHGetFileInfoConstants.SHGFI_ICON |
                SHGetFileInfoConstants.SHGFI_DISPLAYNAME |
                SHGetFileInfoConstants.SHGFI_TYPENAME |
                SHGetFileInfoConstants.SHGFI_ATTRIBUTES |
                SHGetFileInfoConstants.SHGFI_EXETYPE;
        }

        /// <summary>
        /// Constructs a new instance of the FileIcon class
        /// and retrieves the icon, display name and type name
        /// for the specified file.      
        /// </summary>
        /// <param name="fileName">The filename to get the icon, 
        /// display name and type name for</param>
        public FileIcon(string fileName)
            : this()
        {
            this.fileName = fileName;
            GetInfo();
        }

        /// <summary>
        /// Constructs a new instance of the FileIcon class
        /// and retrieves the information specified in the 
        /// flags.
        /// </summary>
        /// <param name="fileName">The filename to get information
        /// for</param>
        /// <param name="flags">The flags to use when extracting the
        /// icon and other shell information.</param>
        public FileIcon(string fileName, SHGetFileInfoConstants flags)
        {
            this.fileName = fileName;
            this.flags = flags;
            GetInfo();
        }
    }
}
