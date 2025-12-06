using System;
using System.IO;
// ReSharper disable MemberInitializerValueIgnored
// ReSharper disable UnusedAutoPropertyAccessor.Global
// ReSharper disable InconsistentNaming
// ReSharper disable CheckNamespace
// ReSharper disable IdentifierTypo

namespace CollapseLauncher
{
    internal class DeltaPatchProperty
    {
        internal DeltaPatchProperty(string patchFile)
        {
            ReadOnlySpan<string> strings = Path.GetFileNameWithoutExtension(patchFile).Split('_');
            MD5hash = strings[5];
            ZipHash = strings[4];
            ProfileName = strings[0];
            SourceVer = strings[1];
            TargetVer = strings[2];
            PatchCompr = strings[3];
            PatchPath = patchFile;
        }

        public string ZipHash { get; set; }
        public string MD5hash { get; set; }
        public string ProfileName { get; }
        public string SourceVer { get; }
        public string TargetVer { get; }
        public string PatchCompr { get; set; }
        public string PatchPath { get; }
    }
}
