using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
#if (!NETFRAMEWORK)
using System.Net.Http;
using System.Net.Http.Headers;
#endif
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using System.Threading;
using System.Threading.Tasks;
using Hi3HelperGUI;
using Hi3HelperGUI.Preset;
using static Hi3HelperGUI.Logger;
using static Hi3HelperGUI.Data.ConverterTool;

using static Hi3HelperGUI.MainWindow;

namespace Hi3HelperGUI.Data
{
    public class DownloadStatusChangedEventArgs : EventArgs
    {
        public bool ResumeSupported { get; set; }
    }

    public class DownloadProgressChangedEventArgs : EventArgs
    {
        public long BytesReceived { get; set; }
        public long TotalBytesToReceive { get; set; }
        public float ProgressPercentage { get; set; }
        public float CurrentSpeed { get; set; } // in bytes
        public long TimeLeft { get; set; } // in seconds
        public long CurrentReceivedBytes { get; set; }
        public bool NoProgress {
            /*get
            {
                return !(CurrentSpeed > 0 && TimeLeft > 0);
            }
            set {}
            */
            get; set;
        }
    }

    public class DownloadProgressCompletedEventArgs : EventArgs
    {
        public bool DownloadCompleted { get; set; }
    }

    public class DownloadUtils
    {
        public event EventHandler<DownloadStatusChangedEventArgs> DownloadStatusChanged;
        public event EventHandler<DownloadProgressChangedEventArgs> DownloadProgressChanged;
        public event EventHandler<DownloadProgressCompletedEventArgs> DownloadCompleted;
        public string userAgent = "Dalvik/2.1.0 (Linux; U; Android 11; M2012K11AG Build/RKQ1.200826.002)";

        // By default, stop is true
        public bool stop = true;

        /* Declare download buffer
         * by default: 256 KiB (262144 bytes)
        */
        long bufflength = 262144;
        HttpWebRequest request;

#if (NETCOREAPP)
        HttpClient httpClient;
#endif
        DownloadStatusChangedEventArgs downloadStatusArgs;

        Stream localStream;
        Stream remoteStream;

        public DownloadUtils()
        {
#if (NETCOREAPP)
            httpClient = new HttpClient(
            new HttpClientHandler
            {
                AutomaticDecompression = DecompressionMethods.All,
                UseCookies = true,
                MaxConnectionsPerServer = 256,
                AllowAutoRedirect = true,
            });
#endif
        }

        public async Task<bool> DownloadFileAsyncHTTP(string DownloadLink, string path, long startOffset = -1, long endOffset = -1, CancellationToken token = new CancellationToken())
        {
            bool ret = true;
            downloadStatusArgs = new DownloadStatusChangedEventArgs();
            long ExistingLength = 0;
            bool downloadResumable;

            if (File.Exists(path))
            {
                FileInfo fileInfo = new FileInfo(path);
                ExistingLength = fileInfo.Length;
            }

            try
            {
                var request = new HttpRequestMessage { RequestUri = new Uri(DownloadLink) };

                if (startOffset != -1 && endOffset != -1)
                    request.Headers.Range = new RangeHeaderValue(startOffset, endOffset);
                else
                    request.Headers.Range = new RangeHeaderValue(ExistingLength, null);

                using (HttpResponseMessage message = await httpClient.SendAsync(new HttpRequestMessage { RequestUri = new Uri(DownloadLink) }, HttpCompletionOption.ResponseHeadersRead, token))
                {
                    long ContentLength = ExistingLength + message.Content.Headers.ContentLength ?? 0;

                    if ((int)message.StatusCode == 206)
                    {
                        downloadResumable = true;
                        downloadStatusArgs.ResumeSupported = downloadResumable;
                        OnDownloadStatusChanged(downloadStatusArgs);
                        localStream = new FileStream(path, FileMode.Append, FileAccess.Write, FileShare.ReadWrite);
                    }
                    else
                    {
                        //LoggerLine("This download is not resumable.", LogType.Warning);
                        ExistingLength = 0;
                        downloadResumable = false;
                        downloadStatusArgs.ResumeSupported = downloadResumable;
                        OnDownloadStatusChanged(downloadStatusArgs);
                        localStream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.ReadWrite);
                    }

                    await ReadRemoteResponseAsyncHTTP(message.Content, localStream, ExistingLength, ContentLength, downloadResumable, token);
                    message.Dispose();
                }
                OnDownloadCompleted(new DownloadProgressCompletedEventArgs());
            }
            catch (WebException e)
            {
                ret = ThrowWebExceptionAsBool(e);
            }
            catch (TaskCanceledException e)
            {
                ret = true;
                throw new TaskCanceledException(e.ToString());
            }
            catch (Exception e)
            {
                LogWriteLine($"An error occured while downloading {Path.GetFileName(path)}\r\nTraceback: {e}", LogType.Error, true);
                ret = false;
            }
            finally
            {
                localStream?.Dispose();
                remoteStream?.Dispose();
            }

            return ret;
        }

        public async Task ReadRemoteResponseAsyncHTTP(HttpContent response, Stream localStream, long ExistingLength, long ContentLength, bool downloadResumable, CancellationToken token)
        {
            using (remoteStream = await response.ReadAsStreamAsync(token).ConfigureAwait(false))
            {
                int byteSize = 0;
                long totalReceived = byteSize + ExistingLength;
                var sw = new Stopwatch();
                sw.Start();
                float currentSpeed;
                long bytesRemainingtoBeReceived;
                byte[] downBuffer = new byte[bufflength];
                while ((byteSize = await remoteStream.ReadAsync(downBuffer, 0, downBuffer.Length, token).ConfigureAwait(false)) > 0)
                {
                    await localStream.WriteAsync(downBuffer, 0, byteSize, token).ConfigureAwait(false);
                    totalReceived += byteSize;
                    currentSpeed = totalReceived / (float)sw.Elapsed.TotalSeconds;
                    bytesRemainingtoBeReceived = ContentLength - totalReceived;

                    OnDownloadProgressChanged(new DownloadProgressChangedEventArgs()
                    {
                        CurrentReceivedBytes = byteSize,
                        BytesReceived = totalReceived,
                        TotalBytesToReceive = ContentLength,
                        CurrentSpeed = currentSpeed,
                        ProgressPercentage = downloadResumable ? (((float)totalReceived / (float)ContentLength) * 100) : 0,
                        TimeLeft = downloadResumable ? ((long)(bytesRemainingtoBeReceived / currentSpeed)) : 0
                    });
                }
                sw.Stop();
            }
        }

        public async Task<bool> DownloadFileAsync(string DownloadLink, string path, long startOffset = -1, long endOffset = -1, CancellationToken token = new CancellationToken())
        {
            bool ret = true;
            downloadStatusArgs = new DownloadStatusChangedEventArgs();

            long ExistingLength = File.Exists(path) ? new FileInfo(path).Length : 0;

            request = (HttpWebRequest)WebRequest.Create(DownloadLink);

            request.UserAgent = userAgent;

            if (startOffset != -1 && endOffset != -1)
                request.AddRange(startOffset, endOffset);
            else
                request.AddRange(ExistingLength);

            try
            {
                using (HttpWebResponse response = (HttpWebResponse)await request.GetResponseAsync())
                {
                    long ContentLength = ExistingLength + response.ContentLength;

                    if ((int)response.StatusCode == 206)
                    {
                        downloadStatusArgs.ResumeSupported = true;
                    }
                    else
                    {
                        ExistingLength = 0;
                        downloadStatusArgs.ResumeSupported = false;
                    }

                    localStream = new FileStream(path, downloadStatusArgs.ResumeSupported ? FileMode.Append : FileMode.Create, FileAccess.Write, FileShare.ReadWrite);
                    OnDownloadStatusChanged(downloadStatusArgs);

                    await ReadRemoteResponse(response, localStream, ExistingLength, ContentLength, downloadStatusArgs.ResumeSupported, token).ConfigureAwait(false);
                }
                OnDownloadCompleted(new DownloadProgressCompletedEventArgs());
            }
            catch (WebException e)
            {
                ret = ThrowWebExceptionAsBool(e);
            }
            catch (TaskCanceledException e)
            {
                ret = true;
                throw new TaskCanceledException(e.ToString());
            }
            catch (Exception e)
            {
                LogWriteLine($"An error occured while downloading {Path.GetFileName(path)}\r\nTraceback: {e}", LogType.Error, true);
                ret = false;
            }
            finally
            {
                localStream.Dispose();
                remoteStream.Dispose();
            }

            return ret;
        }

        public async Task ReadRemoteResponse(HttpWebResponse response, Stream localStream, long ExistingLength, long ContentLength, bool downloadResumable, CancellationToken token)
        {
            Stopwatch sw;
            using (remoteStream = response.GetResponseStream())
            {
                int byteSize = 0;
                long totalReceived = byteSize + ExistingLength;
                sw = new Stopwatch();
                sw.Start();
                byte[] downBuffer = new byte[bufflength];
                float currentSpeed;
                while ((byteSize = await remoteStream.ReadAsync(downBuffer, 0, downBuffer.Length, token).ConfigureAwait(false)) > 0)
                {
                    await localStream.WriteAsync(downBuffer, 0, byteSize, token).ConfigureAwait(false);
                    totalReceived += byteSize;
                    currentSpeed = totalReceived / (float)sw.Elapsed.TotalSeconds;

                    OnDownloadProgressChanged(new DownloadProgressChangedEventArgs()
                    {
                        CurrentReceivedBytes = byteSize,
                        BytesReceived = totalReceived,
                        TotalBytesToReceive = ContentLength,
                        CurrentSpeed = currentSpeed,
                        ProgressPercentage = downloadResumable ? ((float)totalReceived / (float)ContentLength) * 100 : 0,
                        TimeLeft = downloadResumable ? (long)(ContentLength - totalReceived / currentSpeed) : 0
                    });
                }
                sw.Stop();
            }
        }

        public bool DownloadFileToBuffer(string DownloadLink, in Stream output)
        {
            bool ret = true;
            stop = false;

            long ExistingLength = output.Length;

            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(DownloadLink);
            request.UserAgent = userAgent;
            request.AddRange(ExistingLength);

            try
            {
                using (var response = (HttpWebResponse)request.GetResponse())
                {
                    long FileSize = ExistingLength + response.ContentLength;
                    bool downloadResumable;

                    long ContentLength = response.ContentLength;

                    if ((int)response.StatusCode == 206)
                    {
                        var downloadStatusArgs = new DownloadStatusChangedEventArgs();
                        downloadResumable = true;
                        downloadStatusArgs.ResumeSupported = downloadResumable;
                        OnDownloadStatusChanged(downloadStatusArgs);
                    }
                    else
                    {
                        //LoggerLine("This download is not resumable.", LogType.Warning);
                        ExistingLength = 0;
                        var downloadStatusArgs = new DownloadStatusChangedEventArgs();
                        downloadResumable = false;
                        downloadStatusArgs.ResumeSupported = downloadResumable;
                        OnDownloadStatusChanged(downloadStatusArgs);
                        output.Flush();
                    }

                    ReadRemoteResponse(response, output, ExistingLength, ContentLength, downloadResumable);
                }
                OnDownloadCompleted(new DownloadProgressCompletedEventArgs());
            }
            catch (WebException e)
            {
                ret = ThrowWebExceptionAsBool(e);
            }
            catch (Exception e)
            {
                LogWriteLine($"An error occured while downloading to buffer\r\nTraceback: {e}", LogType.Error, true);
                ret = false;
            }

            return ret;
        }

        bool ThrowWebExceptionAsBool(WebException e)
        {
            switch (GetStatusCodeResponse(e.Response))
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

        int GetStatusCodeResponse(WebResponse e) => e == null ? -1 : (int)((HttpWebResponse)e).StatusCode;

        public void ReadRemoteResponse(HttpWebResponse response, in Stream localStream, long ExistingLength, long ContentLength, bool downloadResumable)
        {
            Stopwatch sw;
            using (remoteStream = response.GetResponseStream())
            {
                int byteSize = 0;
                long totalReceived = byteSize + ExistingLength;
                float currentSpeed;
                sw = new Stopwatch();
                sw.Start();
                byte[] downBuffer = new byte[bufflength];
                while ((byteSize = remoteStream.Read(downBuffer, 0, downBuffer.Length)) > 0)
                {
                    localStream.Write(downBuffer, 0, byteSize);
                    totalReceived += byteSize;
                    currentSpeed = totalReceived / (float)sw.Elapsed.TotalSeconds;

                    OnDownloadProgressChanged(new DownloadProgressChangedEventArgs()
                    {
                        CurrentReceivedBytes = byteSize,
                        BytesReceived = totalReceived,
                        TotalBytesToReceive = ContentLength,
                        CurrentSpeed = currentSpeed,
                        ProgressPercentage = downloadResumable ? ((float)totalReceived / (float)ContentLength) * 100 : 0,
                        TimeLeft = downloadResumable ? (long)(ContentLength - totalReceived / currentSpeed) : 0
                    });
                }
                sw.Stop();
            }
        }

        protected virtual void OnDownloadStatusChanged(DownloadStatusChangedEventArgs e)
        {
            EventHandler<DownloadStatusChangedEventArgs> handler = DownloadStatusChanged;
            if (handler != null)
            {
                handler(this, e);
            }
        }

        protected virtual void OnDownloadProgressChanged(DownloadProgressChangedEventArgs e)
        {
            EventHandler<DownloadProgressChangedEventArgs> handler = DownloadProgressChanged;
            if (handler != null)
            {
                handler(this, e);
            }
        }

        protected virtual void OnDownloadCompleted(DownloadProgressCompletedEventArgs e)
        {
            EventHandler<DownloadProgressCompletedEventArgs> handler = DownloadCompleted;
            if (handler != null)
            {
                handler(this, e);
            }
        }
    }
    public class DownloadTool
    {
        public bool DownloadToBuffer(string input, in MemoryStream output, string CustomMessage = "", bool updateProgress = false)
        {
            DownloadUtils client = new DownloadUtils();

            client.DownloadProgressChanged += Client_DownloadProgressChanged(CustomMessage != "" ? CustomMessage : $"Downloading to buffer", updateProgress);
            client.DownloadCompleted += Client_DownloadFileCompleted();

            while (!client.DownloadFileToBuffer(input, output))
            {
                LogWriteLine($"Retrying...", LogType.Warning);
                Task.Run(async () => { await Task.Delay(3000); }).Wait();
            }

            return false;
        }

        private EventHandler<DownloadProgressChangedEventArgs> Client_DownloadProgressChanged(string customMessage = "", bool updateProgress = false)
        {
            Action<object, DownloadProgressChangedEventArgs> action = (sender, e) =>
            {
#if DEBUG
                LogWrite($"{customMessage} \u001b[33;1m{(byte)e.ProgressPercentage}%"
                 + $"\u001b[0m ({SummarizeSizeSimple(e.BytesReceived)}) (\u001b[32;1m{SummarizeSizeSimple(e.CurrentSpeed)}/s\u001b[0m)", LogType.NoTag, false, true);
#endif
            };
            return new EventHandler<DownloadProgressChangedEventArgs>(action);
        }

        private static EventHandler<DownloadProgressCompletedEventArgs> Client_DownloadFileCompleted()
        {
            Action<object, DownloadProgressCompletedEventArgs> action = (sender, e) =>
            {
#if DEBUG
                LogWrite($" Done!", LogType.Empty);
                LogWriteLine();
#endif
            };
            return new EventHandler<DownloadProgressCompletedEventArgs>(action);
        }
    }
}
