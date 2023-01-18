using Hi3Helper.UABT;
using Hi3Helper.UABT.Binary;
using System;
using System.IO;

namespace Hi3Helper.EncTool
{
    public static class XMFUtility
    {
        /// <summary>
        /// Compares the version given from <c>versionBytes</c> with the one from xmfPath.
        /// </summary>
        /// <param name="xmfPath">The path of the XMF file to compare.</param>
        /// <param name="versionBytes">The <c>int</c> array of version to compare. The array length must be 4.</param>
        /// <returns>
        /// If the version contained in the XMF file matches the one from <c>versionBytes</c> or there's any fault, then return <c>false</c>.<br/>
        /// If it matches, then return <c>true</c>.
        /// </returns>
        public static bool CheckIfXMFVersionMatches(string xmfPath, ReadOnlySpan<int> versionBytes)
        {
            FileInfo xmf = new FileInfo(xmfPath);

            if (!xmf.Exists) return false;
            if (versionBytes.Length != XMFParser._versioningLength) return false;

            using (EndianBinaryReader reader = new EndianBinaryReader(xmf.OpenRead(), EndianType.LittleEndian))
            {
                reader.Position = XMFParser._signatureLength;
                ReadOnlySpan<int> versionXMF = XMFParser.ReadVersion(reader);

                return versionXMF[0] == versionBytes[0] && versionXMF[1] == versionBytes[1]
                    && versionXMF[2] == versionBytes[2] && versionXMF[3] == versionBytes[3];
            }
        }
    }
}
