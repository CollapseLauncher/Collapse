using CollapseLauncher.Extension;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Documents;
using Microsoft.UI.Xaml.Media;
using System.Collections.Generic;
// ReSharper disable IdentifierTypo

#nullable enable
namespace CollapseLauncher.Helper
{
    internal static class HighlightableControlPropertyExtension
    {
        internal static HighlightableControlProperty CreateHighlight(this FrameworkElement element, Brush? highlightBrush, Brush? highlightSelectedBrush)
            => HighlightableControlProperty.Create(element, highlightBrush, highlightSelectedBrush);
    }

    internal class HighlightableControlProperty
    {
        internal FrameworkElement?       Element                                         { get; }
        private  Brush?                  BackgroundHighlightBrush                        { get; }
        private  Brush?                  BackgroundHighlightSelectedBrush                { get; }
        private  Brush?                  BackgroundOriginalBrush                         { get; }

        internal IList<TextHighlighter>? ElementTextHighlighter                          { get; }
        private  Brush?                  TextHighlighterBackgroundHighlightBrush         { get; }
        private  Brush?                  TextHighlighterBackgroundHighlightSelectedBrush { get; }

        private HighlightableControlProperty(FrameworkElement element, Brush? highlightBrush, Brush? highlightSelectedBrush)
        {
            Element = element;

            if (element is TextBlock textBlock)
            {
                ElementTextHighlighter                          = textBlock.TextHighlighters;
                TextHighlighterBackgroundHighlightBrush         = highlightBrush;
                TextHighlighterBackgroundHighlightSelectedBrush = highlightSelectedBrush;
            }
            else
            {
                BackgroundHighlightBrush         = highlightBrush;
                BackgroundHighlightSelectedBrush = highlightSelectedBrush;
                BackgroundOriginalBrush          = element.GetBackground();
            }
        }

        internal static HighlightableControlProperty Create(FrameworkElement element, Brush? highlightBrush, Brush? highlightSelectedBrush)
            => new(element, highlightBrush, highlightSelectedBrush);

        internal void SetBrushHighlightElement()
        {
            SetBackground(Element is TextBlock ? TextHighlighterBackgroundHighlightBrush : BackgroundHighlightBrush);

            if (Element is ComboBoxItem { Parent: ComboBox asComboBox })
            {
                asComboBox.IsDropDownOpen = false;
            }
        }

        internal void SetBrushHighlightSelectElement()
        {
            SetBackground(Element is TextBlock ? TextHighlighterBackgroundHighlightSelectedBrush : BackgroundHighlightSelectedBrush);

            if (Element is ComboBoxItem { Parent: ComboBox asComboBox })
            {
                asComboBox.IsDropDownOpen = true;
            }
        }

        internal void ClearHighlight()
        {
            if (Element is TextBlock textBlock && textBlock.TextHighlighters.Count != 0)
            {
                textBlock.TextHighlighters.Clear();
                return;
            }

            SetBackground(BackgroundOriginalBrush);
        }

        private void SetBackground(Brush? brush)
        {
            if (Element is TextBlock textBlock && textBlock.TextHighlighters.Count != 0)
            {
                foreach (TextHighlighter highlighter in textBlock.TextHighlighters)
                {
                    highlighter.Background = brush;
                }
                List<TextHighlighter> reAddList = [.. textBlock.TextHighlighters];
                textBlock.TextHighlighters.Clear();
                foreach (TextHighlighter readdHighlighter in reAddList)
                {
                    textBlock.TextHighlighters.Add(readdHighlighter);
                }
                return;
            }

            _ = Element?.SetBackground(brush);
        }
    }
}
