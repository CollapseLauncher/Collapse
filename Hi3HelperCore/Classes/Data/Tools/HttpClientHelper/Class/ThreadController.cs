using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;

namespace Hi3Helper.Data
{
    public partial class HttpClientHelper
    {
        private int _ThreadNumber;
        private CancellationToken _ThreadToken;
        private int _ThreadRetryAttempt = 1;
        private float _ThreadRetryDelay = 1f;
        private int _ThreadMaxRetry = 5;
        private bool _ThreadSingleMode = false;
        private IEnumerable<_ThreadProperty> _ThreadProperties;

        private class _ThreadProperty : HttpClient
        {
            public string URL { get; set; }
            public CancellationToken ThreadToken { get; set; }
            public int ThreadID { get; set; }
            public long? StartOffset { get; set; }
            public long? EndOffset { get; set; }
            public int CurrentRetry { get; set; }
            public HttpResponseMessage HttpMessage { get; set; }
            public Stream LocalStream { get; set; }
            public Stream RemoteStream { get; set; }
            public bool IsDownloading { get; set; } = false;
            public bool IsCompleted { get; set; } = false;

            public new void Dispose()
            {
                LocalStream?.Dispose();
                RemoteStream?.Dispose();
                HttpMessage?.Dispose();
            }

            private HttpResponseMessage CheckResponseStatusCode(HttpResponseMessage Input)
            {
                if (Input.StatusCode == HttpStatusCode.RequestedRangeNotSatisfiable)
                    throw new InvalidDataException($"Return Code: {(int)Input.StatusCode} ({Input.StatusCode}) from {Input.RequestMessage?.RequestUri}. File may already completed or Server cannot respond ContentLength. Ignoring!");
                if (!Input.IsSuccessStatusCode)
                    throw new HttpRequestException($"Error Occured while Reading Response from {Input.RequestMessage?.RequestUri} with Return Code: {(int)Input.StatusCode} ({Input.StatusCode})");

                return Input;
            }

            public async Task RetryRequest()
            {
                StartOffset = LocalStream.Length;
                HttpRequestMessage _RequestMessage = new HttpRequestMessage() { RequestUri = new Uri(URL) };
                _RequestMessage.Headers.Range = new RangeHeaderValue(StartOffset, EndOffset);
                this.HttpMessage = CheckResponseStatusCode(await SendAsync(_RequestMessage, HttpCompletionOption.ResponseHeadersRead, ThreadToken));
            }
        }

        private async Task<IEnumerable<Task>> StartThreads(long? StartOffset, long? EndOffset)
        {
            ICollection<Task> _out = new List<Task>();
            if ((StartOffset ?? 0) < 0 || (EndOffset ?? 0) < 0)
                throw new ArgumentOutOfRangeException($"StartOffset or EndOffset cannot be < 0!");

            _ThreadProperties = await GetThreadProperties(StartOffset, EndOffset);

            foreach (_ThreadProperty ThreadProperty in _ThreadProperties)
            {
                _out.Add(Task.Run(async () =>
                {
                    ThreadProperty.RemoteStream = await GetRemoteStream(ThreadProperty.HttpMessage);
                    bool IsRetryLastChance = false;
                    while (!await TryRunRetryableTask(ThreadProperty, IsRetryLastChance))
                    {
                        if (ThreadProperty.CurrentRetry > _ThreadMaxRetry - 1)
                            IsRetryLastChance = true;

                        Console.WriteLine($"Retrying for threadID: {ThreadProperty.ThreadID} (Retry Attempt: {ThreadProperty.CurrentRetry}/{_ThreadMaxRetry})...");
                        await Task.Delay((int)(_ThreadRetryDelay * 1000), _ThreadToken);
                        ThreadProperty.CurrentRetry++;
                    }

                    ThreadProperty.IsDownloading = false;
                    ThreadProperty.IsCompleted = true;
                }));
            }

            return _out;
        }

        private async Task<IEnumerable<_ThreadProperty>> GetThreadProperties(long? StartOffset, long? EndOffset)
        {
            if (!_ThreadSingleMode)
                return await GetMultipleThreadProperties();

            return await GetSingleThreadProperties(StartOffset, EndOffset);
        }

        private async Task<IList<_ThreadProperty>> GetSingleThreadProperties(long? StartOffset, long? EndOffset)
        {
            bool IsIgnore = false;
            long LocalLength = this._UseStreamOutput ? 0 : _OutputStream.Length;

            HttpRequestMessage RequestMessage = new HttpRequestMessage() { RequestUri = new Uri(_InputURL) };
            _ThreadProperty ThreadProperty = new _ThreadProperty()
            {
                URL = _InputURL,
                ThreadID = 0,
                ThreadToken = _ThreadToken,
                CurrentRetry = 1,
                LocalStream = this._UseStreamOutput ? _OutputStream : SeekStreamToEnd(_OutputStream),
                StartOffset = this._UseStreamOutput ? StartOffset ?? 0 : (StartOffset == null ? LocalLength : LocalLength + StartOffset),
                EndOffset = EndOffset
            };

            _DownloadedSize += LocalLength;

            if (ThreadProperty.EndOffset <= ThreadProperty.StartOffset)
            {
                ThreadProperty.StartOffset = 0;
                ThreadProperty.EndOffset = EndOffset;
                IsIgnore = true;
            }

            RequestMessage.Headers.Range = new RangeHeaderValue(ThreadProperty.StartOffset, ThreadProperty.EndOffset);

            ThreadProperty.HttpMessage = CheckResponseStatusCode(await SendAsync(RequestMessage, HttpCompletionOption.ResponseHeadersRead, _ThreadToken));

            _TotalSizeToDownload += IsIgnore ? ThreadProperty.HttpMessage.Content.Headers.ContentLength ?? 0 :
                (ThreadProperty.HttpMessage.Content.Headers.ContentLength ?? 0) + LocalLength;

            if (IsIgnore)
                return new List<_ThreadProperty>();

            return new List<_ThreadProperty> { ThreadProperty };
        }

        private async Task<IList<_ThreadProperty>> GetMultipleThreadProperties()
        {
            IList<_ThreadProperty> _out = new List<_ThreadProperty>();
            _TotalSizeToDownload = await TryGetContentLength();

            long _SliceSize = (long)Math.Ceiling((double)_TotalSizeToDownload / _ThreadNumber);

            for (long i = 0, thread = 0; thread < _ThreadNumber; i += _SliceSize, thread++)
            {
                _ThreadProperty ThreadProperty = new _ThreadProperty()
                {
                    URL = _InputURL,
                    ThreadID = (int)thread,
                    ThreadToken = _ThreadToken,
                    CurrentRetry = 1,
                    LocalStream = SeekStreamToEnd(new FileStream(string.Format("{0}.{1:000}", _OutputPath, thread + 1), FileMode.OpenOrCreate, FileAccess.Write))
                };
                _DownloadedSize += ThreadProperty.LocalStream.Length;
                ThreadProperty.StartOffset = i + ThreadProperty.LocalStream.Length;
                ThreadProperty.EndOffset = thread + 1 == _ThreadNumber ? _TotalSizeToDownload - 1 : (i + _SliceSize) - 1;

                await CheckAndSetRangeValidation(ThreadProperty, _out, (int)thread);
            }

            return _out;
        }

        private async Task CheckAndSetRangeValidation(_ThreadProperty ThreadProperty, IList<_ThreadProperty> ListOut, int ThreadID)
        {
            try
            {
                if ((ThreadProperty.EndOffset - ThreadProperty.StartOffset == -1)
                    || (ThreadID + 1 == _ThreadNumber ? ThreadProperty.EndOffset == ThreadProperty.StartOffset : false))
                {
                    ThreadProperty.LocalStream.Dispose();
                    return;
                }

                if (ThreadProperty.EndOffset - ThreadProperty.StartOffset < -1)
                {
                    Console.WriteLine($"Chunk on ThreadID: {ThreadID} seems to be corrupted (< expected size). Redownloading it!");
                    _DownloadedSize -= ThreadProperty.LocalStream.Length;
                    ThreadProperty.StartOffset -= ThreadProperty.LocalStream.Length;
                    ThreadProperty.LocalStream.Dispose();
                    ThreadProperty.LocalStream = new FileStream(string.Format("{0}.{1:000}", _OutputPath, ThreadID + 1), FileMode.Create, FileAccess.Write);
                }

                if (ThreadProperty.EndOffset - ThreadProperty.StartOffset > -1)
                {
                    HttpRequestMessage _RequestMessage = new HttpRequestMessage() { RequestUri = new Uri(_InputURL) };
                    _RequestMessage.Headers.Range = new RangeHeaderValue(ThreadProperty.StartOffset, ThreadProperty.EndOffset);
                    ThreadProperty.HttpMessage = CheckResponseStatusCode(await SendAsync(_RequestMessage, HttpCompletionOption.ResponseHeadersRead, _ThreadToken));
                }

                ListOut.Add(ThreadProperty);
            }
            catch (Exception ex)
            {
                ThreadProperty.Dispose();
                throw new Exception($"{ex}", ex);
            }
        }

        private async Task<long> TryGetContentLength()
        {
            while (true)
            {
                try
                {
                    return await GetContentLength(_InputURL, _ThreadToken) ?? 0;
                }
                catch (HttpRequestException ex)
                {
                    if (_ThreadRetryAttempt > _ThreadMaxRetry)
                        throw new HttpRequestException(ex.ToString(), ex);

                    Console.WriteLine($"Error while fetching File Size (Retry Attempt: {_ThreadRetryAttempt})...");
                    Task.Delay((int)(_ThreadRetryDelay * 1000), _ThreadToken).GetAwaiter().GetResult();
                    _ThreadRetryAttempt++;
                }
            }
        }

        private async Task<bool> TryRunRetryableTask(_ThreadProperty ThreadProperty, bool IsLastRetry)
        {
            try
            {
                Console.WriteLine($"ThreadID: {ThreadProperty.ThreadID}, Start: {ThreadProperty.StartOffset}, EndOffset: {ThreadProperty.EndOffset}, Size: {ThreadProperty.EndOffset - ThreadProperty.StartOffset}");
                await ReadStreamAsync(ThreadProperty);

                if (_DisposeStream)
                    ThreadProperty.Dispose();
            }
            catch (IOException ex)
            {
                Console.WriteLine($"I/O Error on ThreadID: {ThreadProperty.ThreadID}\r\n{ex}");
                await ThreadProperty.RetryRequest();
                if (IsLastRetry)
                {
                    ThreadProperty.IsDownloading = false;
                    throw new IOException($"ThreadID: {ThreadProperty.ThreadID} has exceeded Max. Retry: {ThreadProperty.CurrentRetry - 1}/{_ThreadMaxRetry}. CANCELLING!!", ex);
                }

                return false;
            }
            catch (HttpRequestException ex)
            {
                Console.WriteLine($"Error on ThreadID: {ThreadProperty.ThreadID}\r\n{ex}");
                await ThreadProperty.RetryRequest();
                if (IsLastRetry)
                {
                    ThreadProperty.IsDownloading = false;
                    throw new HttpRequestException($"ThreadID: {ThreadProperty.ThreadID} has exceeded Max. Retry: {ThreadProperty.CurrentRetry - 1}/{_ThreadMaxRetry}. CANCELLING!!", ex);
                }

                return false;
            }
            catch (ArgumentOutOfRangeException ex)
            {
                ThreadProperty.IsDownloading = false;
                Console.WriteLine($"Cancel: ThreadID: {ThreadProperty.ThreadID} has been shutdown!\r\nChunk of this thread is already completed. Ignoring!!\r\n{ex}");
                return true;
            }
            catch (InvalidDataException ex)
            {
                ThreadProperty.IsDownloading = false;
                Console.WriteLine($"Cancel: ThreadID: {ThreadProperty.ThreadID} has been shutdown!\r\nThread doesn't receive valid data!\r\n{ex}");
                throw new InvalidDataException($"Cancel: ThreadID: {ThreadProperty.ThreadID} has been shutdown!\r\nThread doesn't receive valid data!", ex);
            }
            catch (TaskCanceledException ex)
            {
                ThreadProperty.IsDownloading = false;
                Console.WriteLine($"Cancel: ThreadID: {ThreadProperty.ThreadID} has been shutdown!");
                throw new TaskCanceledException(ex.ToString(), ex);
            }
            catch (OperationCanceledException ex)
            {
                ThreadProperty.IsDownloading = false;
                Console.WriteLine($"Cancel: ThreadID: {ThreadProperty.ThreadID} has been shutdown!");
                throw new TaskCanceledException(ex.ToString(), ex);
            }
            catch (Exception ex)
            {
                ThreadProperty.IsDownloading = false;
                Console.WriteLine($"Unknown Error on ThreadID: {ThreadProperty.ThreadID}\r\n{ex}");
                throw new Exception(ex.ToString(), ex);
            }

            return true;
        }

        public async Task<long?> GetContentLength(string input, CancellationToken token = new CancellationToken())
        {
            HttpResponseMessage response = await SendAsync(new HttpRequestMessage() { RequestUri = new Uri(input) }, HttpCompletionOption.ResponseHeadersRead, token);

            return response.Content.Headers.ContentLength;
        }
    }
}
