using System.Collections.Generic;

namespace Hi3Helper.Preset
{
    public static class GameConfigurationTemplate
    {
        public static List<PresetConfigClasses> GameConfigTemplate = new List<PresetConfigClasses>
        {
            new PresetConfigClasses
            {
                ProfileName = "Hi3SEA",
                ZoneName = "Southeast Asia",
                InstallRegistryLocation = "HKEY_LOCAL_MACHINE\\SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Uninstall\\Honkai Impact 3",
                ConfigRegistryLocation = "Software\\miHoYo\\Honkai Impact 3",
                DefaultGameLocation = "C:\\Program Files\\Honkai Impact 3 sea",
                DictionaryHost = "http://data.in.seanew.ironmaid.xyz:14000/",
                UpdateDictionaryAddress = "update_sea/",
                BlockDictionaryAddress = "com.miHoYo.bh3oversea/",
                LanguageAvailable = new List<string>{ "en", "cn", "vn", "th", "id" },
                FallbackLanguage = "en",
                GameDirectoryName = "Games",
                GameExecutableName = "BH3.exe",
                IsConvertible = true,
                ConvertibleTo = new List<string>{ "Hi3Global" },
                ConvertibleCookbookURL = "https://prophost.ironmaid.xyz/_shared/_diffrepo/{0}",
                BetterHi3LauncherVerInfoReg = "VersionInfoSEA",
                ZipFileURL = "https://prophost.ironmaid.xyz/_shared/_zipfiles/sea/{0}/",
                CachesListAPIURL = "https://prophost.ironmaid.xyz/api/updatepackage?platform=0&datatype={0}&gamever={1}&usenewformat=true",
                CachesEndpointURL = "http://ali-hk-bundle-os01.oss-cn-hongkong.aliyuncs.com/asset_bundle/overseas01/1.1/{0}/editor_compressed/",
                CachesListGameVerID = 0,
                LauncherSpriteURL = "https://sdk-os-static.mihoyo.com/bh3_global/mdk/launcher/api/content?key=tEGNtVhN&filter_adv=false&language=en-us&launcher_id=9",
                LauncherResourceURL = "https://sdk-os-static.mihoyo.com/bh3_global/mdk/launcher/api/resource?channel_id=1&key=tEGNtVhN&launcher_id=9&sub_channel_id=1"
            },
            new PresetConfigClasses
            {
                ProfileName = "Hi3Global",
                ZoneName = "Global",
                InstallRegistryLocation = "HKEY_LOCAL_MACHINE\\SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Uninstall\\Honkai Impact 3rd",
                SteamInstallRegistryLocation = "HKEY_LOCAL_MACHINE\\SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Uninstall\\Steam App 1671200",
                SteamGameID = 1671200,
                ConfigRegistryLocation = "Software\\miHoYo\\Honkai Impact 3rd",
                DefaultGameLocation = "C:\\Program Files\\Honkai Impact 3rd glb",
                DictionaryHost = "http://data.in.seanew.ironmaid.xyz:14000/",
                UpdateDictionaryAddress = "update_global/",
                BlockDictionaryAddress = "tmp/com.miHoYo.bh3global/",
                LanguageAvailable = new List<string>{ "en", "cn", "fr", "de" },
                FallbackLanguage = "en",
                GameDirectoryName = "Games",
                GameExecutableName = "BH3.exe",
                IsConvertible = true,
                ConvertibleTo = new List<string>{ "Hi3SEA" },
                ConvertibleCookbookURL = "https://prophost.ironmaid.xyz/_shared/_diffrepo/{0}",
                BetterHi3LauncherVerInfoReg = "VersionInfoGlobal",
                ZipFileURL = "https://prophost.ironmaid.xyz/_shared/_zipfiles/global/{0}/",
                CachesListAPIURL = "https://prophost.ironmaid.xyz/api/updatepackage?platform=0&datatype={0}&gamever={1}&usenewformat=true",
                CachesEndpointURL = "http://d2wztyirwsuyyo.cloudfront.net/asset_bundle/eur01/1.1/{0}/editor_compressed/",
                CachesListGameVerID = 1,
                LauncherSpriteURL = "https://sdk-os-static.mihoyo.com/bh3_global/mdk/launcher/api/content?key=dpz65xJ3&filter_adv=false&language=en-us&launcher_id=10",
                LauncherResourceURL = "https://sdk-os-static.mihoyo.com/bh3_global/mdk/launcher/api/resource?key=dpz65xJ3&channel_id=1&launcher_id=10&sub_channel_id=1"
            },
            new PresetConfigClasses
            {
                ProfileName = "Hi3CN",
                ZoneName = "Mainland China",
                InstallRegistryLocation = "HKEY_LOCAL_MACHINE\\SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Uninstall\\崩坏3",
                ConfigRegistryLocation = "Software\\miHoYo\\崩坏3",
                DefaultGameLocation = "C:\\Program Files\\Honkai Impact 3",
                DictionaryHost = "http://data.in.seanew.ironmaid.xyz:14000/",
                UpdateDictionaryAddress = "update_cn/",
                BlockDictionaryAddress = "tmp/Original/",
                LanguageAvailable = new List<string>{ "cn" },
                FallbackLanguage = "cn",
                GameDirectoryName = "Games",
                GameExecutableName = "BH3.exe",
                BetterHi3LauncherVerInfoReg = "VersionInfoCN",
                ZipFileURL = "https://prophost.ironmaid.xyz/_shared/_zipfiles/{0}/",
                CachesListAPIURL = "https://prophost.ironmaid.xyz/api/updatepackage?platform=0&datatype={0}&gamever={1}&usenewformat=true",
                CachesEndpointURL = "http://bh3rd.oss-cn-shanghai.aliyuncs.com/asset_bundle/pc01/1.0/{0}/editor_compressed/",
                CachesListGameVerID = 2,
                UseRightSideProgress = true,
                IsHideSocMedDesc = false,
                LauncherSpriteURL = "https://sdk-static.mihoyo.com/bh3_cn/mdk/launcher/api/content?key=SyvuPnqL&filter_adv=false&language=zh-cn&launcher_id=4",
                LauncherResourceURL = "https://sdk-static.mihoyo.com/bh3_cn/mdk/launcher/api/resource?channel_id=1&key=SyvuPnqL&launcher_id=4&sub_channel_id=1",
            },
            new PresetConfigClasses
            {
                ProfileName = "Hi3TW",
                ZoneName = "TW/HK/MO",
                InstallRegistryLocation = "HKEY_LOCAL_MACHINE\\SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Uninstall\\崩壞3rd",
                ConfigRegistryLocation = "Software\\miHoYo\\崩壞3rd",
                DefaultGameLocation = "C:\\Program Files\\Honkai Impact 3rd tw",
                DictionaryHost = "http://data.in.seanew.ironmaid.xyz:14000/",
                UpdateDictionaryAddress = "update_cn/",
                BlockDictionaryAddress = "tmp/Original/",
                LanguageAvailable = new List<string>{ "cn" },
                FallbackLanguage = "cn",
                GameDirectoryName = "Games",
                GameExecutableName = "BH3.exe",
                BetterHi3LauncherVerInfoReg = "VersionInfoTW",
                LauncherSpriteURL = "https://sdk-os-static.mihoyo.com/bh3_global/mdk/launcher/api/content?key=demhUTcW&filter_adv=false&language=zh-tw&launcher_id=8",
                LauncherResourceURL = "https://sdk-os-static.mihoyo.com/bh3_global/mdk/launcher/api/resource?channel_id=1&key=demhUTcW&launcher_id=8&sub_channel_id=1"
            },
            new PresetConfigClasses
            {
                ProfileName = "Hi3KR",
                ZoneName = "Korean",
                InstallRegistryLocation = "HKEY_LOCAL_MACHINE\\SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Uninstall\\붕괴3rd",
                ConfigRegistryLocation = "Software\\miHoYo\\붕괴3rd",
                DefaultGameLocation = "C:\\Program Files\\Honkai Impact 3rd kr",
                DictionaryHost = "http://data.in.seanew.ironmaid.xyz:14000/",
                UpdateDictionaryAddress = "update_cn/",
                BlockDictionaryAddress = "tmp/Original/",
                LanguageAvailable = new List<string>{ "cn" },
                FallbackLanguage = "cn",
                GameDirectoryName = "Games",
                GameExecutableName = "BH3.exe",
                BetterHi3LauncherVerInfoReg = "VersionInfoKR",
                LauncherSpriteURL = "https://sdk-os-static.mihoyo.com/bh3_global/mdk/launcher/api/content?key=PRg571Xh&filter_adv=false&language=ko-kr&launcher_id=11",
                LauncherResourceURL = "https://sdk-os-static.mihoyo.com/bh3_global/mdk/launcher/api/resource?channel_id=1&key=PRg571Xh&launcher_id=11&sub_channel_id=1"
            },
            new PresetConfigClasses
            {
                ProfileName = "GIGlb",
                ZoneName = "Genshin Impact",
                InstallRegistryLocation = "HKEY_LOCAL_MACHINE\\SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Uninstall\\Genshin Impact",
                ConfigRegistryLocation = "Software\\miHoYo\\Genshin Impact",
                DefaultGameLocation = "C:\\Program Files\\Genshin Impact",
                LanguageAvailable = new List<string>{ "en" },
                FallbackLanguage = "en",
                IsGenshin = true,
                UseRightSideProgress = true,
                GameDirectoryName = "Genshin Impact game",
                GameDispatchURL = "https://{0}.yuanshen.com/query_cur_region?version={1}&platform=3&channel_id=1&dispatchSeed={2}",
                GameExecutableName = "GenshinImpact.exe",
                ProtoDispatchKey = "10616738251919d2",
                IsHideSocMedDesc = false,
                LauncherSpriteURLMultiLang = true,
                LauncherSpriteURL = "https://sdk-os-static.mihoyo.com/hk4e_global/mdk/launcher/api/content?filter_adv=false&key=gcStgarh&language={0}&launcher_id=10",
                LauncherResourceURL = "https://sdk-os-static.mihoyo.com/hk4e_global/mdk/launcher/api/resource?channel_id=1&key=gcStgarh&launcher_id=10&sub_channel_id=0"
            },
            new PresetConfigClasses
            {
                ProfileName = "GICN",
                ZoneName = "原神",
                InstallRegistryLocation = "HKEY_LOCAL_MACHINE\\SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Uninstall\\原神",
                ConfigRegistryLocation = "Software\\miHoYo\\原神",
                DefaultGameLocation = "C:\\Program Files\\Genshin Impact",
                LanguageAvailable = new List<string>{ "en" },
                FallbackLanguage = "en",
                IsGenshin = true,
                UseRightSideProgress = true,
                GameDirectoryName = "Genshin Impact game",
                GameExecutableName = "YuanShen.exe",
                IsHideSocMedDesc = false,
                LauncherSpriteURL = "https://sdk-static.mihoyo.com/hk4e_cn/mdk/launcher/api/content?filter_adv=false&key=eYd89JmJ&language=zh-cn&launcher_id=18",
                LauncherResourceURL = "https://sdk-static.mihoyo.com/hk4e_cn/mdk/launcher/api/resource?channel_id=1&key=eYd89JmJ&launcher_id=18&sub_channel_id=1"
            }
        };
    }
}
