using System;
using System.IO;

namespace CollapseLauncher
{
    internal class DeltaPatchProperty
    {
        internal DeltaPatchProperty(string PatchFile)
        {
            ReadOnlySpan<string> strings = Path.GetFileNameWithoutExtension(PatchFile).Split('_');
            this.MD5hash = strings[5];
            this.ZipHash = strings[4];
            this.ProfileName = strings[0];
            this.SourceVer = strings[1];
            this.TargetVer = strings[2];
            this.PatchCompr = strings[3];
            this.PatchPath = PatchFile;
        }

        public string ZipHash { get; set; }
        public string MD5hash { get; set; }
        public string ProfileName { get; set; }
        public string SourceVer { get; set; }
        public string TargetVer { get; set; }
        public string PatchCompr { get; set; }
        public string PatchPath { get; set; }
    }
}
