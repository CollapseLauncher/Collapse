using Hi3Helper.Data;
using System.IO;

namespace Hi3Helper.Shared.ClassStruct
{
    public enum AppBGMode
    {
        Desktop,
        Custom,
        Default
    }

    public struct AppIniStruct
    {
        public IniFile Profile;
        public Stream ProfileStream;
        public string ProfilePath;
    }

    public enum AppThemeMode
    {
        Default = 0,
        Light = 1,
        Dark = 2,
    }

    public enum GameInstallStateEnum
    {
        Installed = 0,
        InstalledHavePreload = 1,
        NotInstalled = 2,
        NeedsUpdate = 3,
        GameBroken = 4,
    }

    public enum CachesType
    {
        Data = 0,
        Event = 1,
        AI = 2
    }

    public enum CachesDataStatus
    {
        Missing = 0,
        Obsolete = 1,
        Unecessary = 2
    }

    public enum HPatchUtilStat
    {
        HPATCH_SUCCESS = 0,
        HPATCH_OPTIONS_ERROR,
        HPATCH_OPENREAD_ERROR,
        HPATCH_OPENWRITE_ERROR,
        HPATCH_FILEREAD_ERROR,
        HPATCH_FILEWRITE_ERROR,
        HPATCH_FILEDATA_ERROR,
        HPATCH_FILECLOSE_ERROR,
        HPATCH_MEM_ERROR,
        HPATCH_HDIFFINFO_ERROR,
        HPATCH_COMPRESSTYPE_ERROR,
        HPATCH_HPATCH_ERROR,

        HPATCH_PATHTYPE_ERROR,
        HPATCH_TEMPPATH_ERROR,
        HPATCH_DELETEPATH_ERROR,
        HPATCH_RENAMEPATH_ERROR,

        HPATCH_SPATCH_ERROR,
        HPATCH_BSPATCH_ERROR,
        DIRPATCH_DIRDIFFINFO_ERROR = 101,
        DIRPATCH_CHECKSUMTYPE_ERROR,
        DIRPATCH_CHECKSUMSET_ERROR,
        DIRPATCH_CHECKSUM_DIFFDATA_ERROR,
        DIRPATCH_CHECKSUM_OLDDATA_ERROR,
        DIRPATCH_CHECKSUM_NEWDATA_ERROR,
        DIRPATCH_CHECKSUM_COPYDATA_ERROR,
        DIRPATCH_PATCH_ERROR,
        DIRPATCH_LAOD_DIRDIFFDATA_ERROR,
        DIRPATCH_OPEN_OLDPATH_ERROR,
        DIRPATCH_OPEN_NEWPATH_ERROR,
        DIRPATCH_CLOSE_OLDPATH_ERROR,
        DIRPATCH_CLOSE_NEWPATH_ERROR,
        DIRPATCH_PATCHBEGIN_ERROR,
        DIRPATCH_PATCHFINISH_ERROR,

        HPATCH_CREATE_SFX_DIFFFILETYPE_ERROR = 201,
        HPATCH_CREATE_SFX_SFXTYPE_ERROR,
        HPATCH_CREATE_SFX_EXECUTETAG_ERROR,
        HPATCH_RUN_SFX_NOTSFX_ERROR,
        HPATCH_RUN_SFX_DIFFOFFSERT_ERROR
    }
}
