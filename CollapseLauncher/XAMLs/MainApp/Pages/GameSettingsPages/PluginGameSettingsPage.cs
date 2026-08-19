using CollapseLauncher.Plugins;
using CollapseLauncher.Helper;
using CollapseLauncher.GameManagement.ImageBackground;
using Hi3Helper.Plugin.Core.UI.Settings;
using Hi3Helper.Plugin.Core.Utility;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using System;
using System.Globalization;
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

        foreach (GameSettingsSection section in page.Sections)
        {
            sectionsPanel.Children.Add(CreateSection(section));
        }

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
        applyPanel.ColumnDefinitions.Add(new ColumnDefinition());
        applyPanel.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        applyPanel.Children.Add(_statusText);

        Button applyButton = new()
        {
            Content = Locale.Current.Lang?._GameSettingsPage?.ApplyBtn ?? "Apply settings",
            MinWidth = 144,
            CornerRadius = new CornerRadius(16),
            Style = Application.Current.Resources["AccentButtonStyle"] as Style
        };
        applyButton.Click += OnApply;
        Grid.SetColumn(applyButton, 1);
        applyPanel.Children.Add(applyButton);
        Grid.SetRow(applyPanel, 1);
        root.Children.Add(applyPanel);

        return root;
    }

    private FrameworkElement CreateSection(GameSettingsSection section)
    {
        StackPanel panel = new() { Spacing = 8 };
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

        foreach (GameSettingEntry entry in section.Entries)
        {
            panel.Children.Add(CreateEntry(entry));
        }

        return panel;
    }

    private FrameworkElement CreateEntry(GameSettingEntry entry)
    {
        Grid card = new()
        {
            Padding = new Thickness(16, 12, 16, 12),
            ColumnSpacing = 24,
            Background = Application.Current.Resources["CardBackgroundFillColorDefaultBrush"] as Brush,
            CornerRadius = new CornerRadius(8)
        };
        card.ColumnDefinitions.Add(new ColumnDefinition());
        card.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        StackPanel text = new() { VerticalAlignment = VerticalAlignment.Center, Spacing = 2 };
        text.Children.Add(new TextBlock { Text = entry.Title, TextWrapping = TextWrapping.Wrap });
        if (!string.IsNullOrWhiteSpace(entry.Description))
        {
            text.Children.Add(new TextBlock
            {
                Text = entry.Description,
                Opacity = 0.72,
                TextWrapping = TextWrapping.Wrap
            });
        }
        card.Children.Add(text);

        FrameworkElement editor = CreateEditor(entry);
        editor.MinWidth = entry.Kind is GameSettingKind.Toggle ? 0 : 180;
        editor.VerticalAlignment = VerticalAlignment.Center;
        Grid.SetColumn(editor, 1);
        card.Children.Add(editor);
        return card;
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

    private ToggleSwitch CreateToggle(GameSettingEntry entry)
    {
        ToggleSwitch control = new() { IsOn = bool.TryParse(entry.Value, out bool value) && value };
        control.Toggled += (_, _) => SetValue(entry.Key, control.IsOn ? bool.TrueString : bool.FalseString);
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
            Value = ParseNumber(entry.Value, entry.Minimum),
            Width = 220
        };
        control.ValueChanged += (_, args) =>
            SetValue(entry.Key, args.NewValue.ToString(CultureInfo.InvariantCulture));
        return control;
    }

    private ComboBox CreateChoice(GameSettingEntry entry)
    {
        ComboBox control = new();
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
