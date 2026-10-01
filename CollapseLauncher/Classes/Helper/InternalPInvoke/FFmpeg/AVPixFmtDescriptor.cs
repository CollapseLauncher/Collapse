// ReSharper disable StringLiteralTypo
// ReSharper disable IdentifierTypo
// ReSharper disable InconsistentNaming
#pragma warning disable IDE0130
using System.Runtime.InteropServices;

namespace CollapseLauncher.Helper.InternalPInvoke.FFmpeg;

[StructLayout(LayoutKind.Sequential)]
public struct AVPixFmtDescriptor
{
    public byte* Name;

    public byte NbComponents;
    public byte Log2ChromaW;
    public byte Log2ChromaH;

    public ulong Flags;
}