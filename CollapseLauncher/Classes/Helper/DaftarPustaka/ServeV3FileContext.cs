using Hi3Helper.EncTool;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Hashing;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
#pragma warning disable IDE0130

#nullable enable
namespace CollapseLauncher.Helper.DaftarPustaka;

[JsonSerializable(typeof(Dictionary<string, ServeV3FileContext>))]
[JsonSourceGenerationOptions(WriteIndented = true,
                             IndentCharacter = ' ',
                             IndentSize = 2,
                             AllowTrailingCommas = true)]
internal partial class ServeV3FileContextContext : JsonSerializerContext;

public class ServeV3FileContext
{
    public byte[] OriginFileHash  { get; set; } = [];
    public byte[] ContentFileHash { get; set; } = [];

    public string? DataHashType     { get; set; }
    public string? FilenameHashType { get; set; }

    public DateTimeOffset FileTime { get; set; }
    public long           Salt     { get; set; }

    public Stream? Stream { get; set; }

    public Dictionary<string, byte[]>? Metadata { get; set; }

    public void GetKeyAndIvFromLength(int keyLength, int ivLength, out byte[] key, out byte[] iv)
    {
        Unsafe.SkipInit(out key);
        Unsafe.SkipInit(out iv);

        if (FileTime == default) throw new InvalidOperationException("FileTime property cannot be empty!");
        if (Salt == 0) throw new InvalidOperationException("Salt property cannot be empty!");

        key = new byte[keyLength];
        iv  = new byte[ivLength];

        DateTimeOffset fileTime = FileTime;
        long dateTimeAsLong = MemoryMarshal.Read<long>(MemoryMarshal.AsBytes(new ReadOnlySpan<DateTimeOffset>(ref fileTime)));

        int saltLow  = unchecked((int)Salt);
        int saltHigh = unchecked((int)(Salt >> 32));

        int dateTimeLow  = unchecked((int)dateTimeAsLong);
        int dateTimeHigh = unchecked((int)(dateTimeAsLong >> 32));

        Random random = new(((saltLow | saltHigh) - (dateTimeLow | dateTimeHigh)) | (keyLength * ivLength));

        if (key.Length != 0)
        {
            Array.Copy(ContentFileHash,
                       0,
                       key,
                       0,
                       Math.Min(ContentFileHash.Length, key.Length));

            for (int i = 0; i < keyLength; i++)
                key[i] |= (byte)random.Next();
        }

        if (iv.Length != 0)
        {
            Array.Copy(ContentFileHash,
                       0,
                       iv,
                       0,
                       Math.Min(ContentFileHash.Length, iv.Length));

            for (int i = 0; i < ivLength; i++)
                iv[i] |= (byte)random.Next();
        }
    }

    public bool IsKeyStoreExist(string key) => Metadata?.ContainsKey(key) ?? false;

    public string GetOriginalFileUrl()
    {
        const string dictKey = "origUrl";
        if (!TryReadStringStoreAs(dictKey, out string? result))
        {
            throw new KeyNotFoundException("origUrl from pustaka's store is not exist. Please report this issue to our Discord Server!");
        }

        return string.IsNullOrEmpty(result)
            ? throw new NullReferenceException("origUrl from pustaka's store is null or just an empty string. Please report this issue to our Discord Server!")
            : result;
    }

    public bool TryReadStringStoreAs(string key, out string? result)
    {
        result = null;
        if (!IsKeyStoreExist(key))
        {
            return false;
        }

        ReadOnlySpan<byte> dataSpan = Metadata?[key];
        result = Encoding.UTF8.GetString(dataSpan);
        return true;
    }

    public async Task<CDNCacheResult> GetOriginalFileHttpResponse(
        HttpClient        client,
        HttpMethod?       method = null,
        CancellationToken token  = default)
    {
        string originalUrl = GetOriginalFileUrl();
        return await client.TryGetCachedStreamFrom(originalUrl, method, token);
    }

    public static string GetHashStringAuto(string source, ServeV3FileContext context)
        => GetHashStringAuto(source.AsSpan(), context);

    public static string GetHashStringAuto(ReadOnlySpan<char> source, ServeV3FileContext context)
    {
        return context.FilenameHashType switch
        {
            nameof(XxHash3)   => GetHashString<XxHash3>(source),
            nameof(XxHash32)  => GetHashString<XxHash32>(source),
            nameof(XxHash64)  => GetHashString<XxHash64>(source),
            nameof(XxHash128) => GetHashString<XxHash128>(source),
            nameof(Crc32)     => GetHashString<Crc32>(source),
            nameof(Crc64)     => GetHashString<Crc64>(source),

            _ => throw new NotSupportedException($"FilenameHashType: {context.FilenameHashType} is not supported")
        };
    }

    public static string GetHashString<T>(ReadOnlySpan<char> source)
        where T : NonCryptographicHashAlgorithm, new()
    {
        T hasher = new();

        Span<byte> hashBuffer     = stackalloc byte[hasher.HashLengthInBytes];
        Span<char> hashCharBuffer = stackalloc char[hasher.HashLengthInBytes * 2];

        ReadOnlySpan<byte> sourceAsBytes = MemoryMarshal.AsBytes(source);
        hasher.Append(sourceAsBytes);
        hasher.GetHashAndReset(hashBuffer);

        Convert.TryToHexStringLower(hashBuffer, hashCharBuffer, out _);

        return new string(hashCharBuffer);
    }
}