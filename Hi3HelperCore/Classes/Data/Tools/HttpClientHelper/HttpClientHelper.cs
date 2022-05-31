using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.IO;
using System.Net;
using System.Linq;
using System.Diagnostics;
using System.Net.Http;

namespace Hi3Helper.Data
{
    public partial class HttpClientHelper : HttpClient
    {
        private Stopwatch _Stopwatch;
        private string _InputURL = "", _OutputPath = "";
        private State _DownloadState;
        private bool _IsFileAlreadyCompleted = false;

        public HttpClientHelper(bool IgnoreCompression = false, bool ignoreSslCertificate = false, int maxRetryCount = 5, float maxRetryTimeout = 1)
            : base(new HttpClientHandler
            {
                AllowAutoRedirect = true,
                UseCookies = true,
                MaxConnectionsPerServer = 32,
                AutomaticDecompression = IgnoreCompression ? DecompressionMethods.None : DecompressionMethods.GZip | DecompressionMethods.Deflate | DecompressionMethods.None,
                ServerCertificateCustomValidationCallback = ignoreSslCertificate ? (message, cert, chain, errors) => { return true; } : null
            })
        {
            this._Stopwatch = new Stopwatch();
            this._OutputStream = new MemoryStream();
            this._ThreadMaxRetry = maxRetryCount;
            this._ThreadRetryDelay = maxRetryTimeout;
            this._DownloadState = State.Idle;
        }

        public async Task DownloadFileAsync(string Input, string Output, CancellationToken Token, long? StartOffset = null, long? EndOffset = null)
        {
            this._UseStreamOutput = false;
            await InternalDownloadFileAsync(Input, new FileStream(Output, FileMode.OpenOrCreate, FileAccess.Write, FileShare.Write), Token, StartOffset, EndOffset, true);
        }

        public async Task DownloadFileAsync(string Input, Stream Output, CancellationToken Token, long? StartOffset = null, long? EndOffset = null, bool DisposeStream = true)
        {
            this._UseStreamOutput = true;
            await InternalDownloadFileAsync(Input, Output, Token, StartOffset, EndOffset, DisposeStream);
        }

        private async Task InternalDownloadFileAsync(string Input, Stream Output, CancellationToken Token, long? StartOffset, long? EndOffset, bool DisposeStream)
        {
            this._InputURL = Input;
            this._OutputStream = Output;
            this._ThreadToken = Token;
            this._ThreadSingleMode = true;
            this._Stopwatch = Stopwatch.StartNew();
            this._DownloadedSize = 0;
            this._LastContinuedSize = 0;
            this._TotalSizeToDownload = 0;
            this._DownloadState = State.Starting;
            this._IsFileAlreadyCompleted = false;
            this._DisposeStream = DisposeStream;

            try
            {
                EnsureAllThreadsStatus();
                await Task.WhenAll(await StartThreads(StartOffset, EndOffset));
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
            this._ThreadToken = Token;
            this._ThreadSingleMode = false;
            this._Stopwatch = Stopwatch.StartNew();
            this._DownloadedSize = 0;
            this._LastContinuedSize = 0;
            this._TotalSizeToDownload = 0;
            this._DownloadState = State.Starting;
            this._IsFileAlreadyCompleted = false;
            this._UseStreamOutput = false;
            this._DisposeStream = true;

            try
            {
                EnsureAllThreadsStatus();
                await Task.WhenAll(await StartThreads(null, null));

                // HACK: Round the size after multidownload finished
                _DownloadedSize += _TotalSizeToDownload - _DownloadedSize;
                UpdateProgress(new _DownloadProgress(_DownloadedSize, _TotalSizeToDownload, (int)_SelfReadFileSize, _LastContinuedSize, _Stopwatch.Elapsed, _DownloadState));

                if (!_IsFileAlreadyCompleted)
                {
                    await Task.Run(() => MergeSlices());
                    DisposeAllThreadsStream();
                }
                this._DownloadState = State.Completed;
                UpdateProgress(new _DownloadProgress(_DownloadedSize, _TotalSizeToDownload, 0, _LastContinuedSize, _Stopwatch.Elapsed, _DownloadState));

                _Stopwatch.Stop();
            }
            catch (TaskCanceledException ex)
            {
                _Stopwatch.Stop();
                this._DownloadState = State.Cancelled;

                DisposeAllThreadsStream();

                UpdateProgress(new _DownloadProgress(_DownloadedSize, _TotalSizeToDownload, 0, _LastContinuedSize, _Stopwatch.Elapsed, _DownloadState));
                throw new TaskCanceledException($"Cancellation for {Input} has been fired!", ex);
            }
            catch (Exception ex)
            {
                DisposeAllThreadsStream();
                throw new Exception($"Unexpected error has occured!\r\n{ex}", ex);
            }
        }

        private async void EnsureAllThreadsStatus()
        {
            bool IsAllThreadRun = false;
            Console.WriteLine($"Ensure threads status... {_DownloadState}!");
            while (true)
            {
                IsAllThreadRun = _ThreadProperties == null ? false : _ThreadProperties.All(x => x.IsDownloading || x.IsCompleted);

                if (IsAllThreadRun && _ThreadProperties != null)
                {
                    _DownloadState = State.Downloading;
                    Console.WriteLine($"Threads are all loaded! {_DownloadState}...");
                    return;
                }

                await Task.Delay(250);
            }
        }

        public void DownloadFile(string Input, Stream Output, CancellationToken Token, long? StartOffset = null, long? EndOffset = null, bool DisposeStream = true) =>
            DownloadFileAsync(Input, Output, Token, StartOffset, EndOffset, DisposeStream).GetAwaiter().GetResult();
        public void DownloadFile(string Input, string Output, CancellationToken Token, long? StartOffset = null, long? EndOffset = null) =>
            DownloadFileAsync(Input, Output, Token, StartOffset, EndOffset).GetAwaiter().GetResult();
        public void DownloadFile(string Input, string Output, int DownloadThread, CancellationToken Token) =>
            DownloadFileAsync(Input, Output, DownloadThread, Token).GetAwaiter().GetResult();
    }
}
