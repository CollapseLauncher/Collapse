using System.Collections.Generic;

namespace Hi3Helper.Shared.ClassStruct
{
    public class DataProperties
    {
        // Timestamp of the cache updated in Epoch UNIX format
        public uint Timestamp { get; set; }

        // Package version of the cache
        public uint PackageVersion { get; set; }

        // The content of Cache's Properties List
        public List<DataPropertiesContent> Content { get; set; }
        public CachesType DataType { get; set; }
        public string HashSalt { get; set; }
    }

    public class DataPropertiesUI
    {
        public string FileName { get; set; }
        public CachesType DataType { get; set; }
        public string FileSource { get; set; }
        public string FileSizeStr { get; set; }
        public string FileLastModified { get; set; }
        public string FileNewModified { get; set; }
        public CachesDataStatus CacheStatus { get; set; }
    }

    public class DataPropertiesContent
    {
        // Concatenate N and CRC to get the filepath.
        public string ConcatN() => $"{N}_{CRC}.unity3d";
        public string ConcatNRemote() => $"{N}_{CRC}";

        // Filepath for input.
        // You have to concatenate the N with CRC to get the filepath using ConcatN()
        public string N { get; set; }

        // Hash of the file (HMACSHA1)
        // For more information:
        // https://docs.microsoft.com/en-us/dotnet/api/system.security.cryptography.hmacsha1
        public string CRC { get; set; }

        // File size of the cache file.
        public long CS { get; set; }

        // True => The file is necessary at Update Settings.
        // False => The file will only be downloaded while you're accessing certain menu (like Gacha Banner).
        public bool IsNecessary { get; set; }
        public CachesDataStatus Status { get; set; }
    }
}
