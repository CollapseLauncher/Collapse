using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Threading;
using System.Threading;
using System.Threading.Tasks;
using System.Net;
using System.IO;
using System.Diagnostics;
#if (!NETFRAMEWORK)
using System.Net.Http;
using System.Net.Http.Headers;
#endif

using static Hi3HelperGUI.Logger;
using static Hi3HelperGUI.Data.ConverterTool;

namespace Hi3HelperGUI.Data
{
    public class HttpClientTool
    {

#if (NETCOREAPP)
        HttpClient httpClient;
#endif

        protected Stream localStream;
        protected Stream remoteStream;
        public event EventHandler<DownloadStatusChanged> ResumablityChanged;
        public event EventHandler<DownloadProgressChanged> ProgressChanged;
        public event EventHandler<DownloadProgressCompleted> Completed;

        public bool stop = true; // by default stop is true
        /* Declare download buffer
         * by default: 256 KiB (262144 bytes)
        */
        long bufflength = 262144;

        public HttpClientTool()
        {
#if (NETCOREAPP)
            httpClient = new HttpClient(
            new HttpClientHandler
            {
                AutomaticDecompression = DecompressionMethods.All,
                UseCookies = true,
                MaxConnectionsPerServer = 32,
                AllowAutoRedirect = true
            });
#endif
        }

        DownloadStatusChanged resumabilityStatus;

        public async Task<bool> DownloadFile(string input, string output, CancellationToken token, long startOffset = -1, long endOffset = -1, string customMessage = "")
        {
            if (string.IsNullOrEmpty(customMessage))
                customMessage = $"Downloading {Path.GetFileName(output)}";

            return await GetRemoteStreamResponse(input, output, token, startOffset, endOffset, customMessage, false);
        }

        public async Task<Stream> DownloadFileToStream(string input, CancellationToken token, long startOffset = -1, long endOffset = -1, string customMessage = "")
        {
            if (string.IsNullOrEmpty(customMessage))
                customMessage = $"Downloading to buffer";

            localStream = new MemoryStream();

            await GetRemoteStreamResponse(input, "", token, startOffset, endOffset, customMessage, true);

            return localStream;
        }

        public bool DownloadFile(string input, string output, long startOffset = -1, long endOffset = -1, string customMessage = "")
        {
            stop = false;
            if (string.IsNullOrEmpty(customMessage))
                customMessage = $"Downloading {Path.GetFileName(output)}";

            FileStream file = new FileStream(output, File.Exists(output) ? FileMode.Append : FileMode.Create, FileAccess.Write);

            return GetRemoteStreamResponse(input, output, startOffset, endOffset, customMessage, false, file);
        }

        public bool DownloadFileToStream(string input, in Stream output, long startOffset = -1, long endOffset = -1, string customMessage = "")
        {
            stop = false;
            if (string.IsNullOrEmpty(customMessage))
                customMessage = $"Downloading to buffer";

            return GetRemoteStreamResponse(input, "", startOffset, endOffset, customMessage, true, output);
        }

        async Task<bool> GetRemoteStreamResponse(string input, string output, CancellationToken token, long startOffset, long endOffset, string customMessage, bool isStreamOutput)
        {
            long existingLength = 0;
            bool returnValue = true;
            OnCompleted(new DownloadProgressCompleted() { DownloadCompleted = false });

            try
            {
                FileInfo fileinfo = new FileInfo(isStreamOutput ? "" : output);
                if (fileinfo.Exists) existingLength = fileinfo.Length;

                HttpRequestMessage request = new HttpRequestMessage { RequestUri = new Uri(input) };

                if (startOffset != -1 && endOffset != -1)
                    request.Headers.Range = new RangeHeaderValue(startOffset, endOffset);
                else
                    request.Headers.Range = new RangeHeaderValue(existingLength, null);

                using (HttpResponseMessage response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, token).ConfigureAwait(false))
                {
                    long contentLength = existingLength + (response.Content.Headers.ContentRange.Length - response.Content.Headers.ContentRange.From) ?? 0;
                    resumabilityStatus = new DownloadStatusChanged((int)response.StatusCode == 206);

                    if (!isStreamOutput)
                    {
                        localStream = fileinfo.Open(resumabilityStatus.ResumeSupported ? FileMode.Append : FileMode.Create, FileAccess.Write);
                    }

                    OnResumabilityChanged(resumabilityStatus);

                    await ReadRemoteStream(
                        response,
                        localStream,
                        existingLength,
                        contentLength,
                        customMessage,
                        token
                        );
                    response.Dispose();
                }
            }
            catch (WebException e)
            {
                returnValue = ThrowWebExceptionAsBool(e);
            }
            catch (TaskCanceledException e)
            {
                returnValue = true;
                throw new TaskCanceledException(e.ToString());
            }
            catch (NullReferenceException e)
            {
                LogWriteLine($"This file {input} has 0 byte in size.\r\nTraceback: {e}", LogType.Error, true);
                returnValue = false;
            }
            catch (Exception e)
            {
                LogWriteLine($"An error occured while downloading {(isStreamOutput ? "to buffer" : Path.GetFileName(output))}\r\nTraceback: {e}", LogType.Error, true);
                returnValue = false;
            }
            finally
            {
                if (!isStreamOutput) localStream?.Dispose();
                remoteStream?.Dispose();
            }

            OnCompleted(new DownloadProgressCompleted() { DownloadCompleted = true });

            return returnValue;
        }

        bool GetRemoteStreamResponse(string input, string output, long startOffset, long endOffset, string customMessage, bool isStreamOutput, in Stream outputStream)
        {
            long existingLength = isStreamOutput ? outputStream.Length : new FileInfo(output).Length;
            bool returnValue = true;
            OnCompleted(new DownloadProgressCompleted() { DownloadCompleted = false });

            try
            {
                HttpRequestMessage request = new HttpRequestMessage { RequestUri = new Uri(input) };

                if (startOffset != -1 && endOffset != -1)
                    request.Headers.Range = new RangeHeaderValue(startOffset, endOffset);
                else
                    request.Headers.Range = new RangeHeaderValue(existingLength, null);

                using (HttpResponseMessage response = httpClient.Send(request, HttpCompletionOption.ResponseHeadersRead))
                {
                    long contentLength = existingLength + (response.Content.Headers.ContentRange.Length - response.Content.Headers.ContentRange.From) ?? 0;
                    resumabilityStatus = new DownloadStatusChanged((int)response.StatusCode == 206);

                    OnResumabilityChanged(resumabilityStatus);

                    ReadRemoteStream(
                        response,
                        outputStream,
                        existingLength,
                        contentLength,
                        customMessage
                        );
                    response.Dispose();
                }
            }
            catch (WebException e)
            {
                returnValue = ThrowWebExceptionAsBool(e);
            }
            catch (TaskCanceledException e)
            {
                returnValue = true;
                throw new TaskCanceledException(e.ToString());
            }
            catch (NullReferenceException e)
            {
                LogWriteLine($"This file {input} has 0 byte in size.\r\nTraceback: {e}", LogType.Error, true);
                returnValue = false;
            }
            catch (Exception e)
            {
                LogWriteLine($"An error occured while downloading {Path.GetFileName(output)}\r\nTraceback: {e}", LogType.Error, true);
                returnValue = false;
            }
            finally
            {
                if (!isStreamOutput) outputStream?.Dispose();
                remoteStream?.Dispose();
            }

            OnCompleted(new DownloadProgressCompleted() { DownloadCompleted = true });

            stop = true;

            return returnValue;
        }

        async Task ReadRemoteStream(
           HttpResponseMessage response,
           Stream localStream,
           long existingLength,
           long contentLength,
           string customMessage,
           CancellationToken token)
        {
            int byteSize = 0;
            long totalReceived = byteSize + existingLength;
            byte[] buffer = new byte[bufflength];
            using (remoteStream = await response.Content.ReadAsStreamAsync(token).ConfigureAwait(false))
            {
                var sw = Stopwatch.StartNew();
                do
                {
                    await localStream.WriteAsync(buffer, 0, byteSize, token).ConfigureAwait(false);
                    totalReceived += byteSize;

                    OnProgressChanged(new DownloadProgressChanged(totalReceived, contentLength, sw.Elapsed.TotalSeconds) { Message = customMessage, CurrentReceived = byteSize });
                }
                while ((byteSize = await remoteStream.ReadAsync(buffer, 0, buffer.Length, token).ConfigureAwait(false)) > 0);

                sw.Stop();
            }
        }

        void ReadRemoteStream(
           HttpResponseMessage response,
           in Stream stream,
           long existingLength,
           long contentLength,
           string customMessage)
        {
            int byteSize = 0;
            long totalReceived = byteSize + existingLength;
            byte[] buffer = new byte[bufflength];
            using (remoteStream = response.Content.ReadAsStream())
            {
                var sw = Stopwatch.StartNew();
                do
                {
                    if (stop) return;
                    stream.Write(buffer, 0, byteSize);
                    totalReceived += byteSize;

                    OnProgressChanged(new DownloadProgressChanged(totalReceived, contentLength, sw.Elapsed.TotalSeconds) { Message = customMessage, CurrentReceived = byteSize });
                }
                while ((byteSize = remoteStream.Read(buffer, 0, buffer.Length)) > 0);

                sw.Stop();
            }
        }

        bool ThrowWebExceptionAsBool(WebException e)
        {
            switch (GetStatusCodeResponse(e))
            {
                // Always ignore 416 code
                case 416:
                    return true;
                case -1:
                default:
                    LogWriteLine(e.Message, LogType.Error, true);
                    return false;
            }
        }

        int GetStatusCodeResponse(WebException e) => e.Response == null ? -1 : (int)((HttpWebResponse)e.Response).StatusCode;

        public void StopDownload()
        {
            stop = true;
        }

        protected virtual void OnResumabilityChanged(DownloadStatusChanged e)
        {
            var handler = ResumablityChanged;
            if (handler != null)
            {
                handler(this, e);
            }
        }

        protected virtual void OnProgressChanged(DownloadProgressChanged e)
        {
            var handler = ProgressChanged;
            if (handler != null)
            {
                handler(this, e);
            }
        }

        protected virtual void OnCompleted(DownloadProgressCompleted e)
        {
            var handler = Completed;
            if (handler != null)
            {
                handler(this, e);
            }
        }

        public bool DownloadToStream(string input, in Stream output, string CustomMessage = "")
        {
            HttpClientTool client = this;

            client.ProgressChanged += DownloadProgressChanged;
            client.Completed += DownloadProgressCompleted;

            return client.DownloadFileToStream(input, output, -1, -1, CustomMessage);
        }

        void DownloadProgressCompleted(object sender, DownloadProgressCompleted e)
        {
#if DEBUG
            if (e.DownloadCompleted)
            {
                LogWrite($" Done!", LogType.Empty);
                LogWriteLine();
            }
#endif
        }

        void DownloadProgressChanged(object sender, DownloadProgressChanged e)
        {
#if DEBUG
            LogWrite($"{e.Message} \u001b[33;1m{(byte)e.ProgressPercentage}%"
             + $"\u001b[0m ({SummarizeSizeSimple(e.BytesReceived)}) (\u001b[32;1m{SummarizeSizeSimple(e.CurrentSpeed)}/s\u001b[0m)", LogType.NoTag, false, true);
#endif
        }
    }
    public class DownloadStatusChanged : EventArgs
    {
        public DownloadStatusChanged(bool canResume)
        {
            ResumeSupported = canResume;
        }
        public bool ResumeSupported { get; private set; }
    }

    public class DownloadProgressCompleted : EventArgs
    {
        public bool DownloadCompleted { get; set; }
    }

    public class DownloadProgressChanged : EventArgs
    {
        public DownloadProgressChanged(long totalReceived, long fileSize, double totalSecond)
        {
            BytesReceived = totalReceived;
            TotalBytesToReceive = fileSize;
            CurrentSpeed = (long)(totalReceived / totalSecond);
        }
        public string Message { get; set; }
        public long CurrentReceived { get; set; }
        public long BytesReceived { get; private set; }
        public long TotalBytesToReceive { get; private set; }
        public float ProgressPercentage { get { return ((float)BytesReceived / (float)TotalBytesToReceive) * 100; } }
        public long CurrentSpeed { get; private set; }
        public TimeSpan TimeLeft
        {
            get
            {
                var bytesRemainingtoBeReceived = TotalBytesToReceive - BytesReceived;
                return TimeSpan.FromSeconds(bytesRemainingtoBeReceived / CurrentSpeed);
            }
        }
    }
}
