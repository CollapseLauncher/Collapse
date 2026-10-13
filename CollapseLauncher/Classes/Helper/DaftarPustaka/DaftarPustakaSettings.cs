// ReSharper disable IdentifierTypo
#pragma warning disable IDE0130

#nullable enable
namespace CollapseLauncher.Helper.DaftarPustaka;

public enum DaftarPustakaCompression : ushort
{
    Uncompressed,
    Brotli,
    Zstandard,
    Deflate,
    Zlib,
    Lzma,
    Lzma2,
    BZip2
}

public enum DaftarPustakaEncryption : ushort
{
    Unencrypted,
    Aes,
    Rsa,
    Xor
}


internal static class DaftarPustakaSettings
{
    internal const ulong  CollapseHeader = 7310310183885631299u;
    internal const ushort CurrentVersion = 3;
}
