using CollapseLauncher.Extension;
using CollapseLauncher.Helper;
using CollapseLauncher.Interfaces;
using CollapseLauncher.Statics;
using Hi3Helper;
using Hi3Helper.SentryHelper;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media;
using Microsoft.Win32;
using RegistryUtils;
using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Windows.UI;
using WinRT;

// ReSharper disable AsyncVoidMethod

#pragma warning disable IDE0290 uSePriMarY cOnsTrucTor pl3asE. Bro... STFU! Who TF gave them idea to have this useless design pattern??!!
#pragma warning disable IDE0130

#nullable enable
namespace CollapseLauncher.Pages;

[GeneratedBindableCustomProperty]
public abstract partial class GameSettingsPageBase : Page, INotifyPropertyChanged
{
    #region INotifyPropertyChanged
    
    public event PropertyChangedEventHandler? PropertyChanged;

    // ReSharper disable once UnusedMember.Local
    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        // Raise the PropertyChanged event, passing the name of the property whose value has changed.
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
    
    #endregion

    #region Protected and Private Properties
    
    protected IGameSettings?   Settings                 { get; }
    protected RegistryMonitor? RegistryMonitor          { get; }
    protected TextBlock        ApplyText                { get; }
    private   Brush            ApplyTextForeground      { get; }
    private   Brush            ApplyTextForegroundError { get; }

    #endregion

    #region Properties

    protected GamePresetProperty CurrentGameProperty { get; } = GamePropertyVault.GetCurrentGameProperty();

    public string? CustomArgsValue
    {
        get => CurrentGameProperty.GameSettings?.SettingsCustomArgument.CustomArgumentValue ?? "";
        set => SuspendRegistryMonitorOnAction(() =>
        {
            CurrentGameProperty.GameSettings?.SettingsCustomArgument.CustomArgumentValue = value;
            OnPropertyChanged();
        });
    }

    public bool IsUseCustomArgs
    {
        get => CurrentGameProperty.GameSettings?.SettingsCollapseMisc.UseCustomArguments ?? false;
        set => SuspendRegistryMonitorOnAction(() =>
        {
            CurrentGameProperty.GameSettings?.SettingsCollapseMisc.UseCustomArguments = value;
            OnPropertyChanged();
        });
    }

    #endregion

    protected GameSettingsPageBase(IGameSettings? settings, RegistryKey? gameRegistryKeyPath)
    {
        if (gameRegistryKeyPath != null)
        {
            RegistryMonitor            =  new RegistryMonitor(gameRegistryKeyPath);
            RegistryMonitor.RegChanged += OnRegistryChanged;
        }

        Settings = settings;

        Loaded   += OnLoaded;
        Unloaded += OnUnloaded;
        ApplyText = new TextBlock
        {
            Margin              = new Thickness(16, -4, 0, 0),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment   = VerticalAlignment.Center,
            Style               = UIElementExtensions.GetApplicationResource<Style>("BodyStrongTextBlockStyle"),
            TextWrapping        = TextWrapping.Wrap,
            Visibility          = Visibility.Collapsed
        };
        ApplyTextForeground      = ApplyText.Foreground;
        ApplyTextForegroundError = new SolidColorBrush(new Color { A = 255, R = 255 });
    }

    ~GameSettingsPageBase()
    {
        RegistryMonitor?.Dispose();
    }

    protected virtual void OnLoaded(object? sender, RoutedEventArgs args)
    {
        Settings?.ReloadSettings();
        RegistryMonitor?.Start();
    }

    protected virtual void OnUnloaded(object? sender, RoutedEventArgs args)
    {
        RegistryMonitor?.Stop();
    }

    protected virtual void OnApplyButtonClick(object sender, RoutedEventArgs args)
    {
        SuspendRegistryMonitorOnAction(Impl);
        return;

        void Impl()
        {
            try
            {
                Settings?.SaveSettings();
                SetApplyTextStatus("Lang._StarRailGameSettingsPage.SettingsApplied");
            }
            catch (Exception ex)
            {
                Logger.LogWriteLine($"[GSP Module] An error has occurred while trying to applying the game settings!\r\n{ex}", LogType.Error, true);
                SetApplyTextStatus(ex.Message, true);
                ErrorSender.SendException(ex);
                SentryHelper.ExceptionHandler(ex);
            }
        }
    }

    protected virtual async void OnRegistryExportButtonClick(object sender, RoutedEventArgs args)
    {
        await SuspendRegistryMonitorOnActionAsync(Impl);
        return;

        async Task Impl()
        {
            try
            {
                Exception? exc = await (Settings?.ExportSettings() ?? Task.FromResult<Exception?>(null));

                if (exc != null) throw exc;
                SetApplyTextStatus("Lang._GameSettingsPage.SettingsRegExported");
            }
            catch (OperationCanceledException)
            {
                SetApplyTextStatus("Lang._GameSettingsPage.SettingsRegErr1", true);
            }
            catch (Exception ex)
            {
                Logger.LogWriteLine($"[GSP Module] An error has occurred while trying to exporting the registry!\r\n{ex}", LogType.Error, true);
                SetApplyTextStatus(ex.Message, true);
                ErrorSender.SendException(ex);
                SentryHelper.ExceptionHandler(ex);
            }
        }
    }

    protected virtual async void OnRegistryImportButtonClick(object sender, RoutedEventArgs args)
    {
        await SuspendRegistryMonitorOnActionAsync(Impl);
        return;

        async Task Impl()
        {
            try
            {
                Exception? exc = await (Settings?.ImportSettings() ?? Task.FromResult<Exception?>(null));

                if (exc != null) throw exc;
                SetApplyTextStatus("Lang._GameSettingsPage.SettingsRegImported");
            }
            catch (OperationCanceledException)
            {
                SetApplyTextStatus("Lang._GameSettingsPage.SettingsRegErr1", true);
            }
            catch (Exception ex)
            {
                Logger.LogWriteLine($"[GSP Module] An error has occurred while trying to exporting the registry!\r\n{ex}", LogType.Error, true);
                SetApplyTextStatus(ex.Message, true);
                ErrorSender.SendException(ex);
                SentryHelper.ExceptionHandler(ex);
            }
        }
    }

    protected virtual void SuspendRegistryMonitorOnAction(Action action)
    {
        RegistryMonitor?.Stop();
        try
        {
            action();
        }
        catch (Exception ex)
        {
            Logger.LogWriteLine($"[GSP Module] An error has occurred while performing SuspendRegistryMonitorOnAction: {ex}",
                                LogType.Error,
                                true);
            ErrorSender.SendException(ex);
            SentryHelper.ExceptionHandler(ex);
        }
        finally
        {
            RegistryMonitor?.Start();
        }
    }

    protected virtual async Task SuspendRegistryMonitorOnActionAsync(Func<Task> action)
    {
        RegistryMonitor?.Stop();
        try
        {
            await action();
        }
        catch (Exception ex)
        {
            Logger.LogWriteLine($"[GSP Module] An error has occurred while performing SuspendRegistryMonitorOnAction: {ex}",
                                LogType.Error,
                                true);
            ErrorSender.SendException(ex);
            SentryHelper.ExceptionHandler(ex);
        }
        finally
        {
            RegistryMonitor?.Start();
        }
    }

    protected void SetApplyTextContainer(Grid grid,
                                         int  gridColumn     = 0,
                                         int  gridRow        = 0,
                                         int  gridColumnSpan = 0,
                                         int  gridRowSpan    = 0) =>
        grid.AddElementToGridRowColumn(ApplyText, gridRow, gridColumn, gridRowSpan, gridColumnSpan);

    protected void SetApplyTextStatus(string? localePropertyPath = null,
                                      bool    isError            = false)
    {
        DispatcherQueueExtensions.TryEnqueue(() =>
        {
            if (string.IsNullOrEmpty(localePropertyPath))
            {
                ApplyText.Visibility = Visibility.Collapsed;
                return;
            }

            ApplyText.Visibility = Visibility.Visible;
            if (localePropertyPath.StartsWith("Lang"))
            {
                ApplyText.BindProperty(TextBlock.TextProperty,
                                       Locale.Current,
                                       localePropertyPath,
                                       bindingMode: BindingMode.OneWay,
                                       sourceTrigger: UpdateSourceTrigger.PropertyChanged);
            }
            else
            {
                ApplyText.Text = localePropertyPath;
            }

            ApplyText.Foreground = isError ? ApplyTextForegroundError : ApplyTextForeground;
        });
    }

    private void OnRegistryChanged(object? sender, EventArgs e)
    {
        Logger.LogWriteLine("[GSP Module] RegistryMonitor has detected registry change outside of the launcher! Reloading the page...", LogType.Warning, true);
        DispatcherQueue?.TryEnqueue(MainFrameChanger.ReloadCurrentMainFrame);
    }
}
