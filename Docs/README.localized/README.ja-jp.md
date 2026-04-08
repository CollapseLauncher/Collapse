<p align="center">
  <img src="https://raw.githubusercontent.com/neon-nyan/CollapseLauncher-Page/main/images/NewBannerv2_color.webp"/>
</p>

##### ヘッダー背景画像クレジット: [Rafa](https://www.pixiv.net/artworks/102448848)

[![en](https://img.shields.io/badge/README-en-red.svg)](../../README.md)
[![de](https://img.shields.io/badge/README-de-red.svg)](README.de-de.md)
[![fr](https://img.shields.io/badge/README-fr-red.svg)](README.fr-fr.md)
[![id](https://img.shields.io/badge/README-id-red.svg)](README.id-id.md)
[![jp](https://img.shields.io/badge/README-jp-red.svg)](README.ja-jp.md)
[![pt](https://img.shields.io/badge/README-pt-red.svg)](README.pt-pt.md)
[![ru](https://img.shields.io/badge/README-ru-red.svg)](README.ru-ru.md)
[![zh](https://img.shields.io/badge/README-zh-red.svg)](README.zh-cn.md)

**Collapse**は**崩壊3rd**のために開発されたPC用非公式ランチャーです。プロジェクトは進化を続け、現在は全ての**miHoYo/HoYoverse**のゲームに対応しています.

[![Build-Canary](https://github.com/neon-nyan/Collapse/actions/workflows/build.yml/badge.svg?branch=main)](https://github.com/neon-nyan/Collapse/actions/workflows/build.yml)
[![Sync to Bitbucket](https://github.com/neon-nyan/CollapseLauncher-ReleaseRepo/actions/workflows/sync-to-bitbucket.yml/badge.svg)](https://github.com/neon-nyan/CollapseLauncher-ReleaseRepo/actions/workflows/sync-to-bitbucket.yml)
[![Upload to R2](https://github.com/neon-nyan/CollapseLauncher-ReleaseRepo/actions/workflows/upload-to-r2.yml/badge.svg)](https://github.com/neon-nyan/CollapseLauncher-ReleaseRepo/actions/workflows/upload-to-r2.yml)


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
    &nbsp;
    <a href="https://trello.com/b/rsqrnn15/collapse-launcher-tracker" target="_blank">
        <img src="https://cdn.discordapp.com/attachments/593053443761897482/1137795596191797318/logo-gradient-white-trello.svg" alt="Collapse Launcher Trello board" Height=66/>
    </a>
</p>

READMEとアプリケーションの日本語翻訳者：[Vermilion-Shinsha](https://github.com/Vermilion-Sinsha)

# Collapseって？
Collapseは崩壊3rdのタイトルの一部である、「**崩壊**」という言葉の英訳から名付けられています。このランチャーは崩壊3rdPC版公式ランチャーの発展（強化）版を目指して開発されたため、このような名前が付いています。

**Collapse**には**崩壊3rd**をダウンロード・起動する機能に加えて、公式ランチャーにはない高度な機能が搭載されています。
* キャッシュのダウンロード（ゲーム内では「データ更新」と呼ばれています）
* ゲームデータ修復（公式ランチャーの「修復」よりも必要な通信量が軽い）
* ゲームを起動しなくても画質＆音量設定が変更可能
* 海外版クライアントを簡単にダウンロード＆管理
* グローバルSteam版クライアントをグローバルPC版クライアントへ変換 **（注意：日本のSteam版は非対応です！！！）**
* グローバル版をSEA版に変換（逆もOK）
* マルチセッションダウンロードでダウンロード/インストールを高速化
* マルチスレッド解凍でインストールを高速化
* デルタパッチ形式でアップデートパッチを軽量化（詳細は[**こちら**](https://github.com/neon-nyan/CollapseLauncher/wiki/Update-Game-Region-with-Delta-Patch)）
* グラフィックスAPIを選択可能（DirectX 11 (FL: 10.1, 11.0 and 11.1)とDirectX 12 [新しめのステージでゲームがクラッシュする可能性あり]）

さらに、**原神**のための高度な機能も搭載されています。
* 初回インストール時にボイス言語設定を選択可能、音声パックを後からダウンロードする手間を省きます
* キャッシュ、ムービー、音声パックや永続ファイルをゲームを起動せずに修復可能

などなど……

# サポートされている機能 
<table>
  <thead>
    <tr>
      <th rowspan="2">タイトル</th>
      <th rowspan="2">サーバー</th>
      <th colspan="7">機能</th>
    </tr>
    <tr>
      <th>インストール</th>
      <th>事前ダウンロード</th>
      <th>アップデート</th>
      <th>ゲーム修復</th>
      <th>キャッシュ更新</th>
      <th>ゲーム設定</th>
      <th>サーバー移動</th>
    </tr>
  </thead>
  <tbody>
    <tr>
      <td rowspan="6">崩壊3rd</td>
      <td>SEA版</td>
      <td>:white_check_mark:</td>
      <td>:white_check_mark:</td>
      <td>:white_check_mark: (通常＆デルタパッチ両対応)</td>
      <td>:white_check_mark:</td>
      <td>:white_check_mark:</td>
      <td>:white_check_mark:</td>
      <td>:white_check_mark: <br> (SEA版→グローバル版) </td>
    </tr>
    <tr>
      <td>グローバル版</td>
      <td>:white_check_mark:</td>
      <td>:white_check_mark:</td>
      <td>:white_check_mark: (通常＆デルタパッチ両対応)</td>
      <td>:white_check_mark:</td>
      <td>:white_check_mark:</td>
      <td>:white_check_mark:</td>
      <td>:white_check_mark: <br> (Steam版→グローバル版) <br> (グローバル版→SEA版) </td>
    </tr>
    <tr>
      <td>中国大陸版</td>
      <td>:white_check_mark:</td>
      <td>:white_check_mark:</td>
      <td>:white_check_mark:(通常＆デルタパッチ両対応)</td>
      <td>:white_check_mark:</td>
      <td>:white_check_mark:</td>
      <td>:white_check_mark:</td>
      <td>N/A</td>
    </tr>
    <tr>
      <td>台湾/香港/マカオ版</td>
      <td>:white_check_mark:</td>
      <td>:white_check_mark:</td>
      <td>:white_check_mark:(通常＆デルタパッチ両対応)</td>
      <td>:white_check_mark:</td>
      <td>:white_check_mark:</td>
      <td>:white_check_mark:</td>
      <td>N/A</td>
    </tr>
    <tr>
      <td>日本版</td>
      <td>:white_check_mark:</td>
      <td>:white_check_mark:</td>
      <td>:white_check_mark:(通常＆デルタパッチ両対応)</td>
      <td>:white_check_mark:</td>
      <td>:white_check_mark:</td>
      <td>:white_check_mark:</td>
      <td>N/A</td>
    </tr>
    <tr>
      <td>韓国版</td>
      <td>:white_check_mark:</td>
      <td>:white_check_mark:</td>
      <td>:white_check_mark:(通常＆デルタパッチ両対応)</td>
      <td>:white_check_mark:</td>
      <td>:white_check_mark:</td>
      <td>:white_check_mark:</td>
      <td>N/A</td>
    </tr>
    <tr>
      <td rowspan="2">原神</td>
      <td>グローバル版</td>
      <td>:white_check_mark:</td>
      <td>:white_check_mark:</td>
      <td>:white_check_mark:</td>
      <td>:white_check_mark:</td>
      <td>N/A</td>
      <td>:white_check_mark:</td>
      <td>N/A</td>
    </tr>
    <tr>
      <td>中国大陸版</td>
      <td>:white_check_mark:</td>
      <td>:white_check_mark:</td>
      <td>:white_check_mark:</td>
      <td>:white_check_mark:</td>
      <td>N/A</td>
      <td>:white_check_mark:</td>
      <td>N/A</td>
    </tr>
    <tr>
      <td rowspan="2">崩壊：スターレイル</td>
      <td>グローバル版</td>
      <td>:white_check_mark:</td>
      <td>:white_check_mark:</td>
      <td>:white_check_mark:</td>
      <td>:white_check_mark:</td>
      <td>:white_check_mark:</td>
      <td>:white_check_mark:</td>
      <td>N/A</td>
    </tr>
    <tr>
      <td>中国大陸版</td>
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

> **注意**:
> 上記の表は、Collapseが現在サポートしている機能の一覧です。Collapseに新機能が追加されたり、新しいゲームがリリースされたりする度に、継続的に更新されます。上記のサポートされている機能に問題が発生した場合、GitHubのissueタブで既に報告されているか確認してください。
> > サーバー移動機能は英語の崩壊3rd（SEA＆グローバル）にのみ対応しています。その他のサーバーや崩壊3rd以外のmiHoYo/Cognosphere Pte. Ltd.のゲームには、同機能の実装予定はありません。

# ダウンロード
安定版とプレビュー版の2種類が存在します。[**Releases**](https://github.com/neon-nyan/CollapseLauncher/releases)から最新版をチェックしてください。

# 動作環境
- OS: **Windows 10 1809 Update (build 17763)** 以降 / **Windows 11 (全ビルド)**
- アーキテクチャ: **x64/AMD64**
- インターネット接続: **オンライン**

***

# ローカルビルド/開発の条件
> ### 詳細は[**コントリビューションガイドライン**](https://github.com/neon-nyan/Collapse/blob/main/CONTRIBUTING.md)を参照してください。

***

# 使用されているサードパーティ製リポジトリ・ライブラリ
- [**Windows UI Library**](https://github.com/microsoft/microsoft-ui-xaml) by Microsoft
- [**Windows App SDK**](https://github.com/microsoft/WindowsAppSDK) by Microsoft
- [**HDiffPatch**](https://github.com/sisong/HDiffPatch) by housisong
- [**Color Thief .NET**](https://github.com/neon-nyan/ColorThief) by KSemenenko
- [**SevenZipExtractor**](https://github.com/neon-nyan/SevenZipExtractor) by adoconnection
- [**Hi3Helper.Http**](https://github.com/neon-nyan/Hi3Helper.Http) by neon-nyan
- [**Hi3Helper.EncTool**](https://github.com/neon-nyan/Hi3Helper.EncTool) by neon-nyan
- [**Crc32.NET**](https://github.com/neon-nyan/Crc32.NET) by force-net
- [**UABT**](https://github.com/neon-nyan/UABT) by _unknown_

**免責事項**：Collapseは[**miHoYo (miHoYo Co., Ltd.)**](https://www.mihoyo.com/)および[**HoYoverse (COGNOSPHERE PTE. LTD.)**](https://www.hoyoverse.com/en-us)とは**全く何の関係もない**非公式プロジェクトです。完全にオープンソースです。どんな貢献も歓迎します！😃

# UIプレビュー
![](https://raw.githubusercontent.com/neon-nyan/CollapseLauncher-Page/main/images/UI%20Overview%20RC2.webp)

# プロジェクトを支援したい時は
支援は決して義務ではありませんが、いつでも感謝し、プロジェクトへより多くの時間を掛けたり、当プロジェクトや他プロジェクトを存続させるモチベーションになります。私たちを支援したい場合は、これらの方法があります😄
- **[GitHub Sponsors](https://github.com/sponsors/neon-nyan)**
- **[PayPal](https://paypal.me/neonnyan)**

世界中の艦長に❤️を捧げます。美しい世界を守るために戦うよ！
