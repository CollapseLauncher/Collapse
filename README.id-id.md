
<p align="center">
  <img src="https://raw.githubusercontent.com/neon-nyan/CollapseLauncher-Page/main/images/NewBannerv2_color.webp"/>
</p>

##### Credit latar belakang Nahida oleh: [Rafa di Pixiv](https://www.pixiv.net/en/users/3970196)

[![en](https://img.shields.io/badge/lang-en-red.svg)](https://github.com/neon-nyan/Collapse/blob/main/README.md) [![jp](https://img.shields.io/badge/lang-jp-red.svg)](https://github.com/neon-nyan/Collapse/blob/main/README.ja-jp.md)

Pada saat pertama kali diciptakan, **Collapse** ditujukan hanya untuk **Honkai Impact 3rd**. Namun seiring dengan berjalannya waktu dan kemajuan dalam project ini, akhirnya launcher ini dapat digunakan untuk semua **Game miHoYo** yang saat ini sudah tersedia.

[![Build-Canary](https://github.com/neon-nyan/Collapse/actions/workflows/build.yml/badge.svg?branch=main)](https://github.com/neon-nyan/Collapse/actions/workflows/build.yml)
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
    &nbsp;
    <a href="https://trello.com/b/rsqrnn15/collapse-launcher-tracker" target="_blank">
        <img src="https://cdn.discordapp.com/attachments/593053443761897482/1137795596191797318/logo-gradient-white-trello.svg" alt="Collapse Launcher Trello board" Height=66/>
    </a>
</p>

# Mengapa "Collapse"?
Kata "**Collapse**" berasal dari terjemahan **Honkai Impact** dalam bahasa Mandarin dan Jepang. Kata "**Collapse**" datang dari sebuah kata [**崩坏**] atau **Bēng huài** dalam bahasa Mandarin dan juga [**崩壊**] atau **Houkai** dalam bahasa Jepang. Keduanya memiliki arti yang sama, yaitu "**Collapse**". Sehingga dengan demikian, kami memilih kata tersebut sebagai nama yang menginspirasi untuk launcher kami dengan tujuan sebagai launcher alternatif (yang lebih canggih) untuk *Honkai Impact 3rd* pada awalnya.

**Collapse** memiliki beberapa fitur canggih untuk **Honkai Impact 3rd** yang tidak hanya mampu untuk mengunduh dan menjalankan game, namun juga memiliki beberapa fitur tambahan yang dimana Launcher Official tidak punya, salah satunya:
* Pengunduhan Cache (aka "Updating Settings" di dalam game).
* Perbaikan Data Game (hanya memerlukan bandwidth yang lebih kecil ketimbang fitur *Perbaikan Data* di dalam game).
* Mengubah Pengaturan Game (Pengaturan Grafik & Audio) tanpa harus membuka game terlebih dahulu.
* Dukungan Unduhan Game secara Multi-Region (Dapat mengunduh dan menjalankan versi game yang berbeda).
* Konversi versi Steam ke Global (tanpa mengunduh ulang game secara keseluruhan).
* Konversi versi Global ke SEA (atau sebaliknya).
* Pengunduhan Multi-sesi untuk pengunduhan dan instalasi yang lebih cepat.
* Instalasi menggunakan Multi-thread untuk instalasi game yang lebih cepat.
* Pembaruan menggunakan *Delta-Patch* untuk ukuran pembaruan yang lebih kecil ([**Klik di sini** [Dalam Bahasa Inggris]](https://github.com/neon-nyan/CollapseLauncher/wiki/Update-Game-Region-with-Delta-Patch) untuk informasi lebih lanjut).
* Kemampuan untuk memilih *Graphics API* (DirectX 11 (FL: 10.1, 11.0 and 11.1) dan DirectX 12 [Kemungkinan akan menyebabkan crash pada stage baru]).

Tidak hanya itu. Launcher ini juga memiliki beberapa fitur canggih untuk **Genshin Impact**, salah satunya adalah:
* Memilih bahasa *voice-line* saat instalasi game untuk pertama kalinya sehingga kamu tidak perlu mengunduhnya lagi di dalam game.
* Memperbaiki file pada game termasuk: Game Caches, Cutscenes, Audio Pack & persistent files di luar dari game.

# Fitur yang Didukung
<table>
  <thead>
    <tr>
      <th rowspan="2">Judul Game</th>
      <th rowspan="2">Region</th>
      <th colspan="7">Fitur-fitur</th>
    </tr>
    <tr>
      <th>Instalasi</th>
      <th>Pre-load</th>
      <th>Pembaruan</th>
      <th>Perbaikan Game</th>
      <th>Pembaruan Cache</th>
      <th>Pengaturan Game</th>
      <th>Konversi Region Game</th>
    </tr>
  </thead>
  <tbody>
    <tr>
      <td rowspan="6">Honkai Impact 3rd</td>
      <td>Southeast Asia</td>
      <td>:white_check_mark:</td>
      <td>:white_check_mark:</td>
      <td>:white_check_mark: (Pembaruan dengan metode Normal dan Delta-patch tersedia)</td>
      <td>:white_check_mark:</td>
      <td>:white_check_mark:</td>
      <td>:white_check_mark:</td>
      <td>:white_check_mark: <br> (Dari SEA -&gt; Global) </td>
    </tr>
    <tr>
      <td>Global</td>
      <td>:white_check_mark:</td>
      <td>:white_check_mark:</td>
      <td>:white_check_mark: (Pembaruan dengan metode Normal dan Delta-patch tersedia)</td>
      <td>:white_check_mark:</td>
      <td>:white_check_mark:</td>
      <td>:white_check_mark:</td>
      <td>:white_check_mark: <br> (Dari Steam -&gt; Global) <br> (Dari Global -&gt; SEA) </td>
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
      <td rowspan="2">Genshin Impact</td>
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
      <td rowspan="2">Honkai: Star Rail</td>
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
  </tbody>
</table>

> **Catatan**:
> Tabel di atas bermaksud untuk menggambarkan fitur yang saat ini didukung oleh **Collapse**. Daftar-daftar yang tercantum pada tabel akan terus diperbarui seiring dengan adanya pembaruan fitur dan game yang akan ditambahkan kedepannya. Apabila kamu memiliki masalah dengan fitur yang sudah disebutkan di atas, periksa halaman "**Issue**" kami atau silahkan laporkan pada halaman "**Issue**" apabila issue yang sama tidak ditemukan.
> > Mohon diingat bahwa fitur konversi game saat ini hanya didukung untuk Honkai Impact 3rd. Kami saat ini tidak merencanakan dukungan ini untuk game lainnya dari miHoYo/Cognosphere Ptr. Ltd.

# Unduh Collapse Sekarang!
Untuk mengunduh versi terbaru yang tersedia pada Collapse, silahkan pergi ke halaman [**Rilis**](https://github.com/neon-nyan/CollapseLauncher/releases) berikut.

# Persyaratan Sistem yang Dibutuhkan
- OS: **Windows 10 1809 Update (Build 17763)** atau di atasnya / **Windows 11 (Build apa saja)**
- Arsitektur: **x64/AMD64**
- Akses Internet: **Ya**

***

# Persyaratan untuk *Building/Development* secara Local
### Informasi mengenai hal ini dapat ditemukan pada halaman [**Panduan Kontribusi** [Dalam Bahasa Inggris]](https://github.com/neon-nyan/Collapse/blob/main/CONTRIBUTING.md) berikut.

***

# Repositori Pihak Ketiga dan Librari yang Digunakan pada Project Ini
- [**Windows UI Library**](https://github.com/microsoft/microsoft-ui-xaml) oleh Microsoft
- [**Windows App SDK**](https://github.com/microsoft/WindowsAppSDK) oleh Microsoft
- [**HDiffPatch**](https://github.com/sisong/HDiffPatch) oleh housisong
- [**Color Thief .NET**](https://github.com/neon-nyan/ColorThief) oleh KSemenenko
- [**SevenZipExtractor**](https://github.com/neon-nyan/SevenZipExtractor) oleh adoconnection
- [**Hi3Helper.Http**](https://github.com/neon-nyan/Hi3Helper.Http) oleh neon-nyan
- [**Hi3Helper.EncTool**](https://github.com/neon-nyan/Hi3Helper.EncTool) oleh neon-nyan
- [**Crc32.NET**](https://github.com/neon-nyan/Crc32.NET) oleh force-net
- [**UABT**](https://github.com/neon-nyan/UABT) oleh _tidak diketahui_

**Sanggahan**: Project ini **TIDAK TERAFILIASI** dengan [**miHoYo (miHoYo Co., Ltd.)**](https://www.mihoyo.com/) atau [**HoYoverse (COGNOSPHERE PTE. LTD.)**](https://www.hoyoverse.com/en-us) dengan cara apapun dan seluruhnya Open-Source. Kontribusi apapun dipersilahkan! 😃

# Desain Antarmuka
![](https://raw.githubusercontent.com/neon-nyan/CollapseLauncher-Page/main/images/UI%20Overview%20RC2.webp)

# Bagaimana Caranya untuk Mendukung Project Ini?
Kami tidak memaksa kamu untuk selalu mendukung project ini namun bentuk dukungan apapun yang kamu berikan kepada kami sangatlah berharga dan menjadi motivasi kami untuk memberikan banyak peluang untuk project ini dan tentunya, membuat project ini terus hidup! Kamu dapat mendukung kami dengan cara berikut 😃:
- **[GitHub Sponsors](https://github.com/sponsors/neon-nyan)**
- **[QRIS (Quick Response Code Indonesian Standard)](https://qris.id/homepage/)** (Coming Soon:tm:)
- **[PayPal](https://paypal.me/neonnyan)**

Dibuat oleh seluruh kapten di penjuru dunia dengan **Cinta**❤️.<br/>
**_Teruslah berjuang untuk seluruh keindahan di dunia ini!_** (*Fight for all that is beautiful in this world!*)