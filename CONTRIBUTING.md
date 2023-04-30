# Contribution Guidelines
## Before you Start
- Unless otherwise stated in this project README. Make sure to clone the source code from the `main` branch using `--recurse` parameter to pull all the submodules Collapse need to run.

    ```git pull https://github.com/neon-nyan/Collapse --recurse```
    
- Make sure you use the supported IDE/SDKs listed below.
- Make sure to read the "Restriction for New Feature(s)" section below.
- When submitting a Pull Request, make sure that it is done to the same branch from which you first pulled, unless otherwise stated in the project's README, or if specifically instructed by maintainers of this project.
- We don't require a fully descriptive PR, but please be clear on what added/removed and what the code does.
## Language/Translation Contribution
You can help us add or fixes localization on [Collapse on Crowdin!](https://crowdin.com/project/collapse-launcher)
If you wish to add new language that isn't yet listed in the Crowdin project, make a discussion/issue either in GitHub or in Crowdin itself and we will get back to you at timely manner. 

## Tools Needed
Below is the tools needed to contribute to this project.
1. **Visual Studio 2022 (Any Edition - 17.4 or later)**
2. **Windows 10 SDK (10.0.19043.0) or Windows 11 SDK (10.0.22000.0)** via Visual Studio Installer
3. .NET: [**.NET Core 7 SDK (7.0.100 or later)**](https://dotnet.microsoft.com/en-us/download/dotnet/7.0)
4. WinUI 3: [**WinUI 3 (WindowsAppSDK 1.3.0-230331000 Stable Runtime)**](https://aka.ms/windowsappsdk/1.3/1.3.230331000/windowsappruntimeinstall-x64.exe)

## Restriction for New Feature(s)
While this software is fully open source and not affiliated with HoYoverse, Cognosphere, or any of its related entities in any way, we are nonetheless bound to their Terms of Service and Code of Conduct when developing Collapse. This means that there are some features that we will **not** implement. We will close any issue or PR that is made to add such functionality to Collapse. Such features include, but not limited to:
- Anything that, in any way, interacts with miHoYo SDK, including its Authentication and Payment Processing endpoints.
- Anything that directly injects into the game process (and/or any of its subprocesses) including DLL injection, memory reading, etc.
- Anything that directly modifies game files and resources to provide an unfair advantage in-game for those using our launcher.
### Examples for features that we endorse to be implemented:
- Community resources
- Officially-endorsed HoYoverse Tools
  etc.
  
### Examples for features that we will probably never implement:
- Account switcher
- FPS Unlocker
- Any type of cheats or exploits.
  etc.


## Further reads:
- [Genshin Impact Terms of Service](https://genshin.hoyoverse.com/en/company/terms)
- [Honkai Impact 3rd Terms of Service](https://honkaiimpact3.hoyoverse.com/global/en-us/terms)
- [Honkai: Star Rail Terms of Service](https://hsr.hoyoverse.com/en-us/company/terms)
- [HoYoLAB Forum Terms of Service](https://www.hoyolab.com/agreement)

# A Humble Thank You
As contributors, we always feel grateful for all your contribution to the project, whether it be through helping with localizing the app, coming up with new features, reporting bugs, and even using this launcher. Through everyone's effort, we can keep this project alive by bringing even more features and quality-of-life (QoL) upgrades over the existing launchers (icluding official) that is out there.
Thank you ❤️