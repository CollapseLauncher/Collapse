using System;

namespace CollapseLauncher.ShellLinkCOM
{
    internal enum EShellLinkGP : uint
    {
        SLGP_SHORTPATH = 1,
        SLGP_UNCPRIORITY = 2
    }

    [Flags]
    internal enum EShowWindowFlags : uint
    {
        SW_HIDE = 0,
        SW_SHOWNORMAL = 1,
        SW_NORMAL = 1,
        SW_SHOWMINIMIZED = 2,
        SW_SHOWMAXIMIZED = 3,
        SW_MAXIMIZE = 3,
        SW_SHOWNOACTIVATE = 4,
        SW_SHOW = 5,
        SW_MINIMIZE = 6,
        SW_SHOWMINNOACTIVE = 7,
        SW_SHOWNA = 8,
        SW_RESTORE = 9,
        SW_SHOWDEFAULT = 10,
        SW_MAX = 10
    }

    [Flags]
    public enum SHGetFileInfoConstants : int
    {
        SHGFI_ICON = 0x100, // get icon 
        SHGFI_DISPLAYNAME = 0x200, // get display name 
        SHGFI_TYPENAME = 0x400, // get type name 
        SHGFI_ATTRIBUTES = 0x800, // get attributes 
        SHGFI_ICONLOCATION = 0x1000, // get icon location 
        SHGFI_EXETYPE = 0x2000, // return exe type 
        SHGFI_SYSICONINDEX = 0x4000, // get system icon index 
        SHGFI_LINKOVERLAY = 0x8000, // put a link overlay on icon 
        SHGFI_SELECTED = 0x10000, // show icon in selected state 
        SHGFI_ATTR_SPECIFIED = 0x20000, // get only specified attributes 
        SHGFI_LARGEICON = 0x0, // get large icon 
        SHGFI_SMALLICON = 0x1, // get small icon 
        SHGFI_OPENICON = 0x2, // get open icon 
        SHGFI_SHELLICONSIZE = 0x4, // get shell size icon 
                                   //SHGFI_PIDL = 0x8,                  // pszPath is a pidl 
        SHGFI_USEFILEATTRIBUTES = 0x10, // use passed dwFileAttribute 
        SHGFI_ADDOVERLAYS = 0x000000020, // apply the appropriate overlays
        SHGFI_OVERLAYINDEX = 0x000000040 // Get the index of the overlay
    }

    /// <summary>
    /// Flags determining how the links with missing
    /// targets are resolved.
    /// </summary>
    [Flags]
    public enum EShellLinkResolveFlags : uint
    {
        /// <summary>
        /// Allow any match during resolution.  Has no effect
        /// on ME/2000 or above, use the other flags instead.
        /// </summary>
        SLR_ANY_MATCH = 0x2,

        /// <summary>
        /// Call the Microsoft Windows Installer. 
        /// </summary>
        SLR_INVOKE_MSI = 0x80,

        /// <summary>
        /// Disable distributed link tracking. By default, 
        /// distributed link tracking tracks removable media 
        /// across multiple devices based on the volume name. 
        /// It also uses the UNC path to track remote file 
        /// systems whose drive letter has changed. Setting 
        /// SLR_NOLINKINFO disables both types of tracking.
        /// </summary>
        SLR_NOLINKINFO = 0x40,

        /// <summary>
        /// Do not display a dialog box if the link cannot be resolved. 
        /// When SLR_NO_UI is set, a time-out value that specifies the 
        /// maximum amount of time to be spent resolving the link can 
        /// be specified in milliseconds. The function returns if the 
        /// link cannot be resolved within the time-out duration. 
        /// If the timeout is not set, the time-out duration will be 
        /// set to the default value of 3,000 milliseconds (3 seconds). 
        /// </summary>                                  
        SLR_NO_UI = 0x1,

        /// <summary>
        /// Not documented in SDK.  Assume same as SLR_NO_UI but 
        /// intended for applications without a hWnd.
        /// </summary>
        SLR_NO_UI_WITH_MSG_PUMP = 0x101,

        /// <summary>
        /// Do not update the link information. 
        /// </summary>
        SLR_NOUPDATE = 0x8,

        /// <summary>
        /// Do not execute the search heuristics. 
        /// </summary>                                                        
        SLR_NOSEARCH = 0x10,

        /// <summary>
        /// Do not use distributed link tracking. 
        /// </summary>
        SLR_NOTRACK = 0x20,

        /// <summary>
        /// If the link object has changed, update its path and list 
        /// of identifiers. If SLR_UPDATE is set, you do not need to 
        /// call IPersistFile::IsDirty to determine whether or not 
        /// the link object has changed. 
        /// </summary>
        SLR_UPDATE = 0x4
    }
}
