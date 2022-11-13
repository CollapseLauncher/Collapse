<p align="center">
  <img src="https://raw.githubusercontent.com/neon-nyan/CollapseLauncher-Page/main/images/Banner20220719.webp"/>
</p>

**Collapse** was firstly made specifically for **Honkai Impact 3rd**. But since the progress of this project, this launcher is now an advanced launcher for **miHoYo Games**.

# Why called Collapse?
Collapse was came from **Honkai Impact** name in Chinese and Japanese. The word came from [**崩坏**] or **Bēng huài** in Chinese and also [**崩壊**] or **Houkai** in Japanese. Both means "**Collapse**" and that's why we are choosing this as our launcher name and since this launcher was supposed to be an alternative launcher for Honkai Impact 3rd in the first place.

**Collapse** has some advanced features for **Honkai Impact 3rd** that not only provides you a feature to download and launch the game, but also some advanced features that the Official Launcher doesn't have, including:
* Caches Download (or aka. "Updating Settings" in-game).
* Game Data Repair (with less data needed than Data Repair feature in-game).
* Changing Game Settings (like: Graphics Settings and Audio Settings) without opening the game.
* Multi-Region Game Download (Supporting: Southeast Asia, Global, TW/HK/MO, Mainland China and Korea version).
* Steam to Global version conversion (without re-downloading the whole game).
* Global to SEA version (or vice versa) conversion.
* Multi-session Download for Game Download/Installation.
* Multi-thread Game Extraction for faster installation.
* Delta-Patch Update Mechanism for smaller update ([**Click here**](https://github.com/neon-nyan/CollapseLauncher/wiki/Update-Game-Region-with-Delta-Patch) for more info).
* Ability to select Graphics API (DirectX 11 (FL: 10.1, 11.0 and 11.1) and DirectX 12 [May crash the game in newer stages]).

Not only that, this launcher has also some advanced features for **Genshin Impact** like:
* Choosing your voice-line language at first-install so you don't have to download it inside the game.
* Repair the game files entirely including game caches, cutscenes, audio pack and persistent.

# Supported Game Client
For now, this launcher is only supporting these versions of the game:
* Southeast Asia
* Global
* TW/HK/MO
* Mainland China
* Korea
* Japanese (in Preview Build)

# Other Supported Game Client
This launcher also supports other game client, including:
* Genshin Impact (Global Release)
* 原神 / Yuan Shen (Mainland China's Genshin)
* ~~Honkai: Star Rail~~ (Waiting for Official Release)
* ~~Zenless Zone Zero~~ (Waiting for Official Release)

Some features like Game Data Repair and Preload Download are available for this game but other things like Game Settings and Caches Download aren't available for this Game Client.

# Download Ready-to-use build
[<img src="https://user-images.githubusercontent.com/30566970/172445052-b0e62327-1d2e-4663-bc0f-af50c7f23615.svg" width="320"/>](https://github.com/neon-nyan/CollapseLauncher/releases/download/CL-v1.0.52.8-stable/CL-1.0.52.8_Installer.exe)
> **Note**: The version for this build is 1.0.52.8 (Released on: November 10th, 2022)

[<img src="https://user-images.githubusercontent.com/30566970/172445153-d098de0d-1236-4124-8e13-05000b374eb6.svg" width="320"/>](https://github.com/neon-nyan/CollapseLauncher/releases/download/CL-v1.0.53.0-pre/CL-1.0.53.0-preview_Installer.exe)
> **Note**: The version for this build is 1.0.53.0 (Released on: November 13rd, 2022).

To see all releases, [**go here**](https://github.com/neon-nyan/CollapseLauncher/releases).

# Prequesties for Ready-to-use build
- OS: **Windows 10 1809 Update (build 17763)** and later or **Windows 11 (Any builds)**
- Architecture: **x64/AMD64**
- Internet Access: **Yes**

***

# Prequesties for self-build/development environment
To develop and build it for yourself, you need to install [**.NET Core 7 SDK (7.0.100 or later)**](https://dotnet.microsoft.com/en-us/download/dotnet/7.0), [**WinUI 3 (WindowsAppSDK 1.2 Stable Runtime)**](https://aka.ms/windowsappsdk/1.2/latest/windowsappruntimeinstall-x64.exe) and **Windows 10 SDK (10.0.19043.0 or later)** via Visual Studio Installer.

> **Note**:
> 
> Starting from November 13<sup>rd</sup> 2022, you must have Visual Studio 2022 installed on your computer due to minimum requirement of WinUI 3 1.2 Stable release.
> 
> Using lower Visual Studio version (like VS2019) is possible, but you have to downgrade the **WindowsAppSDK** via NuGet to **WindowsAppSDK 1.1.5** or **WindowsAppSDK 1.2-preview2** before building.

You may also need **Visual Studio 2022** or later with **WindowsAppSDK 1.2 Stable C# Extension** installed.
Please refer to this link to download the extension for your Visual Studio version
- [**Visual Studio 2022 C# Extension**](https://aka.ms/windowsappsdk/1.2/latest/WindowsAppSDK.Cs.Extension.Dev17.Standalone.vsix)

# Third-party repositories and references being used in this project
- [**Windows UI Library**](https://github.com/microsoft/microsoft-ui-xaml) by Microsoft
- [**HDiffPatch**](https://github.com/sisong/HDiffPatch) by housisong
- [**Color Thief .NET**](https://github.com/KSemenenko/ColorThief) by KSemenenko
- [**SevenZipExtractor**](https://github.com/neon-nyan/SevenZipExtractor) by adoconnection
- [**Newtonsoft.Json**](https://github.com/JamesNK/Newtonsoft.Json) by JamesNK
- [**Hi3HelperCore.Http**](https://github.com/neon-nyan/Hi3HelperCore.Http) by neon-nyan

**Disclaimer**: This project **IS NOT AFFILIATED** with [**miHoYo (miHoYo Co., Ltd.)**](https://www.mihoyo.com/) or [**HoYoverse (COGNOSPHERE PTE. LTD.)**](https://www.hoyoverse.com/en-us) by any means and completely open-sourced and made with love by Kanchous'. Any contributions are welcomed! 😃

# UI Design Overview
![](https://raw.githubusercontent.com/neon-nyan/CollapseLauncher-Page/main/images/UI%20Overview%20RC2.webp)

# How to support us?
We are now opening sponsorship through **GitHub Sponsors**, **DANA (Indonesian-only Digital Wallet)**, and **PayPal** to keep this and other projects alive.
By giving us your support, hope we could keep the server used for the API of this and other projects maintained and make more improvements for this project overtime! :smile:
