<p align="center">
  <img src="https://raw.githubusercontent.com/neon-nyan/CollapseLauncher-Page/main/images/NewBanner2022.webp"/>
</p>

> Art used for light mode by: [**Rafa** (@Pixiv)](https://www.pixiv.net/en/artworks/102448848)

**Collapse** was originally designed for **Honkai Impact 3rd**. However, as the project evolved, this launcher is now a game client for all currently released **miHoYo Games**.

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

# Supported Game Regions
The launcher currently supports the following regions for Honkai Impact 3rd:
* Southeast Asia
* Global
* TW/HK/MO
* Mainland China
* Korea
* Japanese

# Supported Game Clients
This launcher also supports other game client, including:
* Genshin Impact (Global Release)
* 原神 / Yuan Shen (Mainland China's Genshin Impact)
* ~~Honkai: Star Rail~~ (Waiting for Official Release)
* ~~Zenless Zone Zero~~ (Waiting for Official Release)

> **Note**:
> Some features such as Game Data Repair and Preloading are available for Genshin Impact, but others such as Game Settings modification and Caches Download are not available and presently unsupported.

# Download Ready-To-Use Build
[<img src="https://user-images.githubusercontent.com/30566970/172445052-b0e62327-1d2e-4663-bc0f-af50c7f23615.svg" width="320"/>](https://github.com/neon-nyan/CollapseLauncher/releases/download/CL-v1.0.55.6-stable/CL-1.0.55.6_Installer.exe)
> **Note**: The version for this build is `1.0.55.6` (Released on: January 19th, 2023)

[<img src="https://user-images.githubusercontent.com/30566970/172445153-d098de0d-1236-4124-8e13-05000b374eb6.svg" width="320"/>](https://github.com/neon-nyan/CollapseLauncher/releases/download/CL-v1.0.64.0-pre/CL-1.0.64.0-preview_Installer.exe)
> **Note**: The version for this build is `1.0.64.0` (Released on: February 6th, 2023).

To view all releases, [**click here**](https://github.com/neon-nyan/CollapseLauncher/releases).

# System Requirements for Ready-to-use build
- OS: **Windows 10 1809 Update (build 17763)** or later / **Windows 11 (Any builds)**
- Architecture: **x64/AMD64**
- Internet Access: **Yes**

***

# Prerequisites for Building Locally/Development
*Collapse* is presently powered by .NET 7 and as such, the packages listed below are required to create a local and development build of the launcher. Furthermore, *Collapse* uses many submodules and packages outside of this, which will automatically be loaded when the user sets up a local environment of the application.
- .NET: [**.NET Core 7 SDK (7.0.100 or later)**](https://dotnet.microsoft.com/en-us/download/dotnet/7.0)
- WinUI 3: [**WinUI 3 (WindowsAppSDK 1.2 Stable Runtime)**](https://aka.ms/windowsappsdk/1.2/latest/windowsappruntimeinstall-x64.exe)
- WindowsAppSDK 1.2 Stable C# Extension: [**Visual Studio 2022 C# Extension**](https://aka.ms/windowsappsdk/1.2/latest/WindowsAppSDK.Cs.Extension.Dev17.Standalone.vsix)
- **Windows 10 SDK (10.0.19043.0 or later)** via Visual Studio Installer

> **Note**:
> 
> Starting from November 13<sup>rd</sup> 2022, you <b>must</b> have Visual Studio 2022 installed on your computer due to the updated minimum system requirement of `WinUI 3 1.2 Stable`.
> 
> Using a lower Visual Studio version (like VS2019) is possible, but it is not recommended as you need to downgrade **WindowsAppSDK** via *NuGet* to **WindowsAppSDK 1.1.5** or **WindowsAppSDK 1.2-preview2** before building. This has an increased risk of breaking the application and as such, minimal support will be provided for this method. **This is not recommended for beginner users.**

# Third-party repositories and libraries used in this project
- [**Windows UI Library**](https://github.com/microsoft/microsoft-ui-xaml) by Microsoft
- [**HDiffPatch**](https://github.com/sisong/HDiffPatch) by housisong
- [**Color Thief .NET**](https://github.com/KSemenenko/ColorThief) by KSemenenko
- [**SevenZipExtractor**](https://github.com/neon-nyan/SevenZipExtractor) by adoconnection
- [**Newtonsoft.Json**](https://github.com/JamesNK/Newtonsoft.Json) by JamesNK
- [**Hi3HelperCore.Http**](https://github.com/neon-nyan/Hi3HelperCore.Http) by neon-nyan

**Disclaimer**: This project **IS NOT AFFILIATED** with [**miHoYo (miHoYo Co., Ltd.)**](https://www.mihoyo.com/) or [**HoYoverse (COGNOSPHERE PTE. LTD.)**](https://www.hoyoverse.com/en-us) by any means and is completely open-sourced. Any contributions are welcomed! 😃

# UI Design Overview
![](https://raw.githubusercontent.com/neon-nyan/CollapseLauncher-Page/main/images/UI%20Overview%20RC2.webp)

# How can I support this project?
Supporting is never an obligation but is always appreciated and motivates us to invest more time in the project and to keep this and other projects alive. To this end, should you decide to support us, here are ways you can do so :smile::
- **[GitHub Sponsors](https://github.com/sponsors/neon-nyan)**
- **[QRIS (Quick Response Code Indonesian Standard)](https://qris.id/homepage/)** (Coming Soon:tm:)
- **[PayPal](https://paypal.me/neonnyan)**

Made by all captains around the world with ❤️. May all beauty be blessed!
