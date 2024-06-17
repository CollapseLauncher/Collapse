using Hi3Helper;
using Hi3Helper.Data;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace CollapseLauncher
{
    internal partial class FileMigrationProcess
    {
        private void UpdateCountProcessed(FileMigrationProcessUIRef uiRef, string currentPathProcessed)
        {
            Interlocked.Add(ref this.CurrentFileCountMoved, 1);

            string fileCountProcessedString = string.Format(Locale.Lang!._Misc!.PerFromTo!,
            this.CurrentFileCountMoved,
            this.TotalFileCount);

            parentUI!.DispatcherQueue!.TryEnqueue(() =>
            {
                uiRef.fileCountIndicatorSubtitle.Text = fileCountProcessedString;
                uiRef.pathActivitySubtitle.Text = currentPathProcessed;
            });
        }

        private async void UpdateSizeProcessed(FileMigrationProcessUIRef uiRef, long currentRead)
        {
            Interlocked.Add(ref this.CurrentSizeMoved, currentRead);

            if (await CheckIfNeedRefreshStopwatch())
            {
                double percentage = Math.Round((double)this.CurrentSizeMoved / this.TotalFileSize * 100d, 2);
                double speed = this.CurrentSizeMoved / this.ProcessStopwatch!.Elapsed.TotalSeconds;

                lock (uiRef.progressBarIndicator)
                {
                    this.parentUI!.DispatcherQueue!.TryEnqueue(() =>
                    {
                        string speedString = string.Format(Locale.Lang!._Misc!.SpeedPerSec!, ConverterTool.SummarizeSizeSimple(speed));
                        string sizeProgressString = string.Format(Locale.Lang._Misc.PerFromTo!,
                            ConverterTool.SummarizeSizeSimple(this.CurrentSizeMoved),
                            ConverterTool.SummarizeSizeSimple(this.TotalFileSize));

                        uiRef.speedIndicatorSubtitle.Text = speedString;
                        uiRef.fileSizeIndicatorSubtitle.Text = sizeProgressString;
                        uiRef.progressBarIndicator.Value = percentage;
                        uiRef.progressBarIndicator.IsIndeterminate = false;
                    });
                }
            }
        }

        private async ValueTask<bool> CheckIfNeedRefreshStopwatch()
        {
            if (this.EventsStopwatch!.ElapsedMilliseconds > _refreshInterval)
            {
                this.EventsStopwatch.Restart();
                return true;
            }

            await Task.Delay(_refreshInterval);
            return false;
        }
    }
}
