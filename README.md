# Attention
I'm sorry to tell you but the development for stable release of this project will be suspended while we are still waiting for WinUI 3 1.1 to come out this 22H1.
I will be releasing some of preview builds but there might be some breaking changes that made this project unstable. I hope that I can address this after WinUI 3 1.1 become stable in this first half of 2022 (22H1).
Thank you for your patience and cooperation.

<p align="Center">
  <img width="200px" src="https://user-images.githubusercontent.com/30566970/157742052-603a7fd8-1894-4af4-bebc-356a528c10ab.svg">
</p>

# Collapse Launcher
### An Advanced Launcher for Honkai Impact 3rd

<p align="Left">
  <img width="900px" alt="image" src="https://user-images.githubusercontent.com/30566970/157757321-b97f23c0-cd8b-4176-9bda-099f756c7d72.png">
</p>

> A Screenshot taken from Collapse Launcher v1.0.9-preview

Collapse Launcher is a launcher specifically made for Honkai Impact 3rd. This launcher not only provides you a feature to download and launch the game, but also some advanced features that the Official Launcher doesn't have, including:
* Caches Download (or aka. "Updating Settings" in-game).
* Game Data Repair (with less data needed than Data Repair feature in-game).
* Changing Game Settings (like: Graphics Settings and Audio Settings) without opening the game.
* Cutscenes Download and Subtitle Fix feature (Coming Soon).
* Multi-Region Game Download (supporting: Southeast Asia, Global, TW/HK/MO, Mainland China and Korea version).
* Steam to Global version conversion (without re-downloading the whole game).
* Enabling Game Mirror Server to be used for game (Powered by: Hi3Mirror)(Coming Soon).
* Multi-session Download for Game Download/Installation.
* Multi-thread Game Extraction for faster installation.
* Ability to select Graphics API (DirectX 11 (FL: 10.1, 11.0 and 11.1) and DirectX 12 [May crash the game in newer stages]).

This launcher requires [**.NET Core 6 Desktop Runtime**](https://dotnet.microsoft.com/en-us/download/dotnet/thank-you/runtime-desktop-6.0.3-windows-x64-installer) and [**WinUI 3 (WindowsAppSDK Runtime 1.0)**](https://github.com/neon-nyan/CollapseLauncher/releases/download/CL-v1.0.7-pre/prequesties-20220204.7z) in order to run.

Unfortunately, this launcher only supports x64-bit version of Windows 10 (Build 1809 or later) or Windows 11 (Any Build) due to WinUI 3 limitation. ([**Read More Here**](https://microsoft.github.io/microsoft-ui-xaml/about.html))
 
# Supported Game Client
For now, this launcher only supports these versions of the game:
* Southeast Asia
* Global
* TW/HK/MO
* Mainland China
* Korea

# Other Supported Game Client
This launcher also supports other game client, including:
* Genshin Impact (Global Release)
* 原神 / Yuan Shen (Mainland China's Genshin)

Unfortunately, some features like Game Data repair, Game Settings, Game Mirror Server and Caches Download aren't available for this Game Client.

Any contribution in this project are welcome.

# Third-party repositories and references being used on this project
- [**Windows UI Library**](https://github.com/microsoft/microsoft-ui-xaml) by Microsoft
- [**HDiffPatch**](https://github.com/sisong/HDiffPatch) by housisong
- [**Color Thief .NET**](https://github.com/KSemenenko/ColorThief) by KSemenenko
- ~~[**managed-lzma**](https://github.com/weltkante/managed-lzma) by weltkante~~
  - Deprecated (Will be replaced by [**SevenZipExtractor**](https://github.com/neon-nyan/SevenZipExtractor) by adoconnection soon)
- ~~[**SharpCompress**](https://github.com/adamhathcock/sharpcompress) by adamhathcock~~
  - Deprecated (Will be replaced by [**SevenZipExtractor**](https://github.com/neon-nyan/SevenZipExtractor) by adoconnection soon)
- [**HSharp**](https://github.com/Anduin2017/HSharp) by Anduin2017/Aiursoft
- [**Newtonsoft.Json**](https://github.com/JamesNK/Newtonsoft.Json) by JamesNK
