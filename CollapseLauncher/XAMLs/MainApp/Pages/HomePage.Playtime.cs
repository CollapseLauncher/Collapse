using CollapseLauncher.GamePlaytime;
using Hi3Helper;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media.Animation;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.UI.Xaml.Media;
using static CollapseLauncher.Dialogs.SimpleDialogs;
using static Hi3Helper.Locale;
using static Hi3Helper.Logger;

// ReSharper disable InconsistentNaming
// ReSharper disable SwitchStatementHandlesSomeKnownEnumValuesWithDefault
// ReSharper disable AsyncVoidMethod
// ReSharper disable StringLiteralTypo
// ReSharper disable IdentifierTypo
// ReSharper disable CommentTypo
// ReSharper disable SwitchStatementMissingSomeEnumCasesNoDefault
// ReSharper disable CheckNamespace

namespace CollapseLauncher.Pages;

public partial class HomePage
{
    private void ForceUpdatePlaytimeButton_Click(object sender, RoutedEventArgs e)
    {
        if (_cachedIsGameRunning) return;

        UpdatePlaytime(null, CurrentGameProperty.GamePlaytime.CollapsePlaytime);
        PlaytimeFlyout.ShowAt(PlaytimeBtn);
    }

    private async void ChangePlaytimeButton_Click(object sender, RoutedEventArgs e)
    {
        if (await Dialog_ChangePlaytime() != ContentDialogResult.Primary) return;

        int mins  = int.Parse("0" + MinutePlaytimeTextBox.Text);
        int hours = int.Parse("0" + HourPlaytimeTextBox.Text);

        TimeSpan time                = TimeSpan.FromMinutes(hours * 60 + mins);
        if (time.Hours > 99999) time = new TimeSpan(99999, 59, 0);

        CurrentGameProperty.GamePlaytime.Update(time, true);
        PlaytimeFlyout.Hide();
    }

    private async void ResetPlaytimeButton_Click(object sender, RoutedEventArgs e)
    {
        if (await Dialog_ResetPlaytime() != ContentDialogResult.Primary) return;

        CurrentGameProperty.GamePlaytime.Reset();
        PlaytimeFlyout.Hide();
    }

    private async void SyncDbPlaytimeButton_Click(object sender, RoutedEventArgs e)
    {
        Button button = sender as Button;
        if (sender != null)
            if (button != null)
                button.IsEnabled = false;

        try
        {
            SyncDbPlaytimeBtnGlyph.Glyph = "\uf110"; // Loading
            SyncDbPlaytimeBtnText.Text   = Lang._HomePage.GamePlaytime_Idle_SyncDbSyncing;
            await CurrentGameProperty.GamePlaytime.CheckDb(true);
                
            await Task.Delay(500);
            
            SyncDbPlaytimeBtnGlyph.Glyph = "\uf00c"; // Completed (check)
            SyncDbPlaytimeBtnText.Text   = Lang._Misc.Completed + "!";
            await Task.Delay(1000);
            
            SyncDbPlaytimeBtnGlyph.Glyph = "\uf021"; // Default
            SyncDbPlaytimeBtnText.Text   = Lang._HomePage.GamePlaytime_Idle_SyncDb;
        }
        catch (Exception ex)
        {
            LogWriteLine($"Failed when trying to sync playtime to database!\r\n{ex}", LogType.Error, true);
            ErrorSender.SendException(ex);
                
            SyncDbPlaytimeBtnGlyph.Glyph = "\uf021"; // Default
            SyncDbPlaytimeBtnText.Text   = Lang._HomePage.GamePlaytime_Idle_SyncDb;
        }
        finally
        {
            if (sender != null)
                if (button != null) 
                    button.IsEnabled = true;
        }
    }

    private void NumberValidationTextBox(TextBox sender, TextBoxBeforeTextChangingEventArgs args)
    {
        sender.MaxLength = sender == HourPlaytimeTextBox ? 5 : 3;
        args.Cancel      = args.NewText.Any(c => !char.IsDigit(c));
    }

    private void UpdatePlaytime(object sender, CollapsePlaytime playtime)
    {
        DispatcherQueue.TryEnqueue(() =>
                                   {
                                       PlaytimeMainBtn.Text = FormatTimeStamp(playtime.TotalPlaytime);
                                       HourPlaytimeTextBox.Text = (playtime.TotalPlaytime.Days * 24 + playtime.TotalPlaytime.Hours).ToString();
                                       MinutePlaytimeTextBox.Text = playtime.TotalPlaytime.Minutes.ToString();

                                       string lastPlayed = Lang._HomePage.GamePlaytime_Stats_NeverPlayed;
                                       if (playtime.LastPlayed != null)
                                       {
                                           DateTime? last = playtime.LastPlayed?.ToLocalTime();
                                           lastPlayed = string.Format(Lang._HomePage.GamePlaytime_DateDisplay, last?.Day,
                                                                      last?.Month, last?.Year, last?.Hour, last?.Minute);
                                       }

                                       PlaytimeStatsDaily.Text       = FormatTimeStamp(playtime.DailyPlaytime);
                                       PlaytimeStatsWeekly.Text      = FormatTimeStamp(playtime.WeeklyPlaytime);
                                       PlaytimeStatsMonthly.Text     = FormatTimeStamp(playtime.MonthlyPlaytime);
                                       PlaytimeStatsLastSession.Text = FormatTimeStamp(playtime.LastSession);
                                       PlaytimeStatsLastPlayed.Text  = lastPlayed;
                                   });
        return;

        static string FormatTimeStamp(TimeSpan time) => string.Format(Lang._HomePage.GamePlaytime_Display, time.Days * 24 + time.Hours, time.Minutes);
    }

    private void ShowPlaytimeStatsFlyout(object sender, RoutedEventArgs e)
    {
        FlyoutBase.ShowAttachedFlyout(PlaytimeBtn);
    }

    private void HidePlaytimeStatsFlyout(object sender, PointerRoutedEventArgs e)
    {
        PlaytimeStatsFlyout.Hide();
        PlaytimeStatsToolTip.IsOpen = false;
    }

    private void PlaytimeStatsFlyout_OnOpened(object sender, object e)
    {
        // Match PlaytimeStatsFlyout and set its transition animation offset to 0 (but keep animation itself)
        IReadOnlyList<Popup> popups = VisualTreeHelper.GetOpenPopupsForXamlRoot(PlaytimeBtn.XamlRoot);
        foreach (var popup in popups.Where(x => x.Child is FlyoutPresenter {Content: Grid {Tag: "PlaytimeStatsFlyoutGrid"}}))
        {
            var transition = popup.ChildTransitions[0] as PopupThemeTransition;
            transition!.FromVerticalOffset = 0;
        }
    }
}