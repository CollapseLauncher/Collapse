using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;

using static Hi3Helper.Logger;

namespace Hi3Helper.Data
{
    public partial class HttpClientHelper
    {
        private int _ThreadNumber;
        private HttpClient _ThreadHttpClient;
        private IEnumerable<Task> _ThreadList;
        private CancellationToken _ThreadToken;
        private int _ThreadRetryAttempt = 1;
        private float _ThreadRetryDelay = 1f;
        private int _ThreadMaxRetry = 5;
        private bool _ThreadSingleMode = false;
        private List<_ThreadProperty> _ThreadPropertyList;

        private class _ThreadProperty
        {
            public int ThreadID { get; set; }
            public long StartOffset { get; set; }
            public long EndOffset { get; set; }
            public int CurrentRetry { get; set; }
        }

        private IEnumerable<Task> GetThreadSlice()
        {
            List<Task> slice = new List<Task>();
            _ThreadPropertyList = RetryableSliceProperty();

            foreach (_ThreadProperty prop in _ThreadPropertyList)
            {
                slice.Add(Task.Run(async () =>
                {
                    await ThreadChild(prop.StartOffset, prop.EndOffset, prop.ThreadID);
                }));
            }

            return slice;
        }

        private List<_ThreadProperty> RetryableSliceProperty()
        {
            if (this._ThreadNumber < 2)
                throw new ThreadStateException($"Thread number must be > 2 threads");

            List<_ThreadProperty> sliceProperty = new List<_ThreadProperty>();

            bool IsGetContentLength = true;
            while (IsGetContentLength)
            {
                try
                {
                    long ExistingSize = 0;
                    if (!_ThreadSingleMode)
                    {
                        _DownloadedSize = GetSlicesSize(_OutputPath);
                        _TotalSizeToDownload = GetContentLength(_InputURL, _ThreadToken) ?? 0;
                    }
                    else
                    {
                        ExistingSize = GetFileSize(_OutputPath);
                    }

                    if (ExistingSize == _TotalSizeToDownload)
                    {
                        _IsFileAlreadyCompleted = true;
                        return sliceProperty;
                    }

                    IsGetContentLength = false;
                }
                catch (HttpRequestException ex)
                {
                    if (_ThreadRetryAttempt > _ThreadMaxRetry)
                        throw new HttpRequestException(ex.ToString(), ex);

                    LogWriteLine($"Error while fetching File Size (Retry Attempt: {_ThreadRetryAttempt})...", LogType.Warning, true);
                    Task.Delay((int)(_ThreadRetryDelay * 1000), _ThreadToken).GetAwaiter().GetResult();
                    _ThreadRetryAttempt++;
                }
            }

            long startContent, endContent;
            for (int i = 0; i < _ThreadNumber; i++)
            {
                startContent = i * (_TotalSizeToDownload / _ThreadNumber);
                endContent = i + 1 == _ThreadNumber ? _TotalSizeToDownload : ((i + 1) * (_TotalSizeToDownload / _ThreadNumber)) - 1;
                sliceProperty.Add(new _ThreadProperty { ThreadID = i, StartOffset = startContent, EndOffset = endContent, CurrentRetry = 1 });
            }

            return sliceProperty;
        }

        private async Task ThreadChild(long StartOffset, long EndOffset, int ThreadID, bool IgnoreInitSizeCheck = false)
        {
            string LocalPath;

            if (_ThreadSingleMode)
                LocalPath = _OutputPath;
            else
                LocalPath = string.Format("{0}.{1:000}", _OutputPath, ThreadID + 1);

            if (this._DisposeStream)
                using (Stream LocalStream = SeekStreamToEnd(_UseStreamOutput ? _OutputStream : GetFileStream(LocalPath, false)))
                    await ThreadChildStream(LocalStream, StartOffset, EndOffset, ThreadID);
            else
                await ThreadChildStream(SeekStreamToEnd(_UseStreamOutput ? _OutputStream : GetFileStream(LocalPath, false)), StartOffset, EndOffset, ThreadID);
        }

        private async Task ThreadChildStream(Stream LocalStream, long StartOffset, long EndOffset, int ThreadID)
        {
            bool IsRetryLastChance = false;
            while (!await TryRetryableContainer(LocalStream, StartOffset, EndOffset,
                ThreadID, IsRetryLastChance))
            {
                if (_ThreadPropertyList[ThreadID].CurrentRetry > _ThreadMaxRetry - 1)
                    IsRetryLastChance = true;

                LogWriteLine($"Retrying for threadID: {ThreadID} (Retry Attempt: {_ThreadPropertyList[ThreadID].CurrentRetry}/{_ThreadMaxRetry})...", LogType.Warning, true);
                await Task.Delay((int)(_ThreadRetryDelay * 1000), _ThreadToken);
                _ThreadPropertyList[ThreadID].CurrentRetry++;
            }
        }

        private async Task<bool> TryRetryableContainer(Stream LocalStream, long? StartOffset, long? EndOffset,
            int ThreadID, bool IsRetryLastChance)
        {
            HttpRequestMessage RequestMessage;
            RangeHeaderValue RequestRange;
            HttpResponseMessage ResponseMessage;

            long ExistingSize = LocalStream.Length,
                 DownloadSize = 0;

            RequestMessage = new HttpRequestMessage() { RequestUri = new Uri(_InputURL) };

#if DEBUG
            if (!_ThreadSingleMode)
                Console.WriteLine($"ThreadID: {ThreadID}, Start: {StartOffset}, EndOffset: {EndOffset}, Size: {EndOffset - StartOffset}");
#endif

            try
            {
                if (!(StartOffset < 0 && EndOffset < 0))
                    RequestRange = new RangeHeaderValue(
                        StartOffset < 0 || StartOffset == null ? 0 : (StartOffset >= 0 ? StartOffset : StartOffset + ExistingSize),
                        EndOffset < 0   || EndOffset   == null ? null : EndOffset);
                else
                    RequestRange = new RangeHeaderValue(ExistingSize, null);

                RequestMessage.Headers.Range = RequestRange;

                ResponseMessage = CheckResponseStatusCode(await _ThreadHttpClient.SendAsync(RequestMessage, HttpCompletionOption.ResponseHeadersRead, _ThreadToken));

                DownloadSize = (ResponseMessage.Content.Headers.ContentLength ?? 0) + ExistingSize;

                if (_ThreadSingleMode)
                {
                    _DownloadedSize = ExistingSize;
                    _TotalSizeToDownload = DownloadSize;
                }

                using (ResponseMessage)
                {
                    using (Stream RemoteStream = await GetRemoteStream(ResponseMessage))
                    {
                        // Reset CurrentRetry if the connection come back;
                        _ThreadPropertyList[ThreadID].CurrentRetry = 1;
                        await ReadStreamAsync(RemoteStream, LocalStream);
                    }
                }
            }
            catch (IOException ex)
            {
                LogWriteLine($"I/O Error on ThreadID: {ThreadID}\r\n{ex}", LogType.Warning, true);
                if (IsRetryLastChance)
                    throw new IOException($"ThreadID: {ThreadID} has exceeded Max. Retry: {_ThreadPropertyList[ThreadID].CurrentRetry - 1}/{_ThreadMaxRetry}. CANCELLING!!", ex);

                return false;
            }
            catch (HttpRequestException ex)
            {
                LogWriteLine($"Error on ThreadID: {ThreadID}\r\n{ex}", LogType.Warning, true);
                if (IsRetryLastChance)
                    throw new HttpRequestException($"ThreadID: {ThreadID} has exceeded Max. Retry: {_ThreadPropertyList[ThreadID].CurrentRetry - 1}/{_ThreadMaxRetry}. CANCELLING!!", ex);

                return false;
            }
            catch (ArgumentOutOfRangeException ex)
            {
                LocalStream.Dispose();
                LogWriteLine($"Cancel: ThreadID: {ThreadID} has been shutdown!\r\nChunk of this thread is already completed. Ignoring!!\r\n{ex}", LogType.Default, false);
                return true;
            }
            catch (InvalidDataException ex)
            {
                LocalStream.Dispose();
                LogWriteLine($"Cancel: ThreadID: {ThreadID} has been shutdown!\r\n{ex}", LogType.Warning, true);
                return true;
            }
            catch (TaskCanceledException ex)
            {
                LocalStream.Dispose();
                LogWriteLine($"Cancel: ThreadID: {ThreadID} has been shutdown!", LogType.Error, false);
                throw new TaskCanceledException(ex.ToString(), ex);
            }
            catch (OperationCanceledException ex)
            {
                LocalStream.Dispose();
                LogWriteLine($"Cancel: ThreadID: {ThreadID} has been shutdown!", LogType.Default, false);
                throw new TaskCanceledException(ex.ToString(), ex);
            }
            catch (Exception ex)
            {
                LocalStream.Dispose();
                LogWriteLine($"Unknown Error on ThreadID: {ThreadID}\r\n{ex}", LogType.Error, true);
                throw new Exception(ex.ToString(), ex);
            }

            return true;
        }

        public long? GetContentLength(string input, CancellationToken token = new CancellationToken())
        {
            HttpResponseMessage response = _ThreadHttpClient.SendAsync(new HttpRequestMessage() { RequestUri = new Uri(input) }, HttpCompletionOption.ResponseHeadersRead, token)
                .GetAwaiter().GetResult();

            return response.Content.Headers.ContentLength;
        }
    }
}
