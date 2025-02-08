using CollapseLauncher.Pages;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System.Threading;
// ReSharper disable PartialTypeWithSinglePart

#nullable enable
namespace CollapseLauncher.XAMLs.Theme.CustomControls.UserFeedbackDialog
{
    public partial class UserFeedbackDialog
    {
        #region Fields
        private Grid?          _parentOverlayGrid;
        private Image?         _layoutTitleGridBackgroundImage;
        private TextBlock?     _layoutTitleGridText;
        private TextBox?       _layoutFeedbackTitleInput;
        private TextBox?       _layoutFeedbackMessageInput;
        private TextBlock?     _layoutFeedbackRatingText;
        private RatingControl? _layoutFeedbackRatingControl;
        private Button?        _layoutPrimaryButton;
        private Button?        _layoutCloseButton;

        private readonly InverseBooleanConverter  _inverseBooleanConverter;
        private          CancellationTokenSource? _currentConfirmTokenSource;
        #endregion

        #region Properties
        public string? Title
        {
            get => (string?)GetValue(TitleProperty);
            set => SetValue(TitleProperty, value);
        }

        public bool IsTitleReadOnly
        {
            get => (bool?)GetValue(IsTitleReadOnlyProperty) ?? false;
            set => SetValue(IsTitleReadOnlyProperty, value);
        }

        public string? Message
        {
            get => (string?)GetValue(MessageProperty);
            set => SetValue(MessageProperty, value);
        }

        public bool IsMessageReadOnly
        {
            get => (bool?)GetValue(IsMessageReadOnlyProperty) ?? false;
            set => SetValue(IsMessageReadOnlyProperty, value);
        }

        public double RatingValue
        {
            get => (double)GetValue(RatingValueProperty);
            set => SetValue(RatingValueProperty, value);
        }
        #endregion

        #region Callback Methods
        private void OnUILayoutLoaded(object sender, RoutedEventArgs e)
            // Display the layout
            => VisualStateManager.GoToState(this, "DialogShowing", true);

        private void OnUILayoutUnloaded(object sender, RoutedEventArgs e)
            => UnassignEvents();

        private void OnFeedbackInputsChanged(object sender, TextChangedEventArgs e)
            => _layoutPrimaryButton!.IsEnabled = (!string.IsNullOrWhiteSpace(Title) || IsTitleReadOnly) &&
                                                 (!string.IsNullOrWhiteSpace(Message) || IsMessageReadOnly);

        private void OnSubmissionButtonClick(object sender, RoutedEventArgs e)
            => _currentConfirmTokenSource?.Cancel();

        #endregion

        #region DependencyProperty
        public static readonly DependencyProperty TitleProperty = DependencyProperty.Register(nameof(Title), typeof(string), typeof(UserFeedbackDialog), new PropertyMetadata(null));
        public static readonly DependencyProperty IsTitleReadOnlyProperty = DependencyProperty.Register(nameof(IsTitleReadOnly), typeof(bool), typeof(UserFeedbackDialog), new PropertyMetadata(null));

        public static readonly DependencyProperty MessageProperty = DependencyProperty.Register(nameof(Message), typeof(string), typeof(UserFeedbackDialog), new PropertyMetadata(null));
        public static readonly DependencyProperty IsMessageReadOnlyProperty = DependencyProperty.Register(nameof(IsMessageReadOnly), typeof(bool), typeof(UserFeedbackDialog), new PropertyMetadata(null));

        public static readonly DependencyProperty RatingValueProperty = DependencyProperty.Register(nameof(RatingValue), typeof(double), typeof(UserFeedbackDialog), new PropertyMetadata(null));
        #endregion
    }
}
