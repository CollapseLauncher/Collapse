
# 🧪 Windows Community Toolkit Labs (Preview) 🧪

![Windows Community Toolkit Labs header](CommunityToolkitLabs-Header.png)

Welcome to the home of Windows Community Toolkit Labs. A place for all new components to be developed in 'experiments' for the [Windows Community Toolkit](https://aka.ms/windowstoolkitdocs) (built on top of WinUI 2, [WinUI 3](https://aka.ms/winui3), and [Uno Platform](https://platform.uno))! Find out more about Toolkit Labs in our [blog post here](https://devblogs.microsoft.com/ifdef-windows/announcing-the-windows-community-toolkit-labs/). It includes more about our motivations for having this space as well as how to setup the NuGet feed required to easily use experiments found in this repo.

This is the starting place for all new features to make it into the [Windows Community Toolkit](https://aka.ms/wct). It is a useful prototyping space as well as a space to work collaboratively on polishing a feature. This allows a final PR into the main Toolkit repo to go as smoothly as possible once a feature is ready to go.

## Getting Started

### [Try out our Sample App live in your browser!](https://toolkitlabs.dev)

**[See the list of our current open experiments to try here!](https://github.com/CommunityToolkit/Labs-Windows/issues?q=is%3Aopen+is%3Aissue+label%3A%22experiment+%3Atest_tube%3A%22)**

You can find the corresponding `CommunityToolkit.Labs` packages in our Azure DevOps Feed, [find out more about Preview Packages here](https://aka.ms/toolkit/wiki/previewpackages).

> <https://pkgs.dev.azure.com/dotnet/CommunityToolkit/_packaging/CommunityToolkit-Labs/nuget/v3/index.json>

If you find an experiment useful, please up-vote 👍 its corresponding issue and comment with any feedback. Each experiment has an issue assigned to it with the `experiment` label for tracking. Please file any feedback or issues about that experiment on that singular issue. For any other questions or concerns, please [open a Discussion](https://github.com/CommunityToolkit/Labs-Windows/discussions).

Otherwise, you can clone the repo, open the `components` directory, navigate within the folder for a particular experiment and open up it's solution file in Visual Studio. Run one of the project heads (_ExperimentName.Uwp/Wasm/WinAppSDK_) to see its samples.

## Clone the repository

The [tooling](https://github.com/CommunityToolkit/Tooling-Windows-Submodule) is in a submodule, so you'll need to use `--recurse-submodules` when cloning or pulling for the first time:

```shell
git clone --recurse-submodules https://github.com/CommunityToolkit/Labs-Windows.git
```

## Build Requirements

- Visual Studio 2022 (UWP & Desktop Workloads for .NET)
- .NET 6 SDK
- Windows App SDK
- Windows SDK 19041
- Run `dotnet tool restore` from the project root to install SlnGen
- Run build scripts from the [Developer Command Prompt for Visual Studio](https://learn.microsoft.com/visualstudio/ide/reference/command-prompt-powershell) or from elsewhere after adding `MSBuild.exe` to your PATH

## Adding a new Experiment

**Note:** _In Preview we're currently not accepting new experiments beyond our trial list to ensure our infrastructure is stabilized, if you'd like to contribute [please see where you can lend a hand with Labs itself here](https://github.com/CommunityToolkit/Labs-Windows/issues?q=is%3Aopen+is%3Aissue+label%3A%22help+wanted%22)_ [If you have an idea for a component though, please feel free to open a discussion here.](https://github.com/CommunityToolkit/Labs-Windows/discussions?discussions_q=category%3AExperiments+category%3A%22Ideas%22+)

To start a new experiment, open up a new Discussion to propose your idea with the community. Be sure to follow the template and highlight reasons why and how your idea can aid other developers.

Once there is traction and your idea is approved, an issue will be created to track your experiment and its progress.

Then you can fork the Labs repo, create a new branch, and start working on your feature (or porting over your existing prototype).

```ascii
dotnet new --install .\tooling\ProjectTemplate\

cd components

dotnet new ctk-component -n MyExperimentNameHere
```

[Read more about creating a new experiment from the template folder here.](https://github.com/CommunityToolkit/Tooling-Windows-Submodule/tree/main/ProjectTemplate)

Then open a PR, not everything needs to be done in your initial PR, but some basically functionality and a usage example should exist. The Labs space is a great place to work on something over time, get feedback from the community, and collaborate with others. However, your initial PR should compile and have enough content for folks to understand how to leverage your component.

## Modifying an Experiment

First fork the repo and create a new branch specific to your modification.

To work on an experiment you can navigate to it's subdirectory and open its own solution file. This will let you work on the feature, samples, docs, or unit tests for that specific component only in isolation.

Then submit a PR back to the repo with your modifications. Whoever owns the experiment can then work with you to integrate your changes. A maintainer will merge a PR once sign-off from the experiment owner is received.

## When is an Experiment done?

Not all experiments are successful, and that's ok! That's why we experiment! 👨‍🔬🔬👩‍🔬

If there is enough interest in an experiment, it can be time to move it into the main Windows Community Toolkit repo. These experiments should have all the components required implemented like a sample, documentation, and unit tests.

Open up an issue on the main Toolkit repo using the `Toolkit Labs Transfer` Issue Template. (TODO: Link) Use that issue to discuss where in the Toolkit the new component should be placed and what release it should be shipped in. An initial review pass of the code will happen as well. Once the transfer issue is approved, open up a PR to copy over the experiment to its new home.

## Building the Sample App

First ensure you've met the [Build Requirements](#build-requirements) or the build scripts will fail.

Next you can build the main Sample App solution to see all the experiments currently available in this repository by running the `GenerateAllSolution.bat` script in the repo root. 

Then just open the `CommunityToolkit.AllComponents.sln` solution in Visual Studio. You can run one of the project heads such as `CommunityToolkit.App.WinAppSdk` to run the sample app for that platform.

If you'd like to run a head beyond UWP, Wasm, or the WinAppSDK, you'll need to run the `UseTargetFrameworks.ps1` script first in the `tooling/MultiTarget` directory. e.g. `.\UseTargetFrameworks.ps1 -targets all`

If you'd like to test on Uno + Windows App SDK over Uno + UWP, run the `UseUnoWinUI.ps1` script. e.g. `.\UseUnoWinUI.ps1 -targets 3`

If there's a specific experiment you're interested in instead, you can navigate to its directory and open up it's individual solution to see samples specific to that feature. You can also read the next section to find out how to grab a pre-built NuGet package for each feature available here.

## 📄 Code of Conduct

This project has adopted the code of conduct defined by the [Contributor Covenant](http://contributor-covenant.org/)
to clarify expected behavior in our community.
For more information see the [.NET Foundation Code of Conduct](CODE_OF_CONDUCT.md).

## 🏢 .NET Foundation

This project is supported by the [.NET Foundation](http://dotnetfoundation.org).

## 🏆 Contributors

[![Toolkit Contributors](https://contrib.rocks/image?repo=CommunityToolkit/Labs-Windows)](https://github.com/CommunityToolkit/Labs-Windows/graphs/contributors)

Made with [contrib.rocks](https://contrib.rocks).
