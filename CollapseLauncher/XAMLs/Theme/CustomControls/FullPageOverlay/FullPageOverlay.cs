using CollapseLauncher.Extension;
using CollapseLauncher.Pages;
using Hi3Helper;
using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Shapes;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

#nullable enable
namespace CollapseLauncher.XAMLs.Theme.CustomControls.FullPageOverlay;

public partial class FullPageOverlay : ContentControl
{
    #region Constructors
    public FullPageOverlay(FrameworkElement content, bool alwaysOnTop = false) : this(content, content.XamlRoot, alwaysOnTop) { }

    public FullPageOverlay(FrameworkElement content, XamlRoot xamlRoot, bool alwaysOnTop = false)
    {
        Content            = content;
        XamlRoot           = xamlRoot;
        _isAlwaysOnTop     = alwaysOnTop;
        _parentOverlayGrid = XamlRoot.FindOverlayGrid(_isAlwaysOnTop);
    }
    #endregion

    #region Override Methods
    protected override void OnApplyTemplate()
    {
        // Get the UI element from XAML template
        _layoutSmokeBackgroundGrid = GetTemplateChild(TemplateNameSmokeLayerBackground) as Rectangle;
        _layoutContentPresenter    = GetTemplateChild(TemplateNamePresenter) as ContentPresenter;
        _layoutOverlayTitleGrid    = GetTemplateChild(TemplateNameOverlayTitleGrid) as Grid;
        LayoutCloseButton          = GetTemplateChild(TemplateNameCloseButton) as Button;

        // Set cursor type
        InputCursor pointerCursor = InputSystemCursor.Create(InputSystemCursorShape.Hand);
        LayoutCloseButton?.SetCursor(pointerCursor);

        // Initialize size type
        AssignContentFrameSize(Size);

        AssignEvents();
        AssignBindings();
        base.OnApplyTemplate();
    }
    #endregion

    #region Public Methods
    public async Task ShowAsync()
    {
        using (ThisThreadLock.EnterScope())
        {
            // Get the count of the Rows and Column so it can be spanned across the grid.
            int parentGridRowCount    = _parentOverlayGrid.RowDefinitions.Count;
            int parentGridColumnCount = _parentOverlayGrid.ColumnDefinitions.Count;
            // Add the UI element to the grid
            _parentOverlayGrid.AddElementToGridRowColumn(this,
                                                         0,
                                                         0,
                                                         parentGridRowCount,
                                                         parentGridColumnCount);

            // Assign if CTS is null or cancelled already.
            if (_closeCts is null || _closeCts.IsCancellationRequested)
            {
                _closeCts = new CancellationTokenSource();
            }

            // Update the drag area of the app
            CurrentlyOpenedOverlays.Add(this);
        }

        await Task.Delay(300);

        using (ThisThreadLock.EnterScope())
        {
            ChangeTitleDragArea.Change(ChangeTitleDragArea.CurrentDragAreaType | DragAreaTemplate.OverlayOpened);
        }

        try
        {
            // Wait until close button is invalidating the token.
            _closeCts.Token.ThrowIfCancellationRequested();
            await Task.Delay(Timeout.InfiniteTimeSpan, _closeCts.Token);
        }
        catch when (_closeCts.IsCancellationRequested)
        {
            // Ignored
        }
        finally
        {
            using (ThisThreadLock.EnterScope())
            {
                // Dispose the CTS
                _closeCts.Dispose();

                // Set the visual state as hidden and remove the UI element from the overlay grid.
                VisualStateManager.GoToState(this, "OverlayHidden", true);
                _parentOverlayGrid.Children.Remove(this);

                // Update the drag area of the app
                CurrentlyOpenedOverlays.Remove(this);
                if (CurrentlyOpenedOverlays.Count == 0)
                {
                    // Remove DragAreaTemplate.OverlayOpened flag if there's no any overlays opened anymore and update it 
                    ChangeTitleDragArea.Change(ChangeTitleDragArea.CurrentDragAreaType & ~DragAreaTemplate.OverlayOpened);
                }
            }
        }
    }

    public void Hide()
    {
        using (ThisThreadLock.EnterScope())
        {
            _closeCts?.Cancel();
        }
    }
    #endregion

    #region Internal Methods
    private static void AssignContentFrameSizeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is FullPageOverlay asPageOverlay)
        {
            asPageOverlay.AssignContentFrameSize((FullPageOverlaySize)e.NewValue);
        }
    }

    private void AssignContentFrameSize(FullPageOverlaySize size)
    {
        if (_layoutSmokeBackgroundGrid is null ||
            _layoutContentPresenter is null ||
            LayoutCloseButton is null)
        {
            return;
        }

        string overlaySizeStateString = $"OverlaySize{size}";

        VisualStateManager.GoToState(this, "OverlayShowing", true);
        VisualStateManager.GoToState(this, overlaySizeStateString, false);
    }

    private void AssignEvents()
    {
        using (ThisThreadLock.EnterScope())
        {
            Loaded                             += OnUILayoutLoaded;
            Unloaded                           += OnUILayoutUnloaded;
            UpdateBindingsInvoker.UpdateEvents += UpdateBindings;

            if (LayoutCloseButton != null)
            {
                LayoutCloseButton.Click += InvalidateTokenOnClose;
            }
        }
    }

    private void UnassignEvents()
    {
        using (ThisThreadLock.EnterScope())
        {
            Loaded                             -= OnUILayoutLoaded;
            Unloaded                           -= OnUILayoutUnloaded;
            UpdateBindingsInvoker.UpdateEvents -= UpdateBindings;

            if (LayoutCloseButton != null)
            {
                LayoutCloseButton.Click -= InvalidateTokenOnClose;
            }
        }
    }

    private void InvalidateTokenOnClose(object sender, RoutedEventArgs _)
    {
        if (sender is Button asButton)
        {
            asButton.IsEnabled = false;
        }

        Hide();
    }

    private void UpdateBindings(object? sender, EventArgs e)
    {
        ChangeTitleDragArea.UpdateLayout();
        AssignBindings();
    }

    private void AssignBindings()
    {
        // Binding close button text
        if (LayoutCloseButton is { Content: Grid asCloseButtonGrid } &&
            asCloseButtonGrid.Children.FirstOrDefault() is TextBlock asCloseButtonTextBlock)
        {
            asCloseButtonTextBlock.BindProperty(TextBlock.TextProperty, Locale.Lang._Misc, nameof(Locale.Lang._Misc.CloseOverlay));
            asCloseButtonTextBlock.UpdateLayout();
        }

        // Binding overlay title visibility
        if (_layoutOverlayTitleGrid?.Children.OfType<TextBlock>().FirstOrDefault() is { } asOverlayTitleTextBlock)
        {
            asOverlayTitleTextBlock.BindProperty(VisibilityProperty, this, nameof(OverlayTitle), StringToVisibilityConverter);
        }

        // Try update overlay title if title source is available
        string? titleGet = OverlayTitleSource?.Invoke();
        if (titleGet != null)
        {
            OverlayTitle = titleGet;
        }
    }
    #endregion

    #region Private Converter
    private static readonly StringToVisibilityConverter StringToVisibilityConverter = new();
    #endregion
}
