### Before you Start
- Unless stated in README.md. Make sure you are pulling the source code from main branch with recursive variable to pull all submodules Collapse need to run.

    ```git pull https://github.com/neon-nyan/Collapse --recursive```
    
- Make sure you use the supported IDE/SDKs listed below.
- Make sure to read the Feature(s) restriction below.
- When its time to do a Pull Request, do a pull request to the same branch when you first pulled.
- We don't require a fully descriptive PR description, but please be clear on what added/removed and what it does.

### Software Needed
Below is the software needed to develop and contributing to this project. Also, Collapse uses many submodule inside its repository, so don't forget to use recursive variable when pulling this repository to your local computer.
1. **Visual Studio 2022 (Any Edition - 17.4 or later)**
2. .NET: [**.NET Core 7 SDK (7.0.100 or later)**](https://dotnet.microsoft.com/en-us/download/dotnet/7.0)
3. WinUI 3: [**WinUI 3 (WindowsAppSDK 1.3.0-230331000 Stable Runtime)**](https://aka.ms/windowsappsdk/1.3/1.3.230331000/windowsappruntimeinstall-x64.exe)
4. **Windows 10 SDK (10.0.19043.0 or later)** via Visual Studio Installer

### Restriction for New Feature(s)
While this software is fully open source and not affiliated with HoYoverse/Cognosphere in any way, we still need to some extent follow their Terms of Service and Code of Conduct when developing Collapse. With that in mind, there are some features we can not implement and we will reject as it comes up in a commit/PR, these features for example:
- Anything that touches their SDK/Account auth/Payment endpoints, such as [account switcher](https://github.com/neon-nyan/Collapse/issues/88).
- Anything that read/write directly to game process or any of its subprocess, such as [FPS Unlocker for Genshin Impact](https://github.com/neon-nyan/Collapse/issues/104).
- Anything that modify the game files/resources directly to provide unfair advantage in-game.

Further reads:
- [Genshin Impact Terms of Service](https://genshin.hoyoverse.com/en/company/terms)
- [Honkai Impact 3rd Terms of Service](https://honkaiimpact3.hoyoverse.com/global/en-us/terms)