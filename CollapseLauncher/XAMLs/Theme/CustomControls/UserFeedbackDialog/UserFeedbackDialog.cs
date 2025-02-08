using CollapseLauncher.Extension;
using CollapseLauncher.Pages;
using CommunityToolkit.WinUI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Data;
using System;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using System.Threading.Tasks;
// ReSharper disable PartialTypeWithSinglePart

#nullable enable
namespace CollapseLauncher.XAMLs.Theme.CustomControls.UserFeedbackDialog
{
    public record UserFeedbackResult(string Title, string Message, double Rating);

    public partial class UserFeedbackDialog : Control
    {

        public UserFeedbackDialog()
        {
            DefaultStyleKey = typeof(UserFeedbackDialog);
            _inverseBooleanConverter = new InverseBooleanConverter();
        }

        protected override void OnApplyTemplate()
        {
            _layoutTitleGridBackgroundImage = GetTemplateChild(TemplateNameTitleGridBackgroundImage) as Image;
            _layoutFeedbackTitleInput       = GetTemplateChild(TemplateNameFeedbackTitleInput) as TextBox;
            _layoutFeedbackMessageInput     = GetTemplateChild(TemplateNameFeedbackMessageInput) as TextBox;
            _layoutFeedbackRatingControl    = GetTemplateChild(TemplateNameFeedbackRatingControl) as RatingControl;
            _layoutPrimaryButton            = GetTemplateChild(TemplateNamePrimaryButton) as Button;
            _layoutCloseButton              = GetTemplateChild(TemplateNameCloseButton) as Button;

            RatingValue = 5d;

            AssignEvents();
            AssignBindings();
            base.OnApplyTemplate();
        }

        private void AssignBindings()
        {
            // Bind this Text properties to the UI FeedbackTitleInput and FeedbackMessageInput
            _layoutFeedbackTitleInput?.BindProperty(TextBox.TextProperty,
                                                    this,
                                                    nameof(Title),
                                                    null,
                                                    BindingMode.TwoWay);
            _layoutFeedbackMessageInput?.BindProperty(TextBox.TextProperty,
                                                      this,
                                                      nameof(Message),
                                                      null,
                                                      BindingMode.TwoWay);

            // Bind this ReadOnly properties to the UI FeedbackTitleInput and FeedbackMessageInput
            _layoutFeedbackTitleInput?.BindProperty(TextBox.IsReadOnlyProperty,
                                                    this,
                                                    nameof(IsTitleReadOnly),
                                                    null,
                                                    BindingMode.TwoWay);
            _layoutFeedbackTitleInput?.BindProperty(IsEnabledProperty,
                                                    this,
                                                    nameof(IsTitleReadOnly),
                                                    _inverseBooleanConverter,
                                                    BindingMode.TwoWay);
            _layoutFeedbackMessageInput?.BindProperty(TextBox.IsReadOnlyProperty,
                                                      this,
                                                      nameof(IsMessageReadOnly),
                                                      null,
                                                      BindingMode.TwoWay);
            _layoutFeedbackMessageInput?.BindProperty(IsEnabledProperty,
                                                      this,
                                                      nameof(IsMessageReadOnly),
                                                      _inverseBooleanConverter,
                                                      BindingMode.TwoWay);

            // Bind this RatingValue property to the UI FeedbackRatingControl
            _layoutFeedbackRatingControl?.BindProperty(RatingControl.ValueProperty,
                                                    this,
                                                    nameof(RatingValue),
                                                    null,
                                                    BindingMode.TwoWay);

            // Simulate changes
            OnFeedbackInputsChanged(null!, null!);
        }

        private void AssignEvents()
        {
            Loaded                                   += OnUILayoutLoaded;
            Unloaded                                 += OnUILayoutUnloaded;
            _layoutPrimaryButton!.Click              += OnSubmissionButtonClick;
            _layoutCloseButton!.Click                += OnSubmissionButtonClick;
            _layoutFeedbackTitleInput!.TextChanged   += OnFeedbackInputsChanged;
            _layoutFeedbackMessageInput!.TextChanged += OnFeedbackInputsChanged;
        }


        private void UnassignEvents()
        {
            Loaded                                   -= OnUILayoutLoaded;
            Unloaded                                 -= OnUILayoutUnloaded;
            _layoutPrimaryButton!.Click              -= OnSubmissionButtonClick;
            _layoutCloseButton!.Click                -= OnSubmissionButtonClick;
            _layoutFeedbackTitleInput!.TextChanged   -= OnFeedbackInputsChanged;
            _layoutFeedbackMessageInput!.TextChanged -= OnFeedbackInputsChanged;
        }

        public async Task<UserFeedbackResult?> ShowAsync()
        {
            _parentOverlayGrid = FindOverlayGrid(XamlRoot);
            _parentOverlayGrid?.Children.Add(this);

            _currentConfirmTokenSource ??= new CancellationTokenSource();
            try
            {
                await UseTokenAndWait(_currentConfirmTokenSource);
            }
            finally
            {
                _currentConfirmTokenSource.Dispose();
                Interlocked.Exchange(ref _currentConfirmTokenSource, null);

                VisualStateManager.GoToState(this, "DialogHidden", true);
                // ReSharper disable once MethodSupportsCancellation
                await Task.Delay(500);
                _parentOverlayGrid?.Children.Remove(this);
            }

            return _layoutPrimaryButton?.IsEnabled ?? false ?
                new UserFeedbackResult(Title ?? "", Message ?? "", RatingValue) :
                null;
        }

        private static async Task UseTokenAndWait([NotNull] CancellationTokenSource? tokenSource)
        {
            ArgumentNullException.ThrowIfNull(tokenSource, nameof(tokenSource));

            try
            {
                while (true)
                {
                    tokenSource.Token.ThrowIfCancellationRequested();
                    await Task.Delay(1000, tokenSource.Token);
                }
            }
            catch (OperationCanceledException)
            {
                // ignored
            }
        }

        private static Grid FindOverlayGrid([NotNull] XamlRoot? root)
        {
            // XAML root cannot be empty or null!
            ArgumentNullException.ThrowIfNull(root, nameof(root));

            FrameworkElement? parent = root.Content.FindDescendant("OverlayRootGrid", StringComparison.OrdinalIgnoreCase);
            if (parent is not Grid parentAsGrid)
            {
                throw new InvalidOperationException("Cannot find an overlay parent grid with name: \"OverlayRootGrid\" in your XAML layout!");
            }

            return parentAsGrid;
        }
    }
}
