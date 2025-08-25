using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Documents;
using Microsoft.UI.Xaml.Media;
using System.Collections.Generic;
using System.Numerics;
using Windows.Foundation;
// ReSharper disable PartialTypeWithSinglePart

namespace CollapseLauncher.CustomControls
{
    public partial class CompressedTextBlock : Grid
    {
        #region Context
        private const string OpenBracketMarks = "「『“‘（《〈【〖〔［｛";
        private const string CloseBracketMarks = "」』”’）》〉】〗〕］｝";
        private const string PauseMarks = "。．，、：；！？";
        private const string OpenMarks = OpenBracketMarks;
        private const string CloseMarks = CloseBracketMarks + PauseMarks;

        class CompressedText
        {
            public string Text = "";
            public int Spacing;
        }

        private class CompressedContext
        {
            public          bool                 BeginOffset;
            public readonly List<CompressedText> Texts = [];
        }

        private readonly CompressedContext _context          = new();
        private readonly TextBlock         _measureTextBlock = new();
        private readonly TextBlock         _contentTextBlock = new();
        #endregion

        #region Properties
        // TODO: Add more properties

        public static readonly DependencyProperty ForegroundProperty = DependencyProperty.RegisterAttached(
            nameof(Foreground),
            typeof(Brush),
            typeof(CompressedTextBlock),
            new PropertyMetadata(null, OnForegroundChanged));

        public Brush Foreground
        {
            get => (Brush)GetValue(ForegroundProperty);
            set => SetValue(ForegroundProperty, value);
        }

        public static readonly DependencyProperty TextTrimmingProperty = DependencyProperty.RegisterAttached(
            nameof(TextTrimming),
            typeof(TextTrimming),
            typeof(CompressedTextBlock),
            new PropertyMetadata(default(TextTrimming), OnTextTrimmingChanged));

        public TextTrimming TextTrimming
        {
            get => (TextTrimming)GetValue(TextTrimmingProperty);
            set => SetValue(TextTrimmingProperty, value);
        }

        public static readonly DependencyProperty TextProperty = DependencyProperty.RegisterAttached(
            nameof(Text),
            typeof(string),
            typeof(CompressedTextBlock),
            new PropertyMetadata("", OnTextChanged));

        public string Text
        {
            get => (string)GetValue(TextProperty);
            set => SetValue(TextProperty, value);
        }

        public static readonly DependencyProperty FontSizeProperty = DependencyProperty.RegisterAttached(
            nameof(FontSize),
            typeof(double),
            typeof(CompressedTextBlock),
            new PropertyMetadata(14d, OnFontSizeChanged));

        public double FontSize
        {
            get => (double)GetValue(FontSizeProperty);
            set => SetValue(FontSizeProperty, value);
        }
        #endregion

        #region Events
        private static void OnForegroundChanged(DependencyObject sender, DependencyPropertyChangedEventArgs e)
        {
            var compressedTextBlock = (CompressedTextBlock)sender;
            compressedTextBlock._contentTextBlock.Foreground = (Brush)e.NewValue;
        }

        private static void OnTextTrimmingChanged(DependencyObject sender, DependencyPropertyChangedEventArgs e)
        {
            var compressedTextBlock = (CompressedTextBlock)sender;
            compressedTextBlock._contentTextBlock.TextTrimming = (TextTrimming)e.NewValue;
        }

        private static void OnTextChanged(DependencyObject sender, DependencyPropertyChangedEventArgs e)
        {
            var compressedTextBlock = (CompressedTextBlock)sender;
            compressedTextBlock.InvalidateText();
        }

        private static void OnFontSizeChanged(DependencyObject sender, DependencyPropertyChangedEventArgs e)
        {
            var compressedTextBlock = (CompressedTextBlock)sender;
            compressedTextBlock.UpdateFontSize((double)e.NewValue);
        }
        #endregion

        public CompressedTextBlock()
        {
            Children.Add(_contentTextBlock);
        }

        private bool CheckCompressMark(string marks, char ch)
        {
            var contains = marks.Contains(ch);
            if (!contains) return false;
            _measureTextBlock.Text = "" + ch;
            _measureTextBlock.Measure(new Size(float.MaxValue, float.MaxValue));
            return _measureTextBlock.DesiredSize.Height <= _measureTextBlock.DesiredSize.Width * 1.5;
        }

        private void UpdateFontSize(double value)
        {
            _contentTextBlock.FontSize = value;
        }

        private void InvalidateText()
        {
            _contentTextBlock.Inlines.Clear();
            if (Text.Length <= 0) return;

            var lastChar = Text[0];
            var isLastOpenMark = CheckCompressMark(OpenMarks, lastChar);
            var isLastCloseMark = CheckCompressMark(CloseMarks, lastChar);
            var compressedText = new CompressedText();

            _context.BeginOffset = isLastOpenMark;
            _context.Texts.Clear();

            for (var i = 1; i < Text.Length; i++)
            {
                var ch = Text[i];
                var isOpenMark = CheckCompressMark(OpenMarks, ch);
                var isCloseMark = CheckCompressMark(CloseMarks, ch);

                var spacing = 0;
                if ((isOpenMark && isLastOpenMark) || (isCloseMark && (isLastOpenMark || isLastCloseMark)))
                    spacing = -250;
                else if (isOpenMark && isLastCloseMark)
                    spacing = -500;

                if (compressedText.Text.Length > 0 && compressedText.Spacing != spacing)
                {
                    _context.Texts.Add(compressedText);
                    compressedText = new CompressedText();
                }

                compressedText.Text += lastChar;
                compressedText.Spacing = spacing;

                lastChar = ch;
                isLastOpenMark = isOpenMark;
                isLastCloseMark = isCloseMark;
            }

            if (compressedText.Spacing != 0)
            {
                _context.Texts.Add(compressedText);
                compressedText = new CompressedText();
            }

            compressedText.Text += lastChar;
            _context.Texts.Add(compressedText);

            foreach (var text in _context.Texts)
            {
                _contentTextBlock.Inlines.Add(new Run
                {
                    Text = text.Text,
                    CharacterSpacing = text.Spacing
                });
            }

            _contentTextBlock.Translation = _context.BeginOffset ? new Vector3(1 - (float)_contentTextBlock.FontSize / 2, 0, 0) : new Vector3();
        }
    }
}
