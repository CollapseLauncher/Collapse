
<p align="center">
  <img width="512px" height="auto" src="https://raw.githubusercontent.com/CollapseLauncher/Collapse/main/Docs/Imgs/CollapseLauncherIdolType.png"/><br/>
  <i>I know, it's not a good one. But at least we made it lol</i>
  <i>~ neon-nyan</i>
  <img src="https://raw.githubusercontent.com/CollapseLauncher/.github/main/profile/NewBannerv2_Banner_color_20240615.webp"/>
</p>

##### GI Nahida Background Credit: [Rafa on Pixiv](https://www.pixiv.net/en/users/3970196)

[![jp](https://img.shields.io/badge/README-jp-red.svg)](Docs/README.localized/README.ja-jp.md)
[![id](https://img.shields.io/badge/README-id-red.svg)](Docs/README.localized/README.id-id.md)
[![pt](https://img.shields.io/badge/README-pt-red.svg)](Docs/README.localized/README.pt-pt.md)
[![fr](https://img.shields.io/badge/README-fr-red.svg)](Docs/README.localized/README.fr-fr.md)
[![ru](https://img.shields.io/badge/README-ru-red.svg)](Docs/README.localized/README.ru-ru.md)

**Collapse** was originally designed for **Honkai Impact 3rd**. However, as the project evolved, this launcher is now a game client for all currently released **miHoYo Games**.

[![Build-Canary](https://github.com/neon-nyan/Collapse/actions/workflows/build.yml/badge.svg?branch=main)](https://github.com/neon-nyan/Collapse/actions/workflows/build.yml)
[![Qodana](https://github.com/CollapseLauncher/Collapse/actions/workflows/qodana-scan.yml/badge.svg)](https://github.com/CollapseLauncher/Collapse/actions/workflows/qodana-scan.yml)
[![Sync to GitLab](https://github.com/CollapseLauncher/CollapseLauncher-ReleaseRepo/actions/workflows/sync-to-gitlab.yml/badge.svg)](https://github.com/CollapseLauncher/CollapseLauncher-ReleaseRepo/actions/workflows/sync-to-gitlab.yml)
[![Upload to R2](https://github.com/neon-nyan/CollapseLauncher-ReleaseRepo/actions/workflows/upload-to-r2.yml/badge.svg)](https://github.com/neon-nyan/CollapseLauncher-ReleaseRepo/actions/workflows/upload-to-r2.yml)
[![FOSSA Status](https://app.fossa.com/api/projects/git%2Bgithub.com%2FCollapseLauncher%2FCollapse.svg?type=shield&issueType=license)](https://app.fossa.com/projects/git%2Bgithub.com%2FCollapseLauncher%2FCollapse?ref=badge_shield&issueType=license)
[![FOSSA Status](https://app.fossa.com/api/projects/git%2Bgithub.com%2FCollapseLauncher%2FCollapse.svg?type=shield&issueType=security)](https://app.fossa.com/projects/git%2Bgithub.com%2FCollapseLauncher%2FCollapse?ref=badge_shield&issueType=security)


[![Localization](https://img.shields.io/badge/Localization-Transifex-blue)](https://explore.transifex.com/collapse-launcher/collapse-mainapp/)
[![Discord](https://img.shields.io/badge/Join_Community-Discord-5865F2)](https://discord.gg/vJd2exaS7j)
[![KanbanBoard](https://img.shields.io/badge/Kanban_Board-Trello-white)](https://trello.com/b/rsqrnn15/collapse-launcher-tracker)

<p align="center">
    <a href="https://explore.transifex.com/collapse-launcher/collapse-mainapp/" target="_blank">
       <img src="https://upload.wikimedia.org/wikipedia/commons/f/f7/Transifex_logo.svg" alt="Collapse Launcher Localization at Crowdin" Width=280/>
	</a>
    &nbsp;
    <a href="https://discord.gg/vJd2exaS7j" target="_blank">
        <img src="https://discordapp.com/api/guilds/1116150300324139131/widget.png?style=banner2" alt="Discord Shield for Collapse Launcher Discord server" Width=280/>
    </a>
</p>

# Why "Collapse"?
Collapse came from the **Honkai Impact** translation in Chinese and Japanese. The word came from [**崩坏**] or **Bēng huài** in Chinese and also [**崩壊**] or **Houkai** in Japanese, both meaning "**Collapse**" which is why we chose it as our launcher name with the added inspiration that this was supposed to be an alternative (enhanced) launcher for *Honkai Impact 3rd* in the first place.

**Collapse** contains advanced features for **Honkai Impact 3rd** that not only provide you with the ability to download and launch the game, but also some additional features that the Official Launcher doesn't have, including:
* Caches Download (aka "Updating Settings" in-game).
* Game Data Repair (with less bandwidth required than the *Data Repair* feature in-game).
* Changing Game Settings (Graphics Settings & Audio Settings) without opening the game.
* Multi-Region Game Download Support (Allows for downloading and launching different versions of the game).
* Steam to Global version conversion (without re-downloading the whole game).
* Global to SEA version (or vice versa) conversion.
* Multi-session Download for faster Game Download/Installation.
* Multi-thread Game Extraction for faster installation.
* Delta-Patch Update Mechanism for smaller updates ([**Click here**](https://github.com/neon-nyan/CollapseLauncher/wiki/Update-Game-Region-with-Delta-Patch) for more info).
* Ability to select Graphics API (DirectX 11 (FL: 10.1, 11.0 and 11.1) and DirectX 12 [May crash the game in newer stages]).

Not only that, this launcher also has some advanced features for **Genshin Impact**, including but not limited to:
* Choosing your voice-line language during the first installation, so you don't have to download it inside the game.
* Repair the game files including Game Caches, Cutscenes, Audio Pack & persistent files outside of the game.

# Supported Features 
> Features marked with a :warning: are scheduled for removal and/or are no longer supported by the Collapse development team.

<table>
  <thead>
    <tr>
      <th rowspan="2">Game Title</th>
      <th rowspan="2">Region</th>
      <th colspan="7">Features</th>
    </tr>
    <tr>
      <th>Install</th>
      <th>Pre-load</th>
      <th>Update</th>
      <th>Game Repair</th>
      <th>Cache Update</th>
      <th>Game Settings</th>
      <th>Game Region Convert</th>
    </tr>
  </thead>
  <tbody>
    <tr>
      <td rowspan="6">Honkai Impact 3rd</td>
      <td>Southeast Asia</td>
      <td>:white_check_mark:</td>
      <td>:white_check_mark:</td>
      <td>:white_check_mark: (Normal and Delta-patch updates are available)</td>
      <td>:white_check_mark:</td>
      <td>:white_check_mark:</td>
      <td>:white_check_mark:</td>
      <td>:warning: <br> (From SEA -&gt; Global) </td>
    </tr>
    <tr>
      <td>Global</td>
      <td>:white_check_mark:</td>
      <td>:white_check_mark:</td>
      <td>:white_check_mark: (Normal and Delta-patch updates are available)</td>
      <td>:white_check_mark:</td>
      <td>:white_check_mark:</td>
      <td>:white_check_mark:</td>
      <td>:warning: <br> (From Steam -&gt; Global) <br> (From Global -&gt; SEA) </td>
    </tr>
    <tr>
      <td>Mainland China</td>
      <td>:white_check_mark:</td>
      <td>:white_check_mark:</td>
      <td>:white_check_mark:</td>
      <td>:white_check_mark:</td>
      <td>:white_check_mark:</td>
      <td>:white_check_mark:</td>
    </tr>
    <tr>
      <td>TW/HK/MO</td>
      <td>:white_check_mark:</td>
      <td>:white_check_mark:</td>
      <td>:white_check_mark:</td>
      <td>:white_check_mark:</td>
      <td>:white_check_mark:</td>
      <td>:white_check_mark:</td>
    </tr>
    <tr>
      <td>Japan</td>
      <td>:white_check_mark:</td>
      <td>:white_check_mark:</td>
      <td>:white_check_mark:</td>
      <td>:white_check_mark:</td>
      <td>:white_check_mark:</td>
      <td>:white_check_mark:</td>
    </tr>
    <tr>
      <td>Korea</td>
      <td>:white_check_mark:</td>
      <td>:white_check_mark:</td>
      <td>:white_check_mark:</td>
      <td>:white_check_mark:</td>
      <td>:white_check_mark:</td>
      <td>:white_check_mark:</td>
    </tr>
    <tr>
      <td rowspan="4">Genshin Impact</td>
      <td>Global</td>
      <td>:white_check_mark:</td>
      <td>:white_check_mark:</td>
      <td>:white_check_mark:</td>
      <td>:white_check_mark:</td>
      <td>N/A</td>
      <td>:white_check_mark:</td>
    </tr>
    <tr>
      <td>Mainland China</td>
      <td>:white_check_mark:</td>
      <td>:white_check_mark:</td>
      <td>:white_check_mark:</td>
      <td>:white_check_mark:</td>
      <td>N/A</td>
      <td>:white_check_mark:</td>
    </tr>
    <tr>
      <td>Bilibili</td>
      <td>:white_check_mark:</td>
      <td>:white_check_mark:</td>
      <td>:white_check_mark:</td>
      <td>:white_check_mark:</td>
      <td>N/A</td>
      <td>:white_check_mark:</td>
    </tr>
    <tr>
      <td>Google Play (Global)</td>
      <td>:white_check_mark:</td>
      <td>:white_check_mark:</td>
      <td>:white_check_mark:</td>
      <td>:white_check_mark:</td>
      <td>N/A</td>
      <td>:white_check_mark:</td>
    </tr>
    <tr>
      <td rowspan="3">Honkai: Star Rail</td>
      <td>Global</td>
      <td>:white_check_mark:</td>
      <td>:white_check_mark:</td>
      <td>:white_check_mark:</td>
      <td>:white_check_mark:</td>
      <td>:white_check_mark:</td>
      <td>:white_check_mark:</td>
    </tr>
    <tr>
      <td>Mainland China</td>
      <td>:white_check_mark:</td>
      <td>:white_check_mark:</td>
      <td>:white_check_mark:</td>
      <td>:white_check_mark:</td>
      <td>:white_check_mark:</td>
      <td>:white_check_mark:</td>
    </tr>
    <tr>
      <td>Bilibili</td>
      <td>:white_check_mark:</td>
      <td>:white_check_mark:</td>
      <td>:white_check_mark:</td>
      <td>:white_check_mark:</td>
      <td>:white_check_mark:</td>
      <td>:white_check_mark:</td>
    </tr>
   <tr>
      <td rowspan="2">Zenless Zone Zero</td>
      <td>Global</td>
      <td>:white_check_mark:</td>
      <td>:white_check_mark:</td>
      <td>:white_check_mark:</td>
      <td>N/A</td>
      <td>N/A</td>
      <td>:white_check_mark:</td>
    </tr>
    <tr>
      <td>Mainland China</td>
      <td>:white_check_mark:</td>
      <td>:white_check_mark:</td>
      <td>:white_check_mark:</td>
      <td>N/A</td>
      <td>N/A</td>
      <td>:white_check_mark:</td>
    </tr>
  </tbody>
</table>

> **Note**:
> The table above serves to illustrate the features Collapse currently support. This list will continuously get updated as new features get added to Collapse and new games get released. If you have any issues with any supported features above, checks our GitHub issue tab, report if no active issues for it is found.
> > Please keep in mind that the Game Conversion feature is currently only available for Honkai Impact: 3rd. Other miHoYo/Cognosphere Pte. Ltd. games are currently not planned for game conversion. 

# Download Ready-To-Use Builds
[<img src="https://user-images.githubusercontent.com/30566970/172445052-b0e62327-1d2e-4663-bc0f-af50c7f23615.svg" width="320"/>](https://github.com/CollapseLauncher/Collapse/releases/download/CL-v1.80.19/CL-1.80.19-stable_Installer.exe)
> **Note**: The version for this build is `1.80.19` (Released on: August 13rd, 2024).

[<img src="https://user-images.githubusercontent.com/30566970/172445153-d098de0d-1236-4124-8e13-05000b374eb6.svg" width="320"/>](https://github.com/CollapseLauncher/Collapse/releases/download/CL-v1.82.2-pre/CL-1.82.2-preview_Installer.exe)
> **Note**: The version for this build is `1.82.2` (Released on: November 24th, 2024).

To view all releases, [**click here**](https://github.com/neon-nyan/CollapseLauncher/releases).

# System Requirements for Ready-to-use build
- OS: **Windows 10 1809 Update (build 17763)** or later / **Windows 11 (Any builds)**
- Architecture: **x64/AMD64**
- Internet Access: **Yes**

***

# Prerequisites for Building Locally/Development
> ### More information can be found in [**Contribution Guidelines**](CONTRIBUTING.md)

***

# Code Signing Policy
> Free code signing provided by [SignPath.io], certificate by [SignPath Foundation]
- This program will only transfer user data to user configured database server. Exception data will be sent to [Sentry.io] for error tracking purposes (user are able to disable the behavior in App Settings).
- Read our full [**Privacy Policy**](PRIVACY.md)
- Also read our [**Third Party Notices**](THIRD_PARTY_NOTICES.md) for license used by third party libraries that we use.

# Third-party repositories and libraries used in this project
**Disclaimer**: This project **IS NOT AFFILIATED** with [**miHoYo (miHoYo Co., Ltd.)**](https://www.mihoyo.com/) or [**HoYoverse (COGNOSPHERE PTE. LTD.)**](https://www.hoyoverse.com/en-us) by any means and is completely open-sourced. Any contributions are welcomed! 😃
- [**Windows UI Library**](https://github.com/microsoft/microsoft-ui-xaml) by Microsoft under [**MIT License**](https://github.com/microsoft/microsoft-ui-xaml/blob/main/LICENSE)
- [**Windows App SDK**](https://github.com/microsoft/WindowsAppSDK) by Microsoft under [**MIT License**](https://github.com/microsoft/WindowsAppSDK/blob/main/LICENSE)
- [**SharpHDiffPatch**](https://github.com/CollapseLauncher/SharpHDiffPatch.Core) by neon-nyan (original port of [**HPatchZ** by **housisong**](https://github.com/sisong/HDiffPatch)) under [**MIT License**](https://github.com/CollapseLauncher/SharpHDiffPatch.Core/blob/main/LICENSE)
- [**Color Thief .NET**](https://github.com/CollapseLauncher/ColorThief) by KSemenenko and forked by neon-nyan under [**MIT License**](https://github.com/CollapseLauncher/ColorThief/blob/master/LICENSE)
- [**SevenZipExtractor**](https://github.com/CollapseLauncher/SevenZipExtractor) by Alexander Selishchev (adoconnection) and forked by neon-nyan under [**MIT License**](https://github.com/CollapseLauncher/SevenZipExtractor/blob/master/LICENSE)
- [**Hi3Helper.Http**](https://github.com/CollapseLauncher/Hi3Helper.Http) by neon-nyan under [**MIT License**](https://github.com/CollapseLauncher/Hi3Helper.Http/blob/master/LICENSE)
- [**Hi3Helper.EncTool**](https://github.com/CollapseLauncher/Hi3Helper.EncTool) by neon-nyan
- [**Hi3Helper.SharpDiscordRPC**](https://github.com/CollapseLauncher/Hi3Helper.SharpDiscordRPC) from [**discord-rpc-csharp** by Lachee](https://github.com/Lachee/discord-rpc-csharp) and forked by Collapse Project Team under [**MIT License**](https://github.com/CollapseLauncher/Hi3Helper.SharpDiscordRPC/blob/master/LICENSE)
- [**Hi3Helper.Sophon**](https://github.com/CollapseLauncher/Hi3Helper.Sophon) by neon-nyan under [**MIT License**](https://github.com/CollapseLauncher/Hi3Helper.Sophon/blob/master/LICENSE)
- [**Hi3Helper.ZstdNet**](https://github.com/CollapseLauncher/Hi3Helper.ZetdNet) by skbkontur and forked by neon-nyan under [**BSD-3-Clause License**](https://github.com/CollapseLauncher/Hi3Helper.ZetdNet/blob/master/LICENSE)
- [**InnoSetupLogParser**](https://github.com/CollapseLauncher/InnoSetupLogParser) from [**isulr** by preseverence](https://github.com/preseverence/isulr) and forked by neon-nyan under [**WTFPL License**](https://github.com/CollapseLauncher/InnoSetupLogParser/blob/master/LICENSE)
- [**Google Protobuf**](https://github.com/protocolbuffers/protobuf) by Google LLC under [**BSD-3-Clause License**](https://github.com/protocolbuffers/protobuf/blob/main/LICENSE)
- [**Clowd.Squirrel**](https://github.com/clowd/Clowd.Squirrel/tree/1c7eb5e1f40629cfdbe201f9d56b802ec41fea63) by clowd under [**MIT License**](https://github.com/clowd/Clowd.Squirrel/blob/1c7eb5e1f40629cfdbe201f9d56b802ec41fea63/LICENSE)
- [**Windows Community Toolkit**](https://github.com/CommunityToolkit/Windows) by Microsoft under [**MIT License**](https://github.com/CommunityToolkit/Windows/blob/main/License.md)
- [**GitInfo**](https://github.com/devlooped/GitInfo) by Daniel Cazzulino (devlooped) under [**MIT License**](https://github.com/devlooped/GitInfo/blob/main/license.txt)
- [**H.NotifyIcon**](https://github.com/HavenDV/H.NotifyIcon) by Konstantin S. (HavenDV) under [**MIT License**](https://github.com/HavenDV/H.NotifyIcon/blob/master/LICENSE.md)
- [**HTML Agility Pack**](https://github.com/zzzprojects/html-agility-pack) by zzzproject under [**MIT License**](https://github.com/zzzprojects/html-agility-pack/blob/master/LICENSE)
- [**RomanNumerals**](https://github.com/picrap/RomanNumerals) by Pascal Craponne (picrap) under [**MIT License**](https://github.com/picrap/RomanNumerals/blob/master/LICENSE)
- [**TaskScheduler**](https://github.com/dahall/taskscheduler) by David Hall (dahall) under [**MIT License**](https://github.com/dahall/TaskScheduler/blob/master/license.md)
- [**SharpCompress**](https://github.com/adamhathcock/sharpcompress) by Adam Hathcock (adamhathcock) under [**MIT License**](https://github.com/adamhathcock/sharpcompress/blob/master/LICENSE.txt)
- [**UABT**](https://github.com/CollapseLauncher/UABT) by _unknown_

[![FOSSA Status](https://app.fossa.com/api/projects/git%2Bgithub.com%2FCollapseLauncher%2FCollapse.svg?type=large&issueType=license)](https://app.fossa.com/projects/git%2Bgithub.com%2FCollapseLauncher%2FCollapse?ref=badge_large&issueType=license)

# UI Design Overview
![](https://raw.githubusercontent.com/neon-nyan/CollapseLauncher-Page/main/images/UI%20Overview%20RC2.webp)

# How can I support this project?
Supporting is never an obligation but is always appreciated and motivates us to invest more time in the project and to keep this and other projects alive. To this end, should you decide to support us, here are ways you can do so :smile::
- **[GitHub Sponsors](https://github.com/sponsors/neon-nyan)**
- **[PayPal](https://paypal.me/neonnyan)**

Made by all captains around the world with ❤️. Fight for all that is beautiful in this world!

## Contributors

<!-- ALL-CONTRIBUTORS-LIST:START - Do not remove or modify this section -->
<!-- prettier-ignore-start -->
<!-- markdownlint-disable -->
<table>
  <tbody>
    <tr>
      <td align="center" valign="top" width="14.28%"><a href="https://prophost.ironmaid.xyz"><img src="https://avatars.githubusercontent.com/u/30566970?v=4?s=100" width="100px;" alt="Kemal Setya Adhi"/><br /><sub><b>Kemal Setya Adhi</b></sub></a><br /><a href="https://github.com/CollapseLauncher/Collapse/commits?author=neon-nyan" title="Code">💻</a> <a href="#design-neon-nyan" title="Design">🎨</a> <a href="#mentoring-neon-nyan" title="Mentoring">🧑‍🏫</a> <a href="#maintenance-neon-nyan" title="Maintenance">🚧</a> <a href="https://github.com/CollapseLauncher/Collapse/pulls?q=is%3Apr+reviewed-by%3Aneon-nyan" title="Reviewed Pull Requests">👀</a> <a href="#projectManagement-neon-nyan" title="Project Management">📆</a></td>
      <td align="center" valign="top" width="14.28%"><a href="https://ronfriedman.dev"><img src="https://avatars.githubusercontent.com/u/9833218?v=4?s=100" width="100px;" alt="Ron Friedman"/><br /><sub><b>Ron Friedman</b></sub></a><br /><a href="https://github.com/CollapseLauncher/Collapse/commits?author=Cryotechnic" title="Code">💻</a> <a href="#maintenance-Cryotechnic" title="Maintenance">🚧</a> <a href="https://github.com/CollapseLauncher/Collapse/pulls?q=is%3Apr+reviewed-by%3ACryotechnic" title="Reviewed Pull Requests">👀</a> <a href="#projectManagement-Cryotechnic" title="Project Management">📆</a></td>
      <td align="center" valign="top" width="14.28%"><a href="https://github.com/bagusnl"><img src="https://avatars.githubusercontent.com/u/28079733?v=4?s=100" width="100px;" alt="Bagus Nur Listiyono"/><br /><sub><b>Bagus Nur Listiyono</b></sub></a><br /><a href="https://github.com/CollapseLauncher/Collapse/commits?author=bagusnl" title="Code">💻</a> <a href="#maintenance-bagusnl" title="Maintenance">🚧</a> <a href="https://github.com/CollapseLauncher/Collapse/pulls?q=is%3Apr+reviewed-by%3Abagusnl" title="Reviewed Pull Requests">👀</a> <a href="#promotion-bagusnl" title="Promotion">📣</a> <a href="#infra-bagusnl" title="Infrastructure (Hosting, Build-Tools, etc)">🚇</a> <a href="#projectManagement-bagusnl" title="Project Management">📆</a></td>
      <td align="center" valign="top" width="14.28%"><a href="https://github.com/gablm"><img src="https://avatars.githubusercontent.com/u/44784408?v=4?s=100" width="100px;" alt="Gabriel Lima"/><br /><sub><b>Gabriel Lima</b></sub></a><br /><a href="https://github.com/CollapseLauncher/Collapse/commits?author=gablm" title="Code">💻</a> <a href="#translation-gablm" title="Translation">🌍</a></td>
      <td align="center" valign="top" width="14.28%"><a href="https://github.com/shatyuka"><img src="https://avatars.githubusercontent.com/u/31368738?v=4?s=100" width="100px;" alt="Shatyuka"/><br /><sub><b>Shatyuka</b></sub></a><br /><a href="https://github.com/CollapseLauncher/Collapse/commits?author=shatyuka" title="Code">💻</a> <a href="#translation-shatyuka" title="Translation">🌍</a></td>
      <td align="center" valign="top" width="14.28%"><a href="https://github.com/Kiki79250CoC"><img src="https://avatars.githubusercontent.com/u/54137141?v=4?s=100" width="100px;" alt="Kylian Nicouleaud"/><br /><sub><b>Kylian Nicouleaud</b></sub></a><br /><a href="#translation-Kiki79250CoC" title="Translation">🌍</a> <a href="https://github.com/CollapseLauncher/Collapse/issues?q=author%3AKiki79250CoC" title="Bug reports">🐛</a></td>
      <td align="center" valign="top" width="14.28%"><a href="https://misaka.sakurakoi.top/"><img src="https://avatars.githubusercontent.com/u/69132853?v=4?s=100" width="100px;" alt="misaka10843"/><br /><sub><b>misaka10843</b></sub></a><br /><a href="#translation-misaka10843" title="Translation">🌍</a></td>
    </tr>
    <tr>
      <td align="center" valign="top" width="14.28%"><a href="https://github.com/Vermilion-Sinsha"><img src="https://avatars.githubusercontent.com/u/131636335?v=4?s=100" width="100px;" alt="Vermilion-Sinsha"/><br /><sub><b>Vermilion-Sinsha</b></sub></a><br /><a href="#translation-Vermilion-Sinsha" title="Translation">🌍</a></td>
      <td align="center" valign="top" width="14.28%"><a href="https://github.com/kleqing"><img src="https://avatars.githubusercontent.com/u/78801337?v=4?s=100" width="100px;" alt="Pham Khanh Nhan"/><br /><sub><b>Pham Khanh Nhan</b></sub></a><br /><a href="#translation-kleqing" title="Translation">🌍</a></td>
      <td align="center" valign="top" width="14.28%"><a href="https://github.com/DeepChirp"><img src="https://avatars.githubusercontent.com/u/66902050?v=4?s=100" width="100px;" alt="DeepChirp"/><br /><sub><b>DeepChirp</b></sub></a><br /><a href="#translation-DeepChirp" title="Translation">🌍</a></td>
      <td align="center" valign="top" width="14.28%"><a href="https://github.com/Faelayis"><img src="https://avatars.githubusercontent.com/u/48393914?v=4?s=100" width="100px;" alt="Faelayis"/><br /><sub><b>Faelayis</b></sub></a><br /><a href="#translation-Faelayis" title="Translation">🌍</a></td>
      <td align="center" valign="top" width="14.28%"><a href="https://twitter.com/hasukay__"><img src="https://avatars.githubusercontent.com/u/22459107?v=4?s=100" width="100px;" alt="Hasukay"/><br /><sub><b>Hasukay</b></sub></a><br /><a href="#translation-Hasukay" title="Translation">🌍</a></td>
      <td align="center" valign="top" width="14.28%"><a href="https://github.com/Kajitsy"><img src="https://avatars.githubusercontent.com/u/94784342?v=4?s=100" width="100px;" alt="Kajitsy"/><br /><sub><b>Kajitsy</b></sub></a><br /><a href="https://github.com/CollapseLauncher/Collapse/commits?author=Kajitsy" title="Documentation">📖</a> <a href="#translation-Kajitsy" title="Translation">🌍</a></td>
      <td align="center" valign="top" width="14.28%"><a href="https://github.com/despenser08"><img src="https://avatars.githubusercontent.com/u/50207925?v=4?s=100" width="100px;" alt="despenser08"/><br /><sub><b>despenser08</b></sub></a><br /><a href="#translation-despenser08" title="Translation">🌍</a></td>
    </tr>
    <tr>
      <td align="center" valign="top" width="14.28%"><a href="https://seria.is-a.dev/"><img src="https://avatars.githubusercontent.com/u/61446626?v=4?s=100" width="100px;" alt="seria"/><br /><sub><b>seria</b></sub></a><br /><a href="#translation-seriaati" title="Translation">🌍</a></td>
      <td align="center" valign="top" width="14.28%"><a href="https://github.com/xTaiwanPingLord"><img src="https://avatars.githubusercontent.com/u/52948923?v=4?s=100" width="100px;" alt="xTaiwanPingLord"/><br /><sub><b>xTaiwanPingLord</b></sub></a><br /><a href="#translation-xTaiwanPingLord" title="Translation">🌍</a></td>
      <td align="center" valign="top" width="14.28%"><a href="https://github.com/Arikatsu"><img src="https://avatars.githubusercontent.com/u/104459145?v=4?s=100" width="100px;" alt="Scald"/><br /><sub><b>Scald</b></sub></a><br /><a href="https://github.com/CollapseLauncher/Collapse/commits?author=Arikatsu" title="Code">💻</a></td>
      <td align="center" valign="top" width="14.28%"><a href="https://github.com/muscularcandy67"><img src="https://avatars.githubusercontent.com/u/12277635?v=4?s=100" width="100px;" alt="Iskandar Montano"/><br /><sub><b>Iskandar Montano</b></sub></a><br /><a href="https://github.com/CollapseLauncher/Collapse/commits?author=muscularcandy67" title="Code">💻</a></td>
      <td align="center" valign="top" width="14.28%"><a href="https://github.com/Scighost"><img src="https://avatars.githubusercontent.com/u/61003590?v=4?s=100" width="100px;" alt="Scighost"/><br /><sub><b>Scighost</b></sub></a><br /><a href="https://github.com/CollapseLauncher/Collapse/issues?q=author%3AScighost" title="Bug reports">🐛</a></td>
      <td align="center" valign="top" width="14.28%"><a href="https://github.com/IhoFox"><img src="https://avatars.githubusercontent.com/u/133899447?v=4?s=100" width="100px;" alt="IhoFox"/><br /><sub><b>IhoFox</b></sub></a><br /><a href="#translation-IhoFox" title="Translation">🌍</a></td>
      <td align="center" valign="top" width="14.28%"><a href="https://github.com/kujou-kju"><img src="https://avatars.githubusercontent.com/u/23724383?v=4?s=100" width="100px;" alt="kujou"/><br /><sub><b>kujou</b></sub></a><br /><a href="#translation-kujou-kju" title="Translation">🌍</a></td>
    </tr>
  </tbody>
</table>

<!-- markdownlint-restore -->
<!-- prettier-ignore-end -->

<!-- ALL-CONTRIBUTORS-LIST:END -->

[SignPath Foundation]:https://signpath.org
[SignPath.io]:https://signpath.io
