using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using System;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

#nullable enable
namespace CollapseLauncher.XAMLs.Theme.CustomControls;

internal partial class LayeredBackgroundImageExtensions
{
    public static bool TryGetBindingPropertyNames(
        FrameworkElement?               parentElement,
        [NotNullWhen(true)] out string? stretchNameOf,
        [NotNullWhen(true)] out string? horizontalAlignNameOf,
        [NotNullWhen(true)] out string? verticalAlignNameOf)
    {
        const string gridSuffix = nameof(Grid);

        Unsafe.SkipInit(out stretchNameOf);
        Unsafe.SkipInit(out horizontalAlignNameOf);
        Unsafe.SkipInit(out verticalAlignNameOf);

        ReadOnlySpan<char> nameSpan = parentElement?.Name;
        if (nameSpan.IsEmpty)
            return false;

        if (!nameSpan.EndsWith(gridSuffix, StringComparison.OrdinalIgnoreCase) ||
            (nameSpan[0] | 0x20) is < 'a' or > 'z') // Check if first char is not a letter.
            return false;

        string nameTrimmed = nameSpan[..^gridSuffix.Length].ToString();
        stretchNameOf         = nameTrimmed + nameof(Stretch);
        horizontalAlignNameOf = nameTrimmed + nameof(HorizontalAlignment);
        verticalAlignNameOf   = nameTrimmed + nameof(VerticalAlignment);

        return true;
    }
}
