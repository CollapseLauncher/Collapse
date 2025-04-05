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
        /// <summary>
        /// The predefined title to be set or the result of what has been typed in the
        /// feedback's <see cref="TextBox"/>.
        /// </summary>
        public string? Title
        {
            get => (string?)GetValue(TitleProperty);
            set => SetValue(TitleProperty, value);
        }

        /// <summary>
        /// Set the title's <see cref="TextBox.IsReadOnly"/> property.
        /// </summary>
        public bool IsTitleReadOnly
        {
            get => (bool?)GetValue(IsTitleReadOnlyProperty) ?? false;
            set => SetValue(IsTitleReadOnlyProperty, value);
        }

        /// <summary>
        /// Set the visibility of the title's <see cref="TextBox"/>.
        /// </summary>
        public Visibility TitleVisibility
        {
            get => (Visibility)GetValue(TitleVisibilityProperty);
            set => SetValue(TitleVisibilityProperty, value);
        }

        /// <summary>
        /// The predefined message to be set or the result of what has been typed in the
        /// feedback's <see cref="TextBox"/>.
        /// </summary>
        public string? Message
        {
            get => (string?)GetValue(MessageProperty);
            set => SetValue(MessageProperty, value);
        }

        /// <summary>
        /// Set the message's <see cref="TextBox.IsReadOnly"/> property.
        /// </summary>
        public bool IsMessageReadOnly
        {
            get => (bool?)GetValue(IsMessageReadOnlyProperty) ?? false;
            set => SetValue(IsMessageReadOnlyProperty, value);
        }

        /// <summary>
        /// The predefined rating value to be set or the result of the <see cref="RatingControl"/>.<br/>
        /// Default: 5, Valid value range: -1 to 5
        /// </summary>
        public double RatingValue
        {
            get => Math.Clamp((double)GetValue(RatingValueProperty), -1, 5);
            set => SetValue(RatingValueProperty, Math.Clamp(value, -1, 5));
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
            // Set submission as true and invalidate token source
            _isSubmit = true;
            InvalidateTokenSource();
        }

        private void OnSubmissionCancelButtonClick(object sender, RoutedEventArgs e)
        {
            // Set submission as false and invalidate token source
            _isSubmit = false;
            InvalidateTokenSource();
        }

        private async Task OnRunningSubmitTask(Func<UserFeedbackResult?, CancellationToken, Task>? actionCallbackTaskOnSubmit, UserFeedbackResult? result)
        {
            // If the callback task is null or is not submitted, return
            if (!(actionCallbackTaskOnSubmit is not null && _isSubmit))
            {
                return;
            }

            try
            {
                // Assign the token source and set the UI state of tbe Submit button
                _currentConfirmTokenSource ??= new CancellationTokenSource();
                OnSubmitBtnRunningTask();

                // Call the callback task and pass the result and cancellation token
                await actionCallbackTaskOnSubmit(result, _currentConfirmTokenSource.Token);
            }
            catch
            {
                // ignored
            }
            finally
            {
                // Once all have been executed, set the UI state of the Submit button as completed
                // and dispose the token source.
                await OnSubmitBtnCompletedTask();
                DisposeTokenSource();
            }
        }

        private void OnSubmitBtnRunningTask()
        {
            // If the UI buttons and input control is null, return
            if (!(_layoutPrimaryButton is not null &&
                  _layoutFeedbackTitleInput is not null &&
                  _layoutFeedbackMessageInput is not null &&
                  _layoutFeedbackRatingControl is not null))
                return;

            // Find the UI element where each of its state to be changed
            TextBlock? textBlock = (_layoutPrimaryButton.Content as UIElement)?.FindDescendant<TextBlock>();
            ProgressRing? progressRing = (_layoutPrimaryButton.Content as UIElement)?.FindDescendant<ProgressRing>();
            Grid? iconGrid = (_layoutPrimaryButton.Content as UIElement)?.FindDescendant<Grid>();

            // If any of them is null, return
            if (textBlock is null ||
                progressRing is null ||
                iconGrid is null)
                return;

            // Set the UI element state and disable some elements (to prevent the result's value to be changed).
            _layoutPrimaryButton.IsEnabled         = false;
            _layoutFeedbackTitleInput.IsEnabled    = false;
            _layoutFeedbackMessageInput.IsEnabled  = false;
            _layoutFeedbackRatingControl.IsEnabled = false;
            progressRing.Visibility                = Visibility.Visible;
            progressRing.IsIndeterminate           = true;
            progressRing.Opacity                   = 1d;
            iconGrid.Opacity                       = 0d;

            // Set the text as "Processing..." to the Submit button.
            textBlock.BindProperty(TextBlock.TextProperty,
                                   Locale.Lang._Dialogs,
                                   nameof(Locale.Lang._Dialogs.UserFeedback_SubmitBtn_Processing));
        }

        private Task OnSubmitBtnCompletedTask()
        {
            // If any of the button is null, return
            if (!(_layoutPrimaryButton is not null &&
                  _layoutCloseButton is not null))
                return Task.CompletedTask;

            // Find the UI element where each of its state to be changed and set the cancel state
            TextBlock? textBlock = (_layoutPrimaryButton.Content as UIElement)?.FindDescendant<TextBlock>();
            ProgressRing? progressRing = (_layoutPrimaryButton.Content as UIElement)?.FindDescendant<ProgressRing>();
            bool isCancelled = _currentConfirmTokenSource?.IsCancellationRequested ?? false;

            // If any of the UI element is missing, return
            if (textBlock is null ||
                progressRing is null)
                return Task.CompletedTask;

            // Change the UI state and disable the close/cancel button
            progressRing.IsIndeterminate = false;
            _layoutCloseButton.IsEnabled = false;

            // Set the Submit button's text based on the isCancelled state.
            textBlock.BindProperty(TextBlock.TextProperty,
                                   Locale.Lang._Dialogs,
                                   isCancelled ?
                                       nameof(Locale.Lang._Dialogs.UserFeedback_SubmitBtn_Cancelled) :
                                       nameof(Locale.Lang._Dialogs.UserFeedback_SubmitBtn_Completed));

            // Delay before return
            return ReturnDelay();

            // Return for 1 second
            Task ReturnDelay() => Task.Delay(TimeSpan.FromSeconds(1));
        }

        private void InvalidateTokenSource()
            => _currentConfirmTokenSource?.Cancel();

        private void DisposeTokenSource()
        {
            // Dispose the token source and set the token source as null.
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
