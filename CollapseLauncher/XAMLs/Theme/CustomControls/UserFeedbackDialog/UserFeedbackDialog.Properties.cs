using CollapseLauncher.Extension;
using CollapseLauncher.Pages;
using CommunityToolkit.WinUI;
using Hi3Helper;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Threading;
using System.Threading.Tasks;
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

        private static InverseBooleanConverter? _inverseBooleanConverter;
        private        CancellationTokenSource? _currentConfirmTokenSource;

        private          bool _isSubmit;
        private readonly bool _isAlwaysOnTop;
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

        public Visibility TitleVisibility
        {
            get => (Visibility)GetValue(TitleVisibilityProperty);
            set => SetValue(TitleVisibilityProperty, value);
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
            => _layoutPrimaryButton!.IsEnabled = (TitleVisibility == Visibility.Collapsed || !string.IsNullOrWhiteSpace(Title) || IsTitleReadOnly) &&
                                                 (!string.IsNullOrWhiteSpace(Message) || IsMessageReadOnly);

        private void OnSubmissionSubmitButtonClick(object sender, RoutedEventArgs e)
        {
            _isSubmit = true;
            InvalidateTokenSource();
        }

        private void OnSubmissionCancelButtonClick(object sender, RoutedEventArgs e)
        {
            _isSubmit = false;
            InvalidateTokenSource();
        }

        private async Task OnRunningSubmitTask(Func<UserFeedbackResult?, CancellationToken, Task>? actionCallbackTaskOnSubmit, UserFeedbackResult? result)
        {
            if (!(actionCallbackTaskOnSubmit is not null && _isSubmit))
            {
                return;
            }

            try
            {
                _currentConfirmTokenSource ??= new CancellationTokenSource();
                OnSubmitBtnRunningTask();
                await actionCallbackTaskOnSubmit(result, _currentConfirmTokenSource.Token);
            }
            catch
            {
                // ignored
            }
            finally
            {
                await OnSubmitBtnCompletedTask();
                DisposeTokenSource();
            }
        }

        private void OnSubmitBtnRunningTask()
        {
            if (!(_layoutPrimaryButton is not null &&
                  _layoutFeedbackTitleInput is not null &&
                  _layoutFeedbackMessageInput is not null &&
                  _layoutFeedbackRatingControl is not null))
                return;

            TextBlock? textBlock = (_layoutPrimaryButton.Content as UIElement)?.FindDescendant<TextBlock>();
            ProgressRing? progressRing = (_layoutPrimaryButton.Content as UIElement)?.FindDescendant<ProgressRing>();
            Grid? iconGrid = (_layoutPrimaryButton.Content as UIElement)?.FindDescendant<Grid>();

            if (textBlock is null ||
                progressRing is null ||
                iconGrid is null)
                return;

            _layoutPrimaryButton.IsEnabled         = false;
            _layoutFeedbackTitleInput.IsEnabled    = false;
            _layoutFeedbackMessageInput.IsEnabled  = false;
            _layoutFeedbackRatingControl.IsEnabled = false;
            progressRing.Visibility                = Visibility.Visible;
            progressRing.IsIndeterminate           = true;
            progressRing.Opacity                   = 1d;
            iconGrid.Opacity                       = 0d;

            textBlock.BindProperty(TextBlock.TextProperty,
                                   Locale.Lang._Dialogs,
                                   nameof(Locale.Lang._Dialogs.UserFeedback_SubmitBtn_Processing));
        }

        private Task OnSubmitBtnCompletedTask()
        {
            if (!(_layoutPrimaryButton is not null &&
                  _layoutCloseButton is not null))
                return Task.CompletedTask;

            TextBlock? textBlock = (_layoutPrimaryButton.Content as UIElement)?.FindDescendant<TextBlock>();
            ProgressRing? progressRing = (_layoutPrimaryButton.Content as UIElement)?.FindDescendant<ProgressRing>();
            bool isCancelled = _currentConfirmTokenSource?.IsCancellationRequested ?? false;

            if (textBlock is null ||
                progressRing is null)
                return Task.CompletedTask;

            progressRing.IsIndeterminate = false;
            _layoutCloseButton.IsEnabled = false;

            textBlock.BindProperty(TextBlock.TextProperty,
                                   Locale.Lang._Dialogs,
                                   isCancelled ?
                                       nameof(Locale.Lang._Dialogs.UserFeedback_SubmitBtn_Cancelled) :
                                       nameof(Locale.Lang._Dialogs.UserFeedback_SubmitBtn_Completed));

            return ReturnDelay();

            Task ReturnDelay() => Task.Delay(TimeSpan.FromSeconds(1));
        }

        private void InvalidateTokenSource()
            => _currentConfirmTokenSource?.Cancel();

        private void DisposeTokenSource()
        {
            _currentConfirmTokenSource?.Dispose();
            Interlocked.Exchange(ref _currentConfirmTokenSource, null);
        }
        #endregion

        #region DependencyProperty
        public static readonly DependencyProperty TitleProperty = DependencyProperty.Register(nameof(Title), typeof(string), typeof(UserFeedbackDialog), new PropertyMetadata(null));
        public static readonly DependencyProperty TitleVisibilityProperty = DependencyProperty.Register(nameof(TitleVisibility), typeof(Visibility), typeof(UserFeedbackDialog), new PropertyMetadata(null));
        public static readonly DependencyProperty IsTitleReadOnlyProperty = DependencyProperty.Register(nameof(IsTitleReadOnly), typeof(bool), typeof(UserFeedbackDialog), new PropertyMetadata(null));

        public static readonly DependencyProperty MessageProperty = DependencyProperty.Register(nameof(Message), typeof(string), typeof(UserFeedbackDialog), new PropertyMetadata(null));
        public static readonly DependencyProperty IsMessageReadOnlyProperty = DependencyProperty.Register(nameof(IsMessageReadOnly), typeof(bool), typeof(UserFeedbackDialog), new PropertyMetadata(null));

        public static readonly DependencyProperty RatingValueProperty = DependencyProperty.Register(nameof(RatingValue), typeof(double), typeof(UserFeedbackDialog), new PropertyMetadata(null));
        #endregion
    }
}
