using System.Collections.Generic;
using System.Text.Json.Serialization;

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
        public string ConcatN
        {
            get => $"{N}_{CRC}.unity3d";
        }

        public string ConcatNRemote
        {
            get => $"{N}_{CRC}";
        }

        // Filepath for input.
        // You have to concatenate the N with CRC to get the filepath using ConcatN()
        public string N { get; set; }

        // Hash of the file (HMACSHA1)
        // For more information:
        // https://docs.microsoft.com/en-us/dotnet/api/system.security.cryptography.hmacsha1
        public string CRC { get; set; }
        public string CRCLower { get => CRC.ToLower(); }

        // File size of the cache file.
        [JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)]
        public long CS { get; set; }
        public CachesType DataType { get; set; }
        public CachesDataStatus Status { get; set; }
        public PatchDataPropertiesContent PatchProperty { get; set; }
    }

    public class PatchDataPropertiesContent
    {
        public string PatchFileName { get; set; }
        public string PatchFileSize { get; set; }
        public string PatchAfterName { get; set; }
    }
}
