using CollapseLauncher.Extension;
using CollapseLauncher.Helper.Animation;
using CollapseLauncher.Helper.Metadata;
using CollapseLauncher.Pages;
using CollapseLauncher.Statics;
using CommunityToolkit.WinUI;
using Hi3Helper;
using Hi3Helper.Shared.Region;
using Microsoft.UI.Input;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Documents;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Windows.Storage.Streams;
using Windows.UI;

using InnerExtension = CollapseLauncher.Extension.UIElementExtensions;
// ReSharper disable PartialTypeWithSinglePart
// ReSharper disable StringLiteralTypo
// ReSharper disable UnusedMember.Global

#nullable enable
namespace CollapseLauncher.XAMLs.Theme.CustomControls.UserFeedbackDialog
{
    public record UserFeedbackResult(string Title, string Message, double Rating);
    public partial class UserFeedbackDialog : ContentControl
    {
        public UserFeedbackDialog(XamlRoot xamlRoot) : this(xamlRoot, false) { }

        public UserFeedbackDialog(XamlRoot xamlRoot, bool alwaysOnTop)
        {
            _isAlwaysOnTop           =   alwaysOnTop;
            XamlRoot                 =   xamlRoot;
            DefaultStyleKey          =   typeof(UserFeedbackDialog);
            _inverseBooleanConverter ??= new InverseBooleanConverter();
        }

        protected override void OnApplyTemplate()
        {
            // Get the UI element from XAML template
            _layoutTitleGridBackgroundImage = GetTemplateChild(TemplateNameTitleGridBackgroundImage) as Image;
            _layoutTitleGridText            = GetTemplateChild(TemplateNameTitleGridText) as TextBlock;
            _layoutFeedbackTitleInput       = GetTemplateChild(TemplateNameFeedbackTitleInput) as TextBox;
            _layoutFeedbackMessageInput     = GetTemplateChild(TemplateNameFeedbackMessageInput) as TextBox;
            _layoutFeedbackRatingText       = GetTemplateChild(TemplateNameFeedbackRatingText) as TextBlock;
            _layoutFeedbackRatingControl    = GetTemplateChild(TemplateNameFeedbackRatingControl) as RatingControl;
            _layoutPrimaryButton            = GetTemplateChild(TemplateNamePrimaryButton) as Button;
            _layoutCloseButton              = GetTemplateChild(TemplateNameCloseButton) as Button;

            // Assign the cursor
            InputCursor pointerCursor = InputSystemCursor.Create(InputSystemCursorShape.Hand);
            _layoutFeedbackRatingControl?.SetCursor(pointerCursor);
            _layoutPrimaryButton?.SetCursor(pointerCursor);
            _layoutCloseButton?.SetCursor(pointerCursor);

            // Assign implicit animations
            _layoutFeedbackTitleInput?.EnableImplicitAnimation();
            _layoutFeedbackMessageInput?.EnableImplicitAnimation();
            _layoutPrimaryButton?.EnableImplicitAnimation();
            _layoutCloseButton?.EnableImplicitAnimation();

            // Assign dialog title image background
            GamePresetProperty currentGameProperty = GamePropertyVault.GetCurrentGameProperty();
            GameNameType       gameNameType        = currentGameProperty.GamePreset.GameType;
            string relFilePath = gameNameType switch
            {
                GameNameType.Zenless => @"Assets\\Images\\GamePoster\\headerposter_zzz.png",
                GameNameType.Honkai => @"Assets\\Images\\GamePoster\\headerposter_honkai.png",
                GameNameType.StarRail => @"Assets\\Images\\GamePoster\\headerposter_starrail.png",
                _ => @"Assets\\Images\\GamePoster\\headerposter_genshin.png"
            };
            FileInfo filePathInfo = new(Path.Combine(LauncherConfig.AppExecutableDir, relFilePath));
            if (filePathInfo.Exists)
            {
                using FileStream fileStream = filePathInfo.OpenRead();
                using IRandomAccessStream accessStream = fileStream.AsRandomAccessStream();
                BitmapImage bitmapImage = new();
                bitmapImage.SetSource(accessStream);

                _layoutTitleGridBackgroundImage!.Source = bitmapImage;
            }

            // Set initial rating value
            RatingValue = 5d;

            // Assign events and binding, and the Control's base OnApplyTemplate
            AssignEvents();
            AssignBindings();
            AssignLocalization();
            base.OnApplyTemplate();
        }

        private void AssignLocalization()
        {
            _layoutTitleGridText?.BindProperty(TextBlock.TextProperty, Locale.Lang._Dialogs, nameof(Locale.Lang._Dialogs.UserFeedback_DialogTitle));
            _layoutFeedbackTitleInput?.BindProperty(TextBox.PlaceholderTextProperty, Locale.Lang._Dialogs, nameof(Locale.Lang._Dialogs.UserFeedback_TextFieldTitlePlaceholder));
            _layoutFeedbackMessageInput?.BindProperty(TextBox.PlaceholderTextProperty, Locale.Lang._Dialogs, nameof(Locale.Lang._Dialogs.UserFeedback_TextFieldMessagePlaceholder));
            SetTextBoxPropertyHeaderLocale(_layoutFeedbackTitleInput, Locale.Lang._Dialogs.UserFeedback_TextFieldTitleHeader, Locale.Lang._Dialogs.UserFeedback_TextFieldRequired);
            SetTextBoxPropertyHeaderLocale(_layoutFeedbackMessageInput, Locale.Lang._Dialogs.UserFeedback_TextFieldMessageHeader, Locale.Lang._Dialogs.UserFeedback_TextFieldRequired);
            _layoutFeedbackRatingText?.BindProperty(TextBlock.TextProperty, Locale.Lang._Dialogs, nameof(Locale.Lang._Dialogs.UserFeedback_RatingText));
            SetButtonPropertyTextLocale(_layoutPrimaryButton, Locale.Lang._Dialogs, nameof(Locale.Lang._Dialogs.UserFeedback_SubmitBtn));
            SetButtonPropertyTextLocale(_layoutCloseButton, Locale.Lang._Dialogs, nameof(Locale.Lang._Dialogs.UserFeedback_CancelBtn));
        }

        private static void SetButtonPropertyTextLocale(Button? button, object localeObject, string nameOfLocale)
        {
            TextBlock? firstTextBlock = (button?.Content as Grid)?.FindDescendantOrSelf<TextBlock>() ?? button?.Content as TextBlock;
            firstTextBlock?.BindProperty(TextBlock.TextProperty, localeObject, nameOfLocale);
        }

        private static void SetTextBoxPropertyHeaderLocale(TextBox? textBox, string? firstRun, string? secondRun)
        {
            if (textBox?.Header is not TextBlock headerTextBlock)
            {
                return;
            }

            headerTextBlock.Inlines.Clear();
            Run firstInline = new()
            {
                FontWeight = FontWeights.SemiBold,
                FontSize = 14d,
                Text = firstRun + " "
            };
            Run secondInline = new()
            {
                FontWeight = FontWeights.Bold,
                FontSize = 12d,
                Foreground = new SolidColorBrush(InnerExtension.GetApplicationResource<Color>("SystemErrorTextColor")),
                Text = secondRun
            };

            headerTextBlock.Inlines.Add(firstInline);
            headerTextBlock.Inlines.Add(secondInline);
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

            // Bind Visibility property to the UI FeedbackTitleInput
            _layoutFeedbackTitleInput?.BindProperty(VisibilityProperty,
                                                    this,
                                                    nameof(TitleVisibility));

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
            _layoutPrimaryButton!.Click              += OnSubmissionSubmitButtonClick;
            _layoutCloseButton!.Click                += OnSubmissionCancelButtonClick;
            _layoutFeedbackTitleInput!.TextChanged   += OnFeedbackInputsChanged;
            _layoutFeedbackMessageInput!.TextChanged += OnFeedbackInputsChanged;
        }

        private void UnassignEvents()
        {
            Loaded                                   -= OnUILayoutLoaded;
            Unloaded                                 -= OnUILayoutUnloaded;
            _layoutPrimaryButton!.Click              -= OnSubmissionSubmitButtonClick;
            _layoutCloseButton!.Click                -= OnSubmissionCancelButtonClick;
            _layoutFeedbackTitleInput!.TextChanged   -= OnFeedbackInputsChanged;
            _layoutFeedbackMessageInput!.TextChanged -= OnFeedbackInputsChanged;
        }

        public Task<UserFeedbackResult?> ShowAsync()
            => ShowAsync(null);

        public Task<UserFeedbackResult?> ShowAsync(Action<UserFeedbackResult?> actionCallbackOnSubmit)
        {
            return ShowAsync(ActionCallback);
            Task ActionCallback(UserFeedbackResult? result, CancellationToken ctx) => Task.Factory.StartNew(() => actionCallbackOnSubmit(result), ctx, TaskCreationOptions.DenyChildAttach, TaskScheduler.Current);
        }

        public async Task<UserFeedbackResult?> ShowAsync(Func<UserFeedbackResult?, CancellationToken, Task>? actionCallbackTaskOnSubmit)
        {
            _parentOverlayGrid = FindOverlayGrid(XamlRoot, _isAlwaysOnTop);
            int parentGridRowCount    = _parentOverlayGrid?.RowDefinitions.Count ?? 1;
            int parentGridColumnCount = _parentOverlayGrid?.ColumnDefinitions.Count ?? 1;
            _parentOverlayGrid?.AddElementToGridRowColumn(this,
                                                          0,
                                                          0,
                                                          parentGridRowCount,
                                                          parentGridColumnCount);

            _currentConfirmTokenSource ??= new CancellationTokenSource();

            UserFeedbackResult? result;
            try
            {
                await UseTokenAndWait(_currentConfirmTokenSource);
            }
            finally
            {
                DisposeTokenSource();
                result = _isSubmit ?
                    new UserFeedbackResult(Title ?? "", Message ?? "", RatingValue) :
                    null;

                await OnRunningSubmitTask(actionCallbackTaskOnSubmit, result);

                VisualStateManager.GoToState(this, "DialogHidden", true);
                _parentOverlayGrid?.Children.Remove(this);
            }

            return result;
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

        private static Grid FindOverlayGrid([NotNull] XamlRoot? root, bool isAlwaysOnTop)
        {
            // XAML root cannot be empty or null!
            ArgumentNullException.ThrowIfNull(root, nameof(root));

            if (!isAlwaysOnTop)
            {
                FrameworkElement? parent = root.Content.FindDescendant("OverlayRootGrid", StringComparison.OrdinalIgnoreCase);
                if (parent is not Grid parentAsGrid)
                {
                    throw new InvalidOperationException("Cannot find an overlay parent grid with name: \"OverlayRootGrid\" in your XAML layout!");
                }

                return parentAsGrid;
            }

            Grid? topGrid = root.Content as Grid;
            topGrid ??= FindLastChildGrid(root.Content);

            if (topGrid is null)
            {
                throw new InvalidOperationException("Cannot find any or the last grid in your XAML layout!");
            }

            return topGrid;
        }

        private static Grid? FindLastChildGrid(DependencyObject? element)
        {
            int visualTreeCount = VisualTreeHelper.GetChildrenCount(element);
            if (visualTreeCount == 0)
            {
                return null;
            }

            Grid? lastGrid = null;
            for (int i = 0; i < visualTreeCount; i++)
            {
                DependencyObject currentObject = VisualTreeHelper.GetChild(element, i);
                if (currentObject is Grid asGrid)
                {
                    lastGrid = asGrid;
                }
            }

            return lastGrid;
        }
    }
}
