using System;

// ReSharper disable CheckNamespace

#nullable enable
namespace CollapseLauncher.GameManagement.ImageBackground;

[Flags]
public enum FrameToCopyType
{
    Foreground = 1,
    Background = 2,

    Both = Foreground | Background
}
