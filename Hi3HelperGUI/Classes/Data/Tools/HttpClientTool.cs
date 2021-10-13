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
         * by default: 16 KiB (16384 bytes)
        */
        long bufflength = 16384;

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

        public bool DownloadFile(string input, string output, string customMessage = "", long startOffset = -1, long endOffset = -1, CancellationToken token = new CancellationToken())
        {
            if (string.IsNullOrEmpty(customMessage))
                customMessage = $"Downloading {Path.GetFileName(output)}";

            return GetRemoteStreamResponseNew(input, output, startOffset, endOffset, customMessage, token, false);
        }

        public bool DownloadStream(string input, MemoryStream output, CancellationToken token, long startOffset = -1, long endOffset = -1, string customMessage = "")
        {
            if (string.IsNullOrEmpty(customMessage))
                customMessage = $"Downloading to stream";

            localStream = output;

            return GetRemoteStreamResponseNew(input, @"tHiSiSduMmY.z", startOffset, endOffset, customMessage, token, true);
        }

        bool GetRemoteStreamResponseNew(string input, string output, long startOffset, long endOffset, string customMessage, CancellationToken token, bool isStream)
        {
            bool returnValue = true;
            OnCompleted(new DownloadProgressCompleted() { DownloadCompleted = false });

            try
            {
                UseStream(input, output, startOffset, endOffset, customMessage, token, isStream);
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
            catch (OperationCanceledException e)
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
                if (!isStream) localStream?.Dispose();
                else localStream.Position = 0;
                remoteStream?.Dispose();
            }

            OnCompleted(new DownloadProgressCompleted() { DownloadCompleted = true });

            return returnValue;
        }

        void UseStream(string input, string output, long startOffset, long endOffset, string customMessage, CancellationToken token, bool isStream)
        {
            token.ThrowIfCancellationRequested();
            long existingLength;
            FileInfo fileinfo = new FileInfo(output);

            if (isStream)
                existingLength = localStream.Length;
            else
                existingLength = fileinfo.Exists ? fileinfo.Length : 0;

            HttpRequestMessage request = new HttpRequestMessage { RequestUri = new Uri(input) };

            if (startOffset != -1 && endOffset != -1)
                request.Headers.Range = new RangeHeaderValue(startOffset, endOffset);
            else
                request.Headers.Range = new RangeHeaderValue(existingLength, null);

            using (HttpResponseMessage response = httpClient.Send(request, HttpCompletionOption.ResponseHeadersRead, token))
            {
                long contentLength = existingLength + (response.Content.Headers.ContentRange.Length - response.Content.Headers.ContentRange.From) ?? 0;
                resumabilityStatus = new DownloadStatusChanged((int)response.StatusCode == 206);

                if (!isStream)
                    localStream = fileinfo.Open(resumabilityStatus.ResumeSupported ? FileMode.Append : FileMode.Create, FileAccess.Write);

                OnResumabilityChanged(resumabilityStatus);

                ReadRemoteStream(
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

        void ReadRemoteStream(
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
            using (remoteStream = response.Content.ReadAsStream(token))
            {
                var sw = Stopwatch.StartNew();
                while ((byteSize = remoteStream.Read(buffer, 0, buffer.Length)) > 0)
                {
                    token.ThrowIfCancellationRequested();
                    localStream.Write(buffer, 0, byteSize);
                    totalReceived += byteSize;

                    OnProgressChanged(new DownloadProgressChanged(totalReceived, contentLength, sw.Elapsed.TotalSeconds) { Message = customMessage, CurrentReceived = byteSize });
                }

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
        public float ProgressPercentage { get => ((float)BytesReceived / (float)TotalBytesToReceive) * 100; }
        public long CurrentSpeed { get; private set; }
        public TimeSpan TimeLeft
        {
            get => TimeSpan.FromSeconds((TotalBytesToReceive - BytesReceived) / CurrentSpeed);
        }
    }
}
