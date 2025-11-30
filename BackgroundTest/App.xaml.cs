using Microsoft.UI;
using Microsoft.UI.Xaml;
using System.Diagnostics.CodeAnalysis;

// ReSharper disable SwitchStatementMissingSomeEnumCasesNoDefault
// ReSharper disable CommentTypo
// ReSharper disable StringLiteralTypo

namespace BackgroundTest;

public partial class App
{
    private TestWindow? _window;

    public App()
    {
        InitializeComponent();
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        _window = new TestWindow();
        _window.Activate();
    }
}
