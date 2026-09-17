using CollapseLauncher.Plugins;
using CollapseLauncher.Helper;
using CollapseLauncher.GameManagement.ImageBackground;
using Hi3Helper.Plugin.Core.UI.Settings;
using Hi3Helper.Plugin.Core.Utility;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Media;
using System;
using System.Globalization;
using Microsoft.UI.Text;
using static CollapseLauncher.Statics.GamePropertyVault;

#nullable enable
namespace CollapseLauncher.Pages;

/// <summary>
/// Renders the declarative game settings page exposed by a v0.1.6 plugin.
/// </summary>
public sealed partial class PluginGameSettingsPage : Page
{
    private readonly GameSettingsExtension.GameSettingsContext _context;
    private readonly TextBlock _statusText = new()
    {
        Margin = new Thickness(16, 0, 0, 0),
        VerticalAlignment = VerticalAlignment.Center,
        TextWrapping = TextWrapping.Wrap
    };

    public PluginGameSettingsPage()
    {
        InitializeComponent();

        ImageBackgroundManager.Shared.IsBackgroundElevated = true;
        ImageBackgroundManager.Shared.ForegroundOpacity    = 0d;
        ImageBackgroundManager.Shared.SmokeOpacity         = 1d;

        NavigationCacheMode = Microsoft.UI.Xaml.Navigation.NavigationCacheMode.Disabled;

        if (GetCurrentGameProperty().GameVersion.GamePreset is not PluginPresetConfigWrapper preset)
        {
            throw new InvalidOperationException("The current game preset is not provided by a plugin");
        }

        _context = preset.GameSettingsContext;
        Content = CreateContent();
    }

    private UIElement CreateContent()
    {
        if (!_context.TryGetPage(out GameSettingsPage? page, out Exception? error) || page == null)
        {
            return new TextBlock
            {
                Margin = new Thickness(32, 40, 32, 32),
                Text = error?.Message ?? "This plugin did not provide a game settings page.",
                TextWrapping = TextWrapping.Wrap
            };
        }

        Grid root = new();
        root.RowDefinitions.Add(new RowDefinition());
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        StackPanel sectionsPanel = new() { Margin = new Thickness(32, 40, 32, 32), Spacing = 24 };
        if (!string.IsNullOrWhiteSpace(page.Title))
        {
            sectionsPanel.Children.Add(new TextBlock
            {
                Text = page.Title,
                Style = Application.Current.Resources["TitleLargeTextBlockStyle"] as Style,
                TextWrapping = TextWrapping.Wrap
            });
        }

        Grid sectionGrid = new() { ColumnSpacing = 32, RowSpacing = 28 };
        foreach (GameSettingsSection section in page.Sections)
        {
            // Informational sections, including plugin warnings, remain full width.
            if (section.Entries.Count == 0)
                sectionsPanel.Children.Add(CreateSection(section));
            else
                sectionGrid.Children.Add(CreateSection(section));
        }

        sectionsPanel.Children.Add(sectionGrid);
        sectionGrid.SizeChanged += (_, args) => ArrangeGrid(sectionGrid, args.NewSize.Width >= 900 ? 2 : 1);
        ArrangeGrid(sectionGrid, 1);

        ScrollViewer scrollViewer = new()
        {
            Content = sectionsPanel,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto
        };
        root.Children.Add(scrollViewer);

        Grid applyPanel = new()
        {
            Padding = new Thickness(32, 16, 32, 16),
            Background = Application.Current.Resources["GameSettingsApplyGridBrush"] as Brush
        };
        applyPanel.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        applyPanel.ColumnDefinitions.Add(new ColumnDefinition());
        Grid.SetColumn(_statusText, 1);
        applyPanel.Children.Add(_statusText);

        Button applyButton = new()
        {
            Content = Locale.Current.Lang?._GameSettingsPage?.ApplyBtn ?? "Apply settings",
            MinWidth = 144,
            CornerRadius = new CornerRadius(16),
            Style = Application.Current.Resources["AccentButtonStyle"] as Style
        };
        applyButton.Click += OnApply;
        Grid.SetColumn(applyButton, 0);
        applyPanel.Children.Add(applyButton);
        Grid.SetRow(applyPanel, 1);
        root.Children.Add(applyPanel);

        return root;
    }

    private static void ArrangeGrid(Grid grid, int columns)
    {
        int rows = (grid.Children.Count + columns - 1) / columns;
        if (grid.ColumnDefinitions.Count == columns && grid.RowDefinitions.Count == rows)
            return;

        grid.ColumnDefinitions.Clear();
        grid.RowDefinitions.Clear();
        for (int column = 0; column < columns; column++)
            grid.ColumnDefinitions.Add(new ColumnDefinition());
        for (int row = 0; row < rows; row++)
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        for (int index = 0; index < grid.Children.Count; index++)
        {
            FrameworkElement child = (FrameworkElement)grid.Children[index];
            Grid.SetColumn(child, index % columns);
            Grid.SetRow(child, index / columns);
        }
    }

    private FrameworkElement CreateSection(GameSettingsSection section)
    {
        StackPanel panel = new() { Spacing = 12, VerticalAlignment = VerticalAlignment.Top };
        panel.Children.Add(new TextBlock
        {
            Text = section.Title,
            Style = Application.Current.Resources["SubtitleTextBlockStyle"] as Style,
            TextWrapping = TextWrapping.Wrap
        });

        if (!string.IsNullOrWhiteSpace(section.Description))
        {
            panel.Children.Add(new TextBlock
            {
                Text = section.Description,
                Opacity = 0.72,
                TextWrapping = TextWrapping.Wrap
            });
        }

        Grid entries = new() { ColumnSpacing = 16, RowSpacing = 16 };
        foreach (GameSettingEntry entry in section.Entries)
            entries.Children.Add(CreateEntry(entry));

        if (entries.Children.Count > 0)
        {
            entries.SizeChanged += (_, args) =>
                ArrangeGrid(entries, args.NewSize.Width >= 520 ? 3 : args.NewSize.Width >= 340 ? 2 : 1);
            ArrangeGrid(entries, 1);
            panel.Children.Add(entries);
        }
        return panel;
    }

    private FrameworkElement CreateEntry(GameSettingEntry entry)
    {
        FrameworkElement editor = CreateEditor(entry);
        editor.HorizontalAlignment = HorizontalAlignment.Stretch;
        editor.VerticalAlignment = VerticalAlignment.Top;
        AutomationProperties.SetName(editor, entry.Title);

        StackPanel panel = new() { Spacing = 6 };
        if (entry.Kind != GameSettingKind.Toggle)
        {
            panel.Children.Add(new TextBlock
            {
                Text = entry.Title,
                FontWeight = FontWeights.SemiBold,
                TextWrapping = TextWrapping.Wrap
            });
        }
        panel.Children.Add(editor);
        if (!string.IsNullOrWhiteSpace(entry.Description))
        {
            panel.Children.Add(new TextBlock
            {
                Text = entry.Description,
                FontSize = 12,
                Opacity = 0.72,
                TextWrapping = TextWrapping.Wrap
            });
            AutomationProperties.SetHelpText(editor, entry.Description);
        }
        return panel;
    }

    private FrameworkElement CreateEditor(GameSettingEntry entry) => entry.Kind switch
    {
        GameSettingKind.Toggle => CreateToggle(entry),
        GameSettingKind.Text => CreateText(entry),
        GameSettingKind.Number => CreateNumber(entry),
        GameSettingKind.Slider => CreateSlider(entry),
        GameSettingKind.Choice => CreateChoice(entry),
        _ => throw new ArgumentOutOfRangeException(nameof(entry.Kind))
    };

    private CheckBox CreateToggle(GameSettingEntry entry)
    {
        CheckBox control = new()
        {
            Content = new TextBlock { Text = entry.Title, TextWrapping = TextWrapping.Wrap },
            IsChecked = bool.TryParse(entry.Value, out bool value) && value
        };
        control.Checked += (_, _) => SetValue(entry.Key, bool.TrueString);
        control.Unchecked += (_, _) => SetValue(entry.Key, bool.FalseString);
        return control;
    }

    private TextBox CreateText(GameSettingEntry entry)
    {
        TextBox control = new() { Text = entry.Value, PlaceholderText = entry.Placeholder };
        control.TextChanged += (_, _) => SetValue(entry.Key, control.Text);
        return control;
    }

    private NumberBox CreateNumber(GameSettingEntry entry)
    {
        NumberBox control = new()
        {
            Minimum = entry.Minimum,
            Maximum = entry.Maximum,
            SmallChange = entry.Step,
            SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Compact,
            Value = ParseNumber(entry.Value, entry.Minimum)
        };
        control.ValueChanged += (_, args) =>
        {
            if (!double.IsNaN(args.NewValue))
            {
                SetValue(entry.Key, args.NewValue.ToString(CultureInfo.InvariantCulture));
            }
        };
        return control;
    }

    private Slider CreateSlider(GameSettingEntry entry)
    {
        Slider control = new()
        {
            Minimum = entry.Minimum,
            Maximum = entry.Maximum,
            StepFrequency = entry.Step,
            Style = Application.Current.Resources["FatSliderStyle"] as Style,
            TickFrequency = Math.Max(entry.Step, (entry.Maximum - entry.Minimum) / 10),
            TickPlacement = Microsoft.UI.Xaml.Controls.Primitives.TickPlacement.Outside,
            Value = ParseNumber(entry.Value, entry.Minimum)
        };
        control.ValueChanged += (_, args) =>
            SetValue(entry.Key, args.NewValue.ToString(CultureInfo.InvariantCulture));
        return control;
    }

    private ComboBox CreateChoice(GameSettingEntry entry)
    {
        ComboBox control = new() { CornerRadius = new CornerRadius(14) };
        foreach (GameSettingChoice choice in entry.Choices ?? [])
        {
            ComboBoxItem item = new() { Content = choice.Title, Tag = choice.Value };
            control.Items.Add(item);
            if (string.Equals(choice.Value, entry.Value, StringComparison.Ordinal))
            {
                control.SelectedItem = item;
            }
        }

        control.SelectionChanged += (_, _) =>
        {
            if (control.SelectedItem is ComboBoxItem { Tag: string value })
            {
                SetValue(entry.Key, value);
            }
        };
        return control;
    }

    private static double ParseNumber(string value, double fallback) =>
        double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out double result) ? result : fallback;

    private void SetValue(string key, string value)
    {
        try
        {
            _context.SetValue(key, value);
            SetStatus(null);
        }
        catch (Exception ex)
        {
            SetStatus(ex.Message, true);
        }
    }

    private void OnApply(object sender, RoutedEventArgs args)
    {
        try
        {
            _context.Apply();
            SetStatus(Locale.Current.Lang?._GameSettingsPage?.SettingsApplied ?? "Settings applied.");
        }
        catch (Exception ex)
        {
            SetStatus(ex.Message, true);
        }
    }

    private void SetStatus(string? text, bool isError = false)
    {
        _statusText.Text = text ?? string.Empty;
        _statusText.Foreground = isError ? new SolidColorBrush(Colors.IndianRed) : null;
    }
}
