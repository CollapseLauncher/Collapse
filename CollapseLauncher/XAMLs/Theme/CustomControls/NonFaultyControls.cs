using Hi3Helper;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Runtime.CompilerServices;
using Windows.Foundation;

namespace CollapseLauncher.XAMLs.Theme.CustomControls;

public partial class NonFaultyGrid : Grid
{
    protected override Size ArrangeOverride(Size finalSize)
        => this.TryCatchOperation(base.ArrangeOverride, this.NormalizeSize(finalSize));
    protected override Size MeasureOverride(Size availableSize)
        => this.TryCatchOperation(base.MeasureOverride, this.NormalizeSize(availableSize));
}

public partial class NonFaultyButton : Button
{
    protected override Size ArrangeOverride(Size finalSize)
        => this.TryCatchOperation(base.ArrangeOverride, this.NormalizeSize(finalSize));
    protected override Size MeasureOverride(Size availableSize)
        => this.TryCatchOperation(base.MeasureOverride, this.NormalizeSize(availableSize));
}

public partial class NonFaultyProgressBar : ProgressBar
{
    protected override Size ArrangeOverride(Size finalSize)
        => this.TryCatchOperation(base.ArrangeOverride, this.NormalizeSize(finalSize));
    protected override Size MeasureOverride(Size availableSize)
        => this.TryCatchOperation(base.MeasureOverride, this.NormalizeSize(availableSize));
}

public partial class NonFaultyProgressRing : ProgressRing
{
    protected override Size ArrangeOverride(Size finalSize)
        => this.TryCatchOperation(base.ArrangeOverride, this.NormalizeSize(finalSize));
    protected override Size MeasureOverride(Size availableSize)
        => this.TryCatchOperation(base.MeasureOverride, this.NormalizeSize(availableSize));
}

public partial class NonFaultyStackPanel : StackPanel
{
    protected override Size ArrangeOverride(Size finalSize)
        => this.TryCatchOperation(base.ArrangeOverride, this.NormalizeSize(finalSize));
    protected override Size MeasureOverride(Size availableSize)
        => this.TryCatchOperation(base.MeasureOverride, this.NormalizeSize(availableSize));
}

public partial class NonFaultyCheckBox : CheckBox
{
    protected override Size ArrangeOverride(Size finalSize)
        => this.TryCatchOperation(base.ArrangeOverride, this.NormalizeSize(finalSize));
    protected override Size MeasureOverride(Size availableSize)
        => this.TryCatchOperation(base.MeasureOverride, this.NormalizeSize(availableSize));
}

public partial class NonFaultyImageEx : ImageEx.ImageEx
{
    protected override Size ArrangeOverride(Size finalSize)
        => this.TryCatchOperation(base.ArrangeOverride, this.NormalizeSize(finalSize));
    protected override Size MeasureOverride(Size availableSize)
        => this.TryCatchOperation(base.MeasureOverride, this.NormalizeSize(availableSize));
}

file static class NonFaultyExtension
{
    extension(FrameworkElement element)
    {
        internal T TryCatchOperation<T>(Func<T, T>                action,
                                        T                         input,
                                        [CallerMemberName] string nameOfCaller = null)
        {
            try
            {
                return action(input);
            }
            catch (Exception ex)
            {
                Logger.LogWriteLine($"This element: {element.GetType().Name} ({element.Name}) got errored while trying to call: {nameOfCaller}\r\n: {ex}",
                                    LogType.Error,
                                    true);
            }

            return default;
        }

        internal Size NormalizeSize(Size inputSize)
        {
            double width  = inputSize.Width;
            double height = inputSize.Height;

            if (double.IsNaN(width) ||
                width < 0)
            {
                Logger.LogWriteLine($"This element: {element.GetType().Name} ({element.Name}) has NaN or subnormal width! {width}",
                                    LogType.Debug,
                                    true);
                width = 0;
            }

            // ReSharper disable once InvertIf shut the fuck up
            if (double.IsNaN(height) ||
                height < 0)
            {
                Logger.LogWriteLine($"This element: {element.GetType().Name} ({element.Name}) has NaN or subnormal height! {height}",
                                    LogType.Debug,
                                    true);
                height = 0;
            }

            return new Size(width, height);
        }
    }
}