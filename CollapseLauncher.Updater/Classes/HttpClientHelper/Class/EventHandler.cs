using System;

namespace Hi3Helper.Data
{
    public partial class HttpClientHelper
    {
        public enum State { Downloading, Merging, Completed, Idle, Cancelled }

        public event EventHandler<_DownloadProgress> DownloadProgress;

        public void UpdateProgress(_DownloadProgress input) => DownloadProgress?.Invoke(this, input);

        public class _DownloadProgress
        {
            public _DownloadProgress(long DownloadedSize, long TotalSizeToDownload, int CurrentRead,
                long LastContinuedSize, TimeSpan TimeSpan, State DownloadState = State.Downloading)
            {
                this.DownloadedSize = DownloadedSize;
                this.TotalSizeToDownload = TotalSizeToDownload;
                this._TotalSecond = TimeSpan.TotalSeconds;
                this._LastContinuedSize = LastContinuedSize;
                this.CurrentRead = CurrentRead;
                this.DownloadState = DownloadState;
            }

            private long _LastContinuedSize = 0;
            private double _TotalSecond = 0;

            public long DownloadedSize { get; private set; }
            public long TotalSizeToDownload { get; private set; }
            public double ProgressPercentage => Math.Round((DownloadedSize / (double)TotalSizeToDownload) * 100, 2);
            public int CurrentRead { get; private set; }
            public long CurrentSpeed => (long)(_LastContinuedSize / _TotalSecond);
            public TimeSpan TimeLeft => checked(TimeSpan.FromSeconds((TotalSizeToDownload - DownloadedSize) / CurrentSpeed));
            public State DownloadState { get; set; }
        }
    }
}
