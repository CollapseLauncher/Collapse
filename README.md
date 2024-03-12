
<p align="center">
  <img src="https://raw.githubusercontent.com/neon-nyan/CollapseLauncher-Page/main/images/NewBannerv2_color.webp"/>
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
[![Sync to Bitbucket](https://github.com/neon-nyan/CollapseLauncher-ReleaseRepo/actions/workflows/sync-to-bitbucket.yml/badge.svg)](https://github.com/neon-nyan/CollapseLauncher-ReleaseRepo/actions/workflows/sync-to-bitbucket.yml)
[![Upload to R2](https://github.com/neon-nyan/CollapseLauncher-ReleaseRepo/actions/workflows/upload-to-r2.yml/badge.svg)](https://github.com/neon-nyan/CollapseLauncher-ReleaseRepo/actions/workflows/upload-to-r2.yml)


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
      <td>:white_check_mark: <br> (From SEA -&gt; Global) </td>
    </tr>
    <tr>
      <td>Global</td>
      <td>:white_check_mark:</td>
      <td>:white_check_mark:</td>
      <td>:white_check_mark: (Normal and Delta-patch updates are available)</td>
      <td>:white_check_mark:</td>
      <td>:white_check_mark:</td>
      <td>:white_check_mark:</td>
      <td>:white_check_mark: <br> (From Steam -&gt; Global) <br> (From Global -&gt; SEA) </td>
    </tr>
    <tr>
      <td>Mainland China</td>
      <td>:white_check_mark:</td>
      <td>:white_check_mark:</td>
      <td>:white_check_mark:</td>
      <td>:white_check_mark:</td>
      <td>:white_check_mark:</td>
      <td>:white_check_mark:</td>
      <td>N/A</td>
    </tr>
    <tr>
      <td>TW/HK/MO</td>
      <td>:white_check_mark:</td>
      <td>:white_check_mark:</td>
      <td>:white_check_mark:</td>
      <td>:white_check_mark:</td>
      <td>:white_check_mark:</td>
      <td>:white_check_mark:</td>
      <td>N/A</td>
    </tr>
    <tr>
      <td>Japan</td>
      <td>:white_check_mark:</td>
      <td>:white_check_mark:</td>
      <td>:white_check_mark:</td>
      <td>:white_check_mark:</td>
      <td>:white_check_mark:</td>
      <td>:white_check_mark:</td>
      <td>N/A</td>
    </tr>
    <tr>
      <td>Korea</td>
      <td>:white_check_mark:</td>
      <td>:white_check_mark:</td>
      <td>:white_check_mark:</td>
      <td>:white_check_mark:</td>
      <td>:white_check_mark:</td>
      <td>:white_check_mark:</td>
      <td>N/A</td>
    </tr>
    <tr>
      <td rowspan="3">Genshin Impact</td>
      <td>Global</td>
      <td>:white_check_mark:</td>
      <td>:white_check_mark:</td>
      <td>:white_check_mark:</td>
      <td>:white_check_mark:</td>
      <td>N/A</td>
      <td>:white_check_mark:</td>
      <td>N/A</td>
    </tr>
    <tr>
      <td>Mainland China</td>
      <td>:white_check_mark:</td>
      <td>:white_check_mark:</td>
      <td>:white_check_mark:</td>
      <td>:white_check_mark:</td>
      <td>N/A</td>
      <td>:white_check_mark:</td>
      <td>N/A</td>
    </tr>
    <tr>
      <td>Bilibili</td>
      <td>:white_check_mark:</td>
      <td>:white_check_mark:</td>
      <td>:white_check_mark:</td>
      <td>:white_check_mark:</td>
      <td>N/A</td>
      <td>:white_check_mark:</td>
      <td>N/A</td>
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
      <td>N/A</td>
    </tr>
    <tr>
      <td>Mainland China</td>
      <td>:white_check_mark:</td>
      <td>:white_check_mark:</td>
      <td>:white_check_mark:</td>
      <td>:white_check_mark:</td>
      <td>:white_check_mark:</td>
      <td>:white_check_mark:</td>
      <td>N/A</td>
    </tr>
    <tr>
      <td>Bilibili</td>
      <td>:white_check_mark:</td>
      <td>:white_check_mark:</td>
      <td>:white_check_mark:</td>
      <td>:white_check_mark:</td>
      <td>:white_check_mark:</td>
      <td>:white_check_mark:</td>
      <td>N/A</td>
    </tr>
  </tbody>
</table>

> **Note**:
> The table above serves to illustrate the features Collapse currently support. This list will continuously get updated as new features get added to Collapse and new games get released. If you have any issues with any supported features above, checks our GitHub issue tab, report if no active issues for it is found.
> > Please keep in mind that the Game Conversion feature is currently only available for Honkai Impact: 3rd. Other miHoYo/Cognosphere Pte. Ltd. games are currently not planned for game conversion. 

# Download Ready-To-Use Builds
[<img src="https://user-images.githubusercontent.com/30566970/172445052-b0e62327-1d2e-4663-bc0f-af50c7f23615.svg" width="320"/>](https://github.com/CollapseLauncher/Collapse/releases/download/v1.73.5/CL-1.73.5-stable_Installer.exe)
> **Note**: The version for this build is `1.73.5` (Released on: March 9th, 2024).

[<img src="https://user-images.githubusercontent.com/30566970/172445153-d098de0d-1236-4124-8e13-05000b374eb6.svg" width="320"/>](https://github.com/CollapseLauncher/Collapse/releases/download/v1.73.5-pre/CL-1.73.5-preview_Installer.exe)
> **Note**: The version for this build is `1.73.5` (Released on: March 9th, 2024).

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
- This program will not transfer any information to other networked systems.
- Read our full [**Privacy Policy**](PRIVACY.md)
- Also read our [**Third Party Notices**](THIRD_PARTY_NOTICES.md) for license used by third party libraries that we use.

# Third-party repositories and libraries used in this project
- [**Windows UI Library**](https://github.com/microsoft/microsoft-ui-xaml) by Microsoft
- [**Windows App SDK**](https://github.com/microsoft/WindowsAppSDK) by Microsoft
- [**HDiffPatch**](https://github.com/sisong/HDiffPatch) by housisong
- [**Color Thief .NET**](https://github.com/neon-nyan/ColorThief) by KSemenenko
- [**SevenZipExtractor**](https://github.com/neon-nyan/SevenZipExtractor) by adoconnection
- [**Hi3Helper.Http**](https://github.com/neon-nyan/Hi3Helper.Http) by neon-nyan
- [**Hi3Helper.EncTool**](https://github.com/neon-nyan/Hi3Helper.EncTool) by neon-nyan
- [**Crc32.NET**](https://github.com/neon-nyan/Crc32.NET) by force-net
- [**UABT**](https://github.com/neon-nyan/UABT) by _unknown_

**Disclaimer**: This project **IS NOT AFFILIATED** with [**miHoYo (miHoYo Co., Ltd.)**](https://www.mihoyo.com/) or [**HoYoverse (COGNOSPHERE PTE. LTD.)**](https://www.hoyoverse.com/en-us) by any means and is completely open-sourced. Any contributions are welcomed! 😃

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
      <td align="center" valign="top" width="14.28%"><a href="https://prophost.ironmaid.xyz"><img src="https://avatars.githubusercontent.com/u/30566970?v=4?s=100" width="100px;" alt="Kemal Setya Adhi"/><br /><sub><b>Kemal Setya Adhi</b></sub></a><br /><a href="#code-neon-nyan" title="Code">💻</a> <a href="#design-neon-nyan" title="Design">🎨</a> <a href="#mentoring-neon-nyan" title="Mentoring">🧑‍🏫</a> <a href="#maintenance-neon-nyan" title="Maintenance">🚧</a> <a href="#review-neon-nyan" title="Reviewed Pull Requests">👀</a></td>
      <td align="center" valign="top" width="14.28%"><a href="https://ronfriedman.dev"><img src="https://avatars.githubusercontent.com/u/9833218?v=4?s=100" width="100px;" alt="Ron Friedman"/><br /><sub><b>Ron Friedman</b></sub></a><br /><a href="#code-Cryotechnic" title="Code">💻</a> <a href="#maintenance-Cryotechnic" title="Maintenance">🚧</a> <a href="#review-Cryotechnic" title="Reviewed Pull Requests">👀</a></td>
      <td align="center" valign="top" width="14.28%"><a href="https://github.com/gablm"><img src="https://avatars.githubusercontent.com/u/44784408?v=4?s=100" width="100px;" alt="Gabriel Lima"/><br /><sub><b>Gabriel Lima</b></sub></a><br /><a href="#code-gablm" title="Code">💻</a> <a href="#translation-gablm" title="Translation">🌍</a></td>
      <td align="center" valign="top" width="14.28%"><a href="https://github.com/shatyuka"><img src="https://avatars.githubusercontent.com/u/31368738?v=4?s=100" width="100px;" alt="Shatyuka"/><br /><sub><b>Shatyuka</b></sub></a><br /><a href="#code-shatyuka" title="Code">💻</a> <a href="#translation-shatyuka" title="Translation">🌍</a></td>
    </tr>
  </tbody>
</table>

<!-- markdownlint-restore -->
<!-- prettier-ignore-end -->

<!-- ALL-CONTRIBUTORS-LIST:END -->

[SignPath Foundation]:https://signpath.org
[SignPath.io]:https://signpath.io
