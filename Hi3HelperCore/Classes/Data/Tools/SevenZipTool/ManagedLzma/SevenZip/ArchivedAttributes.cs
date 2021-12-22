using System;
#if !(BUILD_PORTABLE && NET_45)
using System.IO;
#endif

// Documentation for attributes
// - SetFileAttributes lists readonly attributes: https://msdn.microsoft.com/en-us/library/windows/desktop/aa365535
// - Full list of attributes: https://msdn.microsoft.com/en-us/library/windows/desktop/gg258117
//
// Some attributes can only be read and never set.
// - 'Compressed' attribute
// - 'Device' attribute
// - 'Directory' attribute
// - 'Encrypted' attribute
// - 'ReparsePoint' attribute
// - 'SparseFile' attribute
//
// Also some attributes make no sense to restore.
// - 'Normal' attribute - this attribute has special meaning for APIs and makes no sense to set when restoring attributes
// - 'Offline' attribute - this attribute should not be set by applications, it is managed by the OS, changing it manually may cause problems
// - 'Temporary' attribute - this attribute is for files which should be kept in cache and not written to disk as long as possible; you can restore it if you really want to, but in my opinion it does not make sense for an unpack-operation to set this attribute
// - 'IntegrityStream' attribute - not supported
// - 'NoScrubData' attribute - not supported
//
// The remaining attributes are valid to restore.
// - 'Archive' attribute
// - 'ReadOnly' attribute
// - 'Hidden' attribute
// - 'System' attribute
// - 'NotContentIndexed' attribute
//

namespace ManagedLzma.SevenZip
{
    [Flags]
    public enum ArchivedAttributes
    {
        /// <summary>
        /// The file has no special attributes.
        /// </summary>
        None = 0,

        /// <summary>
        /// The file is a candidate for backup or removal.
        /// </summary>
        Archive = 32,

        /// <summary>
        /// The file is read-only.
        /// </summary>
        ReadOnly = 1,

        /// <summary>
        /// The file is hidden, and thus is not included in an ordinary directory listing.
        /// </summary>
        Hidden = 2,

        /// <summary>
        /// The file is a system file. That is, the file is part of the operating system or is used exclusively by the operating system.
        /// </summary>
        System = 4,

        /// <summary>
        /// The file will not be indexed by the operating system's content indexing service.
        /// </summary>
        NotContentIndexed = 8192,
    }

    public static partial class ArchivedAttributesExtensions
    {
#if DEBUG
        internal static void CheckArchivedAttributesConsistency()
        {
#if !(BUILD_PORTABLE && NET_45)
            // Perform sanity checks to make sure 

            System.Diagnostics.Debug.Assert((int)ArchivedAttributes.None == (int)default(FileAttributes));
            System.Diagnostics.Debug.Assert((int)ArchivedAttributes.Archive == (int)FileAttributes.Archive);
            System.Diagnostics.Debug.Assert((int)ArchivedAttributes.ReadOnly == (int)FileAttributes.ReadOnly);
            System.Diagnostics.Debug.Assert((int)ArchivedAttributes.Hidden == (int)FileAttributes.Hidden);
            System.Diagnostics.Debug.Assert((int)ArchivedAttributes.System == (int)FileAttributes.System);
            System.Diagnostics.Debug.Assert((int)ArchivedAttributes.NotContentIndexed == (int)FileAttributes.NotContentIndexed);

            System.Diagnostics.Debug.Assert((int)DirectoryAttribute == (int)FileAttributes.Directory);

            System.Diagnostics.Debug.Assert((int)ForbiddenAttributes == (int)(0
                | FileAttributes.Device
                | FileAttributes.Encrypted
                | FileAttributes.ReparsePoint
                | FileAttributes.SparseFile
                ));

            System.Diagnostics.Debug.Assert((int)StrippedAttributes == (int)(0
                | FileAttributes.Normal
                | FileAttributes.Offline
                | FileAttributes.Compressed
                | FileAttributes.IntegrityStream
                | FileAttributes.NoScrubData
                ));

            System.Diagnostics.Debug.Assert((int)ToleratedAttributes == (int)(0
                | FileAttributes.Temporary
                ));

            // Make sure the invalid attributes are the complement of all other attributes.
            System.Diagnostics.Debug.Assert(InvalidAttributes == ~(VisibleAttributes | DirectoryAttribute | ToleratedAttributes | StrippedAttributes | ForbiddenAttributes));

            FileAttributes kAllFileAttributes = 0;
            foreach (var value in (FileAttributes[])Enum.GetValues(typeof(FileAttributes)))
                kAllFileAttributes |= value;

            // Make sure that we detect when the .NET Framework adds new entries into its enum
            System.Diagnostics.Debug.Assert(((int)InvalidAttributes & (int)kAllFileAttributes) == 0);
#endif
        }
#endif

        /// <summary>
        /// Unofficial extension using the high bits to store posix file attributes in a 7z archive.
        /// </summary>
        /// <remarks>
        /// Some third party implementations store posix file attributes in the high 16 bits.
        /// Some implementations also set bit 15 to indicate that the high bits are used,
        /// but since only some do this the bit can't be trusted and must be ignored.
        ///
        /// Note that this is an unofficial extension making use of the fact that the official
        /// implementation was lazy in its error checking. In newer implementations the 7z UI
        /// acknowledges the extension and tries to parse it. The extension however has not
        /// become part of the 7z archive codebase, it is only handled in the UI layer.
        /// </remarks>
        internal const ArchivedAttributes PosixAttributeMask = (ArchivedAttributes)~0x7FFF;

        /// <summary>
        /// These flags do not exist and are invalid.
        /// </summary>
        internal const ArchivedAttributes InvalidAttributes = (ArchivedAttributes)~196599;

        /// <summary>
        /// These attributes cause an exception when writing them and are auto-removed when reading them.
        /// </summary>
        internal const ArchivedAttributes ForbiddenAttributes = (ArchivedAttributes)17984;

        /// <summary>
        /// These attributes are auto-removed when reading or writing them.
        /// </summary>
        internal const ArchivedAttributes StrippedAttributes = (ArchivedAttributes)170112;

        /// <summary>
        /// These attributes are not included in the enum but are allowed to be read or written.
        /// </summary>
        internal const ArchivedAttributes ToleratedAttributes = (ArchivedAttributes)256;

        /// <summary>
        /// This attribute marks directories and is automatically managed.
        /// It is not exposed by the library except when implementing a custom metadata reader.
        /// </summary>
        internal const ArchivedAttributes DirectoryAttribute = (ArchivedAttributes)16;

        /// <summary>
        /// Contains all publically visible attributes.
        /// </summary>
        internal const ArchivedAttributes VisibleAttributes = ArchivedAttributes.None
            | ArchivedAttributes.Archive
            | ArchivedAttributes.ReadOnly
            | ArchivedAttributes.Hidden
            | ArchivedAttributes.System
            | ArchivedAttributes.NotContentIndexed
            ;

#if !(BUILD_PORTABLE && NET_45)
        public const FileAttributes FileAttributeMask = (FileAttributes)(int)VisibleAttributes;

        public static FileAttributes ToFileAttributes(this ArchivedAttributes attributes)
        {
            return (FileAttributes)attributes;
        }

        public static FileAttributes? ToFileAttributes(this ArchivedAttributes? attributes)
        {
            return attributes.HasValue ? (FileAttributes)attributes.Value : default(FileAttributes?);
        }
#endif
    }
}
