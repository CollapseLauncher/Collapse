using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.IO;
using System.Net;
using System.Diagnostics;
using System.Net.Http;

namespace Hi3Helper.Data
{
    public partial class HttpClientHelper
    {
        private Stopwatch _Stopwatch;
        private string _InputURL = "", _OutputPath = "";
        private State _DownloadState;
        private bool _IsFileAlreadyCompleted = false;

        public HttpClientHelper(bool IgnoreCompression = false, int maxRetryCount = 5, float maxRetryTimeout = 1)
        {
            this._Stopwatch = new Stopwatch();
            this._OutputStream = new MemoryStream();
            this._ThreadList = new List<Task>();
            this._ThreadMaxRetry = maxRetryCount;
            this._ThreadRetryDelay = maxRetryTimeout;
            this._DownloadState = State.Idle;

            HttpClientHandler _httpHandler = new HttpClientHandler
            {
                AllowAutoRedirect = true,
                UseCookies = true,
                MaxConnectionsPerServer = 32,
                AutomaticDecompression = IgnoreCompression ? DecompressionMethods.None : DecompressionMethods.GZip | DecompressionMethods.Deflate | DecompressionMethods.None
            };

            this._ThreadHttpClient = new HttpClient(_httpHandler);
        }

        public async Task DownloadFileAsync(string Input, string Output, CancellationToken Token, long StartOffset = -1, long EndOffset = -1) =>
            await DownloadFileAsync(Input, new FileStream(Output, FileMode.OpenOrCreate, FileAccess.Write, FileShare.Write), Token, StartOffset, EndOffset);

        public async Task DownloadFileAsync(string Input, Stream Output, CancellationToken Token, long StartOffset = -1, long EndOffset = -1)
        {
            this._InputURL = Input;
            this._OutputStream = Output;
            this._UseStreamOutput = true;
            this._ThreadToken = Token;
            this._ThreadSingleMode = true;
            this._Stopwatch = Stopwatch.StartNew();
            this._LastContinuedSize = 0;
            this._DownloadState = State.Downloading;
            this._IsFileAlreadyCompleted = false;

            try
            {
                _ThreadPropertyList = new List<_ThreadProperty> { new _ThreadProperty { CurrentRetry = 1 } };
                await ThreadChild(StartOffset, EndOffset, 0, true);
                this._DownloadState = State.Completed;
                UpdateProgress(new _DownloadProgress(_DownloadedSize, _TotalSizeToDownload, 0, _LastContinuedSize, _Stopwatch.Elapsed, _DownloadState));

                _Stopwatch.Stop();
            }
            catch (TaskCanceledException ex)
            {
                _Stopwatch.Stop();
                this._DownloadState = State.Cancelled;
                UpdateProgress(new _DownloadProgress(_DownloadedSize, _TotalSizeToDownload, 0, _LastContinuedSize, _Stopwatch.Elapsed, _DownloadState));
                throw new TaskCanceledException($"Cancellation for {Input} has been fired!", ex);
            }
        }

        public async Task DownloadFileAsync(string Input, string Output, int DownloadThread, CancellationToken Token)
        {
            this._InputURL = Input;
            this._OutputPath = Output;
            this._ThreadNumber = DownloadThread;
            this._ThreadList = new List<Task>();
            this._ThreadToken = Token;
            this._ThreadSingleMode = false;
            this._Stopwatch = Stopwatch.StartNew();
            this._LastContinuedSize = 0;
            this._DownloadState = State.Downloading;
            this._IsFileAlreadyCompleted = false;
            this._UseStreamOutput = false;

            try
            {
                _ThreadList = GetThreadSlice();

                await Task.WhenAll(_ThreadList);

                // HACK: Round the size after multidownload finished
                _DownloadedSize += _TotalSizeToDownload - _DownloadedSize;
                UpdateProgress(new _DownloadProgress(_DownloadedSize, _TotalSizeToDownload, (int)_SelfReadFileSize, _LastContinuedSize, _Stopwatch.Elapsed, _DownloadState));

                if (!_IsFileAlreadyCompleted)
                {
                    MergeSlices();
                }
                this._DownloadState = State.Completed;
                UpdateProgress(new _DownloadProgress(_DownloadedSize, _TotalSizeToDownload, 0, _LastContinuedSize, _Stopwatch.Elapsed, _DownloadState));

                _Stopwatch.Stop();
            }
            catch (TaskCanceledException ex)
            {
                _Stopwatch.Stop();
                this._DownloadState = State.Cancelled;
                UpdateProgress(new _DownloadProgress(_DownloadedSize, _TotalSizeToDownload, 0, _LastContinuedSize, _Stopwatch.Elapsed, _DownloadState));
                throw new TaskCanceledException($"Cancellation for {Input} has been fired!", ex);
            }
        }

        public void DownloadFile(string Input, Stream Output, CancellationToken Token, long StartOffset = -1, long EndOffset = -1) =>
            DownloadFileAsync(Input, Output, Token, StartOffset, EndOffset).GetAwaiter().GetResult();
        public void DownloadFile(string Input, string Output, CancellationToken Token, long StartOffset = -1, long EndOffset = -1) =>
            DownloadFileAsync(Input, Output, Token, StartOffset, EndOffset).GetAwaiter().GetResult();
        public void DownloadFile(string Input, string Output, int DownloadThread, CancellationToken Token) =>
            DownloadFileAsync(Input, Output, DownloadThread, Token).GetAwaiter().GetResult();
    }
}
