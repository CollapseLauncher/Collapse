using CollapseLauncher.Extension;
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

            // Assign dialog title image background
            GamePresetProperty currentGameProperty = GamePropertyVault.GetCurrentGameProperty();
            if (currentGameProperty.GamePreset != null)
            {
                GameNameType gameNameType = currentGameProperty.GamePreset.GameType;
                string relFilePath = gameNameType switch
                {
                    GameNameType.Zenless => @"Assets\\Images\\GamePoster\\headerposter_zzz.png",
                    GameNameType.Honkai => @"Assets\\Images\\GamePoster\\headerposter_honkai.png",
                    GameNameType.StarRail => @"Assets\\Images\\GamePoster\\headerposter_starrail.png",
                    _ => @"Assets\\Images\\GamePoster\\headerposter_genshin.png"
                };
                FileInfo filePathInfo = new FileInfo(Path.Combine(LauncherConfig.AppExecutableDir, relFilePath));
                if (filePathInfo.Exists)
                {
                    using FileStream fileStream = filePathInfo.OpenRead();
                    using IRandomAccessStream accessStream = fileStream.AsRandomAccessStream();
                    BitmapImage bitmapImage = new BitmapImage();
                    bitmapImage.SetSource(accessStream);

                    _layoutTitleGridBackgroundImage!.Source = bitmapImage;
                }
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
            SetTextBoxPropertyHeaderLocale(_layoutFeedbackMessageInput, Locale.Lang._Dialogs.UserFeedback_TextFieldTitleHeader, Locale.Lang._Dialogs.UserFeedback_TextFieldRequired);
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
            Run firstInline = new Run
            {
                FontWeight = FontWeights.SemiBold,
                FontSize = 14d,
                Text = firstRun + " "
            };
            Run secondInline = new Run
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
