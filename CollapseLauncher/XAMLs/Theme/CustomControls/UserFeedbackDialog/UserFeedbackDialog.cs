﻿using CollapseLauncher.Extension;
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
    /// <summary>
    /// A record of the result from <see cref="UserFeedbackDialog"/>
    /// </summary>
    /// <param name="Title">The title of the feedback</param>
    /// <param name="Message">The message of the feedback</param>
    /// <param name="Rating">The rating of the feedback</param>
    public record UserFeedbackResult(string Title, string Message, double Rating);

    /// <summary>
    /// Creates a UI Dialog for User Feedback which includes the Title, Message and Rating control.
    /// </summary>
    public partial class UserFeedbackDialog : ContentControl
    {
        #region Constructors
        /// <summary>
        /// Creates an instance of <see cref="UserFeedbackDialog"/>
        /// </summary>
        /// <param name="xamlRoot">A <see cref="XamlRoot"/> instance of the current UI element where it spawns from.</param>
        public UserFeedbackDialog(XamlRoot xamlRoot) : this(xamlRoot, false) { }

        /// <summary>
        /// Creates an instance of <see cref="UserFeedbackDialog"/>
        /// </summary>
        /// <param name="xamlRoot">A <see cref="XamlRoot"/> instance of the current UI element where it spawns from.</param>
        /// <param name="alwaysOnTop">Tries to spawn the UI element on top of everything under the <see cref="XamlRoot"/>.</param>
        public UserFeedbackDialog(XamlRoot xamlRoot, bool alwaysOnTop)
        {
            _isAlwaysOnTop           =   alwaysOnTop;
            XamlRoot                 =   xamlRoot;
            DefaultStyleKey          =   typeof(UserFeedbackDialog);
            _inverseBooleanConverter ??= new InverseBooleanConverter();
        }
        #endregion

        #region Override Methods
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
            // Get the title image background and load it.
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

            // Assign events, binding, localization and the Control's base OnApplyTemplate
            AssignEvents();
            AssignBindings();
            AssignLocalization();
            base.OnApplyTemplate();
        }
        #endregion

        #region Public Methods
        /// <summary>
        /// Show the dialog and return the result of the inputs.
        /// </summary>
        /// <returns>Returns a record of <see cref="UserFeedbackResult"/> if submitted, or <c>null</c> if cancelled.</returns>
        public Task<UserFeedbackResult?> ShowAsync()
            => ShowAsync(null);

        /// <summary>
        /// Show the dialog and return the result of the inputs, also run an <see cref="Action"/> callback which feeds the result
        /// to a method where it consumes the <see cref="UserFeedbackResult"/>.
        /// </summary>
        /// <param name="actionCallbackOnSubmit">An <see cref="Action"/> callback where it consumes the result of <see cref="UserFeedbackResult"/> record.</param>
        /// <returns>Returns a record of <see cref="UserFeedbackResult"/> if submitted, or <c>null</c> if cancelled.</returns>
        public Task<UserFeedbackResult?> ShowAsync(Action<UserFeedbackResult?> actionCallbackOnSubmit)
        {
            return ShowAsync(ActionCallback);
            Task ActionCallback(UserFeedbackResult? result, CancellationToken ctx) => Task.Factory.StartNew(() => actionCallbackOnSubmit(result), ctx, TaskCreationOptions.DenyChildAttach, TaskScheduler.Current);
        }

        /// <summary>
        /// Show the dialog and return the result of the inputs, also run an asynchronous <see cref="Task"/> callback which feeds the result
        /// to a method where it consumes the <see cref="UserFeedbackResult"/>.
        /// </summary>
        /// <param name="actionCallbackTaskOnSubmit">An <see cref="Task"/> callback where it consumes the result of <see cref="UserFeedbackResult"/> record asynchronously.</param>
        /// <returns>Returns a record of <see cref="UserFeedbackResult"/> if submitted, or <c>null</c> if cancelled.</returns>
        public async Task<UserFeedbackResult?> ShowAsync(Func<UserFeedbackResult?, CancellationToken, Task>? actionCallbackTaskOnSubmit)
        {
            // Find an overlay grid where the UI element will be spawn to
            _parentOverlayGrid = FindOverlayGrid(XamlRoot, _isAlwaysOnTop);
            // Get the count of the Rows and Column so it can be spanned across the grid.
            int parentGridRowCount = _parentOverlayGrid?.RowDefinitions.Count ?? 1;
            int parentGridColumnCount = _parentOverlayGrid?.ColumnDefinitions.Count ?? 1;
            // Add the UI element to the grid
            _parentOverlayGrid?.AddElementToGridRowColumn(this,
                                                          0,
                                                          0,
                                                          parentGridRowCount,
                                                          parentGridColumnCount);

            // Assign the confirmation button token source
            _currentConfirmTokenSource ??= new CancellationTokenSource();

            UserFeedbackResult? result;
            try
            {
                // Run and wait for the confirmation button token source to be triggered
                await UseTokenAndWait(_currentConfirmTokenSource);
            }
            finally
            {
                // Once one of the button is clicked and the token source invalidated, dispose the token source
                DisposeTokenSource();
                // Get the result. If the _isSubmit returns true as a result of "Submit" button getting clicked, create a record.
                // Otherwise, create a null.
                result = _isSubmit ?
                    new UserFeedbackResult(Title ?? "", Message ?? "", RatingValue) :
                    null;

                // Wait for the callback task to be executed (if any) and pass the result into the callback.
                await OnRunningSubmitTask(actionCallbackTaskOnSubmit, result);

                // Set the visual state as hidden and remove the UI element from the overlay grid.
                VisualStateManager.GoToState(this, "DialogHidden", true);
                _parentOverlayGrid?.Children.Remove(this);
            }

            // Return the result
            return result;
        }
        #endregion

        #region Internal Methods
        private void AssignLocalization()
        {
            // Assign locale for Text header, button, placeholders and stuffs...
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
            // Get the first TextBlock or self as TextBlock if possible
            TextBlock? firstTextBlock = (button?.Content as Grid)?.FindDescendantOrSelf<TextBlock>() ?? button?.Content as TextBlock;

            // Bind the button's TextBlock Text to the locale
            firstTextBlock?.BindProperty(TextBlock.TextProperty, localeObject, nameOfLocale);
        }

        private static void SetTextBoxPropertyHeaderLocale(TextBox? textBox, string? firstRun, string? secondRun)
        {
            // If the header is not a TextBlock, return.
            if (textBox?.Header is not TextBlock headerTextBlock)
            {
                return;
            }

            // Otherwise, try to clear the previous TextBlock's Inlines (if any),
            // then create inlines to be added into the collection.
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

            // Add inlines to the collection.
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

        private static async Task UseTokenAndWait([NotNull] CancellationTokenSource? tokenSource)
        {
            // Throw if tokenSource is null
            ArgumentNullException.ThrowIfNull(tokenSource, nameof(tokenSource));

            try
            {
                // Perform a loop while waiting for the tokenSource to be invalidated.
                while (true)
                {
                    tokenSource.Token.ThrowIfCancellationRequested();
                    await Task.Delay(1000, tokenSource.Token);
                }
            }
            // Ignore the cancellation (invalidation) exception
            catch (OperationCanceledException)
            {
                // ignored
            }
        }

        private static Grid FindOverlayGrid([NotNull] XamlRoot? root, bool isAlwaysOnTop)
        {
            // XAML root cannot be empty or null!
            ArgumentNullException.ThrowIfNull(root, nameof(root));

            // If alwaysOnTop is not preferred, find for a grid called "OverlayRootGrid" under the XamlRoot's Content.
            if (!isAlwaysOnTop)
            {
                FrameworkElement? parent = root.Content.FindDescendant("OverlayRootGrid", StringComparison.OrdinalIgnoreCase);
                if (parent is not Grid parentAsGrid)
                {
                    //If the "OverlayRootGrid" doesn't exist, start searching for any last grid existed.
                    goto FindAnyLastGrid;
                }

                // Otherwise, return the "OverlayRootGrid"
                return parentAsGrid;
            }
        
            FindAnyLastGrid:
            // Assign the XamlRoot's Content as grid. If it's not a grid, find any last child grid
            // on the XamlRoot's VisualTree children.
            Grid? topGrid = root.Content as Grid;
            topGrid ??= FindLastChildGrid(root.Content);

            // If it still cannot find any grid, throw.
            if (topGrid is null)
            {
                throw new InvalidOperationException("Cannot find any or the last grid in your XAML layout!");
            }

            // Otherwise, any grid that have been found.
            return topGrid;
        }

        private static Grid? FindLastChildGrid(DependencyObject? element)
        {
            // Get count of any children existed under the element's VisualTree
            int visualTreeCount = VisualTreeHelper.GetChildrenCount(element);
            if (visualTreeCount == 0)
            {
                // If none is found, return null.
                return null;
            }

            // Find the last grid to be found under the element's VisualTree
            Grid? lastGrid = null;
            for (int i = 0; i < visualTreeCount; i++)
            {
                DependencyObject currentObject = VisualTreeHelper.GetChild(element, i);
                if (currentObject is Grid asGrid)
                {
                    lastGrid = asGrid;
                }
            }

            // Return the result (whether if it's not found/as null, or any last grid)
            return lastGrid;
        }
        #endregion
    }
}
