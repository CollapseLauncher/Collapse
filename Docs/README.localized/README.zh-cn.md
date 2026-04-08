
<p align="center">
  <img width="512px" height="auto" src="https://raw.githubusercontent.com/CollapseLauncher/Collapse/main/Docs/Imgs/CollapseLauncherIdolType.png"/><br/>
  <i>I know, it's not a good one. But at least we made it lol</i>
  <i>~ neon-nyan</i>
  <img src="https://raw.githubusercontent.com/CollapseLauncher/.github/main/profile/NewBannerv2_Banner_color_20240615.webp"/>
</p>

##### 纳西妲背景图片来源：[Rafa](https://www.pixiv.net/en/users/3970196)

[![en](https://img.shields.io/badge/README-en-red.svg)](../../README.md)
[![de](https://img.shields.io/badge/README-de-red.svg)](README.de-de.md)
[![fr](https://img.shields.io/badge/README-fr-red.svg)](README.fr-fr.md)
[![id](https://img.shields.io/badge/README-id-red.svg)](README.id-id.md)
[![jp](https://img.shields.io/badge/README-jp-red.svg)](README.ja-jp.md)
[![pt](https://img.shields.io/badge/README-pt-red.svg)](README.pt-pt.md)
[![ru](https://img.shields.io/badge/README-ru-red.svg)](README.ru-ru.md)
[![zh](https://img.shields.io/badge/README-zh-red.svg)](README.zh-cn.md)

**Collapse** 最开始是为 **崩坏3** 设计的。不过，随着项目的发展，这个启动器现在已成为目前所有已发布的**米哈游**游戏的客户端.

[![Build-Canary](https://github.com/CollapseLauncher/Collapse/actions/workflows/build.yml/badge.svg?branch=main)](https://github.com/CollapseLauncher/Collapse/actions/workflows/build.yml)
[![Qodana](https://github.com/CollapseLauncher/Collapse/actions/workflows/qodana-scan.yml/badge.svg)](https://github.com/CollapseLauncher/Collapse/actions/workflows/qodana-scan.yml)
[![Sync to GitLab](https://github.com/CollapseLauncher/CollapseLauncher-ReleaseRepo/actions/workflows/sync-to-gitlab.yml/badge.svg)](https://github.com/CollapseLauncher/CollapseLauncher-ReleaseRepo/actions/workflows/sync-to-gitlab.yml)
[![Sync to Coding](https://github.com/CollapseLauncher/CollapseLauncher-ReleaseRepo/actions/workflows/sync-to-coding.yml/badge.svg)](https://github.com/CollapseLauncher/CollapseLauncher-ReleaseRepo/actions/workflows/sync-to-coding.yml)
[![Upload to R2](https://github.com/CollapseLauncher/CollapseLauncher-ReleaseRepo/actions/workflows/upload-to-r2.yml/badge.svg)](https://github.com/CollapseLauncher/CollapseLauncher-ReleaseRepo/actions/workflows/upload-to-r2.yml)
[![FOSSA Status](https://app.fossa.com/api/projects/git%2Bgithub.com%2FCollapseLauncher%2FCollapse.svg?type=shield&issueType=license)](https://app.fossa.com/projects/git%2Bgithub.com%2FCollapseLauncher%2FCollapse?ref=badge_shield&issueType=license)
[![FOSSA Status](https://app.fossa.com/api/projects/git%2Bgithub.com%2FCollapseLauncher%2FCollapse.svg?type=shield&issueType=security)](https://app.fossa.com/projects/git%2Bgithub.com%2FCollapseLauncher%2FCollapse?ref=badge_shield&issueType=security)


[![Localization](https://img.shields.io/badge/Localization-Transifex-blue)](https://explore.transifex.com/collapse-launcher/collapse-mainapp/)
[![Discord](https://img.shields.io/badge/Join_Community-Discord-5865F2)](https://discord.gg/vJd2exaS7j)
[![KanbanBoard](https://img.shields.io/badge/Kanban_Board-Trello-white)](https://trello.com/b/rsqrnn15/collapse-launcher-tracker)

<p align="center">
    <a href="https://explore.transifex.com/collapse-launcher/collapse-mainapp/" target="_blank">
       <img src="https://upload.wikimedia.org/wikipedia/commons/f/f7/Transifex_logo.svg" alt="Collapse Launcher Localization at Transifex" Width=280/>
	</a>
    &nbsp;
    <a href="https://discord.gg/vJd2exaS7j" target="_blank">
        <img src="https://discordapp.com/api/guilds/1116150300324139131/widget.png?style=banner2" alt="Discord Shield for Collapse Launcher Discord server" Width=280/>
    </a>
</p>

# 为什么是“Collapse”？
Collapse 启动器的名字源自 **崩坏** 的英文翻译，意味着它是一个替代版（增强版）的崩坏3启动器。

**Collapse** 包含有一些针对 **崩坏3** 的高级特性，不仅可以支持下载并启动游戏，还有一些官方启动器没有的额外功能，包括：
* 缓存下载（即游戏内的“更新设置”）。
* 游戏数据修复（比游戏内的*数据修复*功能需要更少的带宽）。
* 修改游戏设置（图形设置 & 声音设置）而无需开启游戏。
* 多区服游戏下载支持（允许下载并启动游戏的多个版本）。
* Steam 到全球版本转换（无需重新下载整个游戏）。
* 全球和东南亚版本相互转换。
* 多会话下载，加速游戏下载。
* 多线程解压，加速游戏安装。
* 用于小更新的 Delta Patch 更新机制（[**点我**](https://github.com/neon-nyan/CollapseLauncher/wiki/Update-Game-Region-with-Delta-Patch)查看更多信息）。
* 可选择图形 API（DirectX 11 (FL: 10.1, 11.0 and 11.1) 和 DirectX 12 [在较新阶段游戏可能会崩溃]）。

不仅如此，这款启动器还为 **原神** 提供了一些高级功能，包括但不限于：
* 在首次安装时选择配音语言，因此无需在游戏内下载。
* 在游戏外修复游戏文件，包括游戏缓存、动画、音频包和持久性文件。

# 支持的功能
> 标有 :warning: 的功能将被移除，且不再受 Collapse 开发团队的支持。

<table>
  <thead>
    <tr>
      <th rowspan="2">游戏名</th>
      <th rowspan="2">区服</th>
      <th colspan="7">功能</th>
    </tr>
    <tr>
      <th>安装</th>
      <th>预载</th>
      <th>更新</th>
      <th>游戏修复</th>
      <th>缓存更新</th>
      <th>游戏设置</th>
      <th>区服转换</th>
    </tr>
  </thead>
  <tbody>
    <tr>
      <td rowspan="6">崩坏3</td>
      <td>东南亚</td>
      <td>:white_check_mark:</td>
      <td>:white_check_mark:</td>
      <td>:white_check_mark:（常规和 Delta Patch 更新）</td>
      <td>:white_check_mark:</td>
      <td>:white_check_mark:</td>
      <td>:white_check_mark:</td>
      <td>:warning: <br>（从东南亚 -&gt; 全球）</td>
    </tr>
    <tr>
      <td>全球</td>
      <td>:white_check_mark:</td>
      <td>:white_check_mark:</td>
      <td>:white_check_mark:（常规和 Delta Patch 更新）</td>
      <td>:white_check_mark:</td>
      <td>:white_check_mark:</td>
      <td>:white_check_mark:</td>
      <td>:warning: <br>（从 Steam -&gt; 全球）<br>（从全球 -&gt; 东南亚）</td>
    </tr>
    <tr>
      <td>中国大陆</td>
      <td>:white_check_mark:</td>
      <td>:white_check_mark:</td>
      <td>:white_check_mark:</td>
      <td>:white_check_mark:</td>
      <td>:white_check_mark:</td>
      <td>:white_check_mark:</td>
    </tr>
    <tr>
      <td>港澳台</td>
      <td>:white_check_mark:</td>
      <td>:white_check_mark:</td>
      <td>:white_check_mark:</td>
      <td>:white_check_mark:</td>
      <td>:white_check_mark:</td>
      <td>:white_check_mark:</td>
    </tr>
    <tr>
      <td>日本</td>
      <td>:white_check_mark:</td>
      <td>:white_check_mark:</td>
      <td>:white_check_mark:</td>
      <td>:white_check_mark:</td>
      <td>:white_check_mark:</td>
      <td>:white_check_mark:</td>
    </tr>
    <tr>
      <td>韩国</td>
      <td>:white_check_mark:</td>
      <td>:white_check_mark:</td>
      <td>:white_check_mark:</td>
      <td>:white_check_mark:</td>
      <td>:white_check_mark:</td>
      <td>:white_check_mark:</td>
    </tr>
    <tr>
      <td rowspan="4">原神</td>
      <td>全球</td>
      <td>:white_check_mark:</td>
      <td>:white_check_mark:</td>
      <td>:white_check_mark:</td>
      <td>:white_check_mark:</td>
      <td>N/A</td>
      <td>:white_check_mark:</td>
    </tr>
    <tr>
      <td>中国大陆</td>
      <td>:white_check_mark:</td>
      <td>:white_check_mark:</td>
      <td>:white_check_mark:</td>
      <td>:white_check_mark:</td>
      <td>N/A</td>
      <td>:white_check_mark:</td>
    </tr>
    <tr>
      <td>哔哩哔哩</td>
      <td>:white_check_mark:</td>
      <td>:white_check_mark:</td>
      <td>:white_check_mark:</td>
      <td>:white_check_mark:</td>
      <td>N/A</td>
      <td>:white_check_mark:</td>
    </tr>
    <tr>
      <td>Google Play</td>
      <td>:white_check_mark:</td>
      <td>:white_check_mark:</td>
      <td>:white_check_mark:</td>
      <td>:white_check_mark:</td>
      <td>N/A</td>
      <td>:white_check_mark:</td>
    </tr>
    <tr>
      <td rowspan="3">崩坏：星穹铁道</td>
      <td>全球</td>
      <td>:white_check_mark:</td>
      <td>:white_check_mark:</td>
      <td>:white_check_mark:</td>
      <td>:white_check_mark:</td>
      <td>:white_check_mark:</td>
      <td>:white_check_mark:</td>
    </tr>
    <tr>
      <td>中国大陆</td>
      <td>:white_check_mark:</td>
      <td>:white_check_mark:</td>
      <td>:white_check_mark:</td>
      <td>:white_check_mark:</td>
      <td>:white_check_mark:</td>
      <td>:white_check_mark:</td>
    </tr>
    <tr>
      <td>哔哩哔哩</td>
      <td>:white_check_mark:</td>
      <td>:white_check_mark:</td>
      <td>:white_check_mark:</td>
      <td>:white_check_mark:</td>
      <td>:white_check_mark:</td>
      <td>:white_check_mark:</td>
    </tr>
   <tr>
      <td rowspan="2">绝区零</td>
      <td>全球</td>
      <td>:white_check_mark:</td>
      <td>:white_check_mark:</td>
      <td>:white_check_mark:</td>
      <td>N/A</td>
      <td>N/A</td>
      <td>:white_check_mark:</td>
    </tr>
    <tr>
      <td>中国大陆</td>
      <td>:white_check_mark:</td>
      <td>:white_check_mark:</td>
      <td>:white_check_mark:</td>
      <td>N/A</td>
      <td>N/A</td>
      <td>:white_check_mark:</td>
    </tr>
  </tbody>
</table>

> **注**:
> 上表展示了 Collapse 目前支持的功能。随着 Collapse 新功能的添加和新游戏的发布，此列表将不断更新。如果您在上述任何支持的功能使用上有任何问题，请检查我们的 GitHub issue 标签页，如果未找到相关活跃的 issue，请报告该问题。
> > 请注意，游戏转换功能目前仅适用于《崩坏3》。其他米哈游的游戏目前尚未计划进行游戏转换。

# 下载地址
目前有两个版本：稳定版和预览版。[**点击这里**](https://github.com/neon-nyan/CollapseLauncher/releases)查看所有版本。

# 系统需求
- 操作系统：**Windows 10 1809 Update (build 17763)** 及以后，或 **Windows 11（任意版本）**
- 架构：**x64/AMD64**
- 互联网接入：**是**

***

# 本地构建/开发需求
> ### 更多信息请参阅 [**贡献指南**](../../CONTRIBUTING.md)

***

# 代码签名政策
> 免费代码签名提供自 [SignPath.io]，证书来自 [SignPath 基金会]
- 该程序只会将用户数据传输到用户配置的数据库服务器。异常数据将发送到 [Sentry.io] 用于错误跟踪（用户可在应用设置中禁用该行为）。
- 阅读我们完整的[**隐私政策**](../../PRIVACY.md)
- 阅读我们的[**第三方声明**](../../THIRD_PARTY_NOTICES.md)查看我们使用的第三方库的协议。

# 本项目使用的第三方库
**免责声明**：本项目与 [**miHoYo (miHoYo Co., Ltd.)**](https://www.mihoyo.com/) 以及 [**HoYoverse (COGNOSPHERE PTE. LTD.)**](https://www.hoyoverse.com/en-us) **没有任何关系**，并完全开放源代码。我们欢迎任何贡献！😃
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

# UI 设计概览
![](https://raw.githubusercontent.com/neon-nyan/CollapseLauncher-Page/main/images/UI%20Overview%20RC2.webp)

# 如何支持该项目？
支持从来不是一种义务，但我们始终心存感激，并会激励我们投入更多时间在该项目上，使这个项目和其他项目得以继续。为此，如果您决定支持我们，可以通过以下方式 :smile::
- **[GitHub Sponsors](https://github.com/sponsors/neon-nyan)**
- **[PayPal](https://paypal.me/neonnyan)**

由全世界所有舰长用❤️制作。为世界上所有的美好而战！

[SignPath 基金会]:https://signpath.org
[SignPath.io]:https://signpath.io
[Sentry.io]:https://sentry.io
