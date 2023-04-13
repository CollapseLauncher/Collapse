using Hi3Helper.Data;
using System.Collections.Generic;
using System.IO;
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

    public class DataPropertiesContent
    {
        // Concatenate N and CRC to get the filepath.
        public string ConcatN => $"{N}_{CRC}.unity3d";
        public string ConcatNRemote => $"{N}_{CRC}";
        public string BaseURL { get; set; }
        public string BasePath { get; set; }
        public string ConcatURL => ConverterTool.CombineURLFromString(BaseURL, ConcatNRemote);
        public string ConcatPath => Path.Combine(BasePath, ConverterTool.NormalizePath(ConcatN));

        // Filepath for input.
        // You have to concatenate the N with CRC to get the filepath using ConcatN()
        public string N { get; set; }

        // Hash of the file (HMACSHA1)
        // For more information:
        // https://docs.microsoft.com/en-us/dotnet/api/system.security.cryptography.hmacsha1
        public string CRC { get; set; }
        public byte[] CRCArray => HexTool.HexToBytesUnsafe(CRC.ToLower());
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
