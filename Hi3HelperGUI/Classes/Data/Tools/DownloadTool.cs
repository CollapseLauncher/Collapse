using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
#if (!NETFRAMEWORK)
using System.Net.Http;
using System.Net.Http.Headers;
#endif
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Hi3HelperGUI;
using Hi3HelperGUI.Preset;
using static Hi3HelperGUI.Logger;
using static Hi3HelperGUI.Data.ConverterTool;

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
    public class DownloadUtils
    {
        public event EventHandler<DownloadStatusChangedEventArgs> DownloadStatusChanged;
        public event EventHandler<DownloadProgressChangedEventArgs> DownloadProgressChanged;
        public event EventHandler DownloadCompleted;
        public string userAgent = "Dalvik/2.1.0 (Linux; U; Android 11; M2012K11AG Build/RKQ1.200826.002)";

        // By default, stop is true
        public bool stop = true;

        /* Declare download buffer
         * by default: 2 KiB (262144 bytes)
        */
        long bufflength = 262144;

#if (!NETFRAMEWORK)
        HttpClient httpClient = new HttpClient();
#endif

        HttpWebRequest request;
        DownloadStatusChangedEventArgs downloadStatusArgs;

        Stream localStream;
        Stream remoteStream;

        /* TODO:
         * Change from HttpWebRequest to HttpClient
         */

#if (!NETFRAMEWORK)
        public async Task<bool> DownloadFileAsyncNew(string DownloadLink, string path, CancellationToken token)
        {
            var downloadStatusArgs = new DownloadStatusChangedEventArgs();
            long ExistingLength = 0;
            bool downloadResumable;

            if (File.Exists(path))
            {
                FileInfo fileInfo = new FileInfo(path);
                ExistingLength = fileInfo.Length;
            }

            Console.WriteLine("Getting Request...");

            try
            {
                using (HttpResponseMessage message = await httpClient.GetAsync(DownloadLink, HttpCompletionOption.ResponseHeadersRead, token))
                {
                    message.Content.Headers.ContentRange = new ContentRangeHeaderValue(ExistingLength);
                    long FileSize = ExistingLength + message.Content.Headers.ContentLength ?? 0;

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

                    using (remoteStream = await message.Content.ReadAsStreamAsync(token).ConfigureAwait(false))
                    {
                        int byteSize = 0;
                        long totalReceived = byteSize + ExistingLength;
                        var sw = new Stopwatch();
                        sw.Start();

                        byte[] downBuffer = new byte[bufflength];
                        while ((byteSize = await remoteStream.ReadAsync(downBuffer, 0, downBuffer.Length, token).ConfigureAwait(false)) > 0)
                        {
                            Console.WriteLine(byteSize);
                            await localStream.WriteAsync(downBuffer, 0, byteSize, token).ConfigureAwait(false);
                            totalReceived += byteSize;

                            var args = new DownloadProgressChangedEventArgs();
                            args.CurrentReceivedBytes = byteSize;
                            args.BytesReceived = totalReceived;
                            args.TotalBytesToReceive = FileSize;
                            float currentSpeed = totalReceived / (float)sw.Elapsed.TotalSeconds;
                            args.CurrentSpeed = currentSpeed;
                            if (downloadResumable == true)
                            {
                                args.ProgressPercentage = ((float)totalReceived / (float)FileSize) * 100;
                                long bytesRemainingtoBeReceived = FileSize - totalReceived;
                                args.TimeLeft = (long)(bytesRemainingtoBeReceived / currentSpeed);
                            }
                            else
                            {
                                args.ProgressPercentage = 0;
                                args.TimeLeft = 0;
                            }

                            OnDownloadProgressChanged(args);
                        }
                        sw.Stop();
                    }
                    var completedArgs = new EventArgs();
                    OnDownloadCompleted(completedArgs);
                }
            }
            catch
            {

            }
            finally
            {
            }

            return true;
        }
#endif

        public async Task<bool> DownloadFileAsync(string DownloadLink, string path, CancellationToken token, long startOffset = -1, long endOffset = -1)
        {
            bool ret = true;
            downloadStatusArgs = new DownloadStatusChangedEventArgs();

            long ExistingLength = 0;

            if (File.Exists(path))
            {
                FileInfo fileInfo = new FileInfo(path);
                ExistingLength = fileInfo.Length;
            }

            request = (HttpWebRequest)WebRequest.Create(DownloadLink);

            request.UserAgent = userAgent;

            if (startOffset != -1 && endOffset != -1)
                request.AddRange(startOffset, endOffset);
            else
                request.AddRange(ExistingLength);

            try
            {
                using (HttpWebResponse response = (HttpWebResponse)await request.GetResponseAsync().ConfigureAwait(false))
                {
                    long ContentLength = ExistingLength + response.ContentLength;
                    bool downloadResumable;

                    if ((int)response.StatusCode == 206)
                    {
                        downloadResumable = true;
                        localStream = new FileStream(path, FileMode.Append, FileAccess.Write, FileShare.ReadWrite);
                    }
                    else
                    {
                        ExistingLength = 0;
                        downloadResumable = false;
                        localStream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.ReadWrite);
                    }
                    downloadStatusArgs.ResumeSupported = downloadResumable;
                    OnDownloadStatusChanged(downloadStatusArgs);

                    await ReadRemoteResponse(response, localStream, ExistingLength, ContentLength, downloadResumable, token).ConfigureAwait(false);
                }
                OnDownloadCompleted(new EventArgs());
            }
            catch (WebException e)
            {
                int respose = (int)((HttpWebResponse)e.Response).StatusCode;
                if (!(respose == 416))
                {
                    LogWriteLine(e.Message, LogType.Error);
                    ret = false;
                }
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
                while ((byteSize = await remoteStream.ReadAsync(downBuffer, 0, downBuffer.Length, token).ConfigureAwait(false)) > 0)
                {
                    await localStream.WriteAsync(downBuffer, 0, byteSize, token).ConfigureAwait(false);
                    totalReceived += byteSize;

                    DownloadProgressChangedEventArgs args = new DownloadProgressChangedEventArgs();
                    args.CurrentReceivedBytes = byteSize;
                    args.BytesReceived = totalReceived;
                    args.TotalBytesToReceive = ContentLength;
                    args.CurrentSpeed = totalReceived / (float)sw.Elapsed.TotalSeconds;
                    if (downloadResumable == true)
                    {
                        args.ProgressPercentage = ((float)totalReceived / (float)ContentLength) * 100;
                        args.TimeLeft = (long)(ContentLength - totalReceived / args.CurrentSpeed);
                    }
                    else
                    {
                        args.ProgressPercentage = 0;
                        args.TimeLeft = 0;
                    }

                    OnDownloadProgressChanged(args);
                }
                sw.Stop();
            }
        }

        public bool DownloadFileToBuffer(string DownloadLink, in Stream output)
        {
            bool ret = true;
            stop = false;

            long ExistingLength = output.Length;
            long ContentLength = 0;

            HttpWebRequest request = (HttpWebRequest)HttpWebRequest.Create(DownloadLink);
            request.UserAgent = userAgent;
            request.AddRange(ExistingLength);

            try
            {
                using (var response = (HttpWebResponse)request.GetResponse())
                {
                    long FileSize = ExistingLength + response.ContentLength;
                    bool downloadResumable;

                    ContentLength = response.ContentLength;

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
                        output.Dispose();
                    }

                    ReadRemoteResponse(response, output, ExistingLength, ContentLength, downloadResumable);
                }
                var completedArgs = new EventArgs();
                OnDownloadCompleted(completedArgs);
            }
            catch (WebException e)
            {
                int respose = (int)((HttpWebResponse)e.Response).StatusCode;
                if (!(respose == 416))
                {
                    LogWriteLine(e.Message, LogType.Error);
                    ret = false;
                }
            }
            catch (Exception e)
            {
                LogWriteLine($"An error occured while downloading to buffer\r\nTraceback: {e}", LogType.Error, true);
                ret = false;
            }

            return ret;
        }

        public void ReadRemoteResponse(HttpWebResponse response, in Stream localStream, long ExistingLength, long ContentLength, bool downloadResumable)
        {
            Stopwatch sw;
            using (remoteStream = response.GetResponseStream())
            {
                int byteSize = 0;
                long totalReceived = byteSize + ExistingLength;
                sw = new Stopwatch();
                sw.Start();
                byte[] downBuffer = new byte[bufflength];
                while ((byteSize = remoteStream.Read(downBuffer, 0, downBuffer.Length)) > 0)
                {
                    localStream.Write(downBuffer, 0, byteSize);
                    totalReceived += byteSize;

                    DownloadProgressChangedEventArgs args = new DownloadProgressChangedEventArgs();
                    args.CurrentReceivedBytes = byteSize;
                    args.BytesReceived = totalReceived;
                    args.TotalBytesToReceive = ContentLength;
                    args.CurrentSpeed = totalReceived / (float)sw.Elapsed.TotalSeconds;
                    if (downloadResumable == true)
                    {
                        args.ProgressPercentage = ((float)totalReceived / (float)ContentLength) * 100;
                        args.TimeLeft = (long)(ContentLength - totalReceived / args.CurrentSpeed);
                    }
                    else
                    {
                        args.ProgressPercentage = 0;
                        args.TimeLeft = 0;
                    }

                    OnDownloadProgressChanged(args);
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

        protected virtual void OnDownloadCompleted(EventArgs e)
        {
            EventHandler handler = DownloadCompleted;
            if (handler != null)
            {
                handler(this, e);
            }
        }
    }
    public class DownloadTool
    {
        public bool DownloadToBuffer(string input, in MemoryStream output, string CustomMessage = "")
        {
            DownloadUtils client = new DownloadUtils();

            client.DownloadProgressChanged += Client_DownloadProgressChanged(CustomMessage != "" ? CustomMessage : $"Downloading to buffer");
            client.DownloadCompleted += Client_DownloadFileCompleted();

            while (!client.DownloadFileToBuffer(input, output))
                LogWriteLine($"Retrying...", LogType.Warning);

            return false;
        }

        private EventHandler<DownloadProgressChangedEventArgs> Client_DownloadProgressChanged(string customMessage = "")
        {
            Action<object, DownloadProgressChangedEventArgs> action = (sender, e) =>
            {
                LogWrite($"{customMessage} \u001b[33;1m{(byte)e.ProgressPercentage}%"
                 + $"\u001b[0m ({SummarizeSizeSimple(e.BytesReceived)}) (\u001b[32;1m{SummarizeSizeSimple(e.CurrentSpeed)}/s\u001b[0m)", LogType.NoTag, false, true);
            };
            return new EventHandler<DownloadProgressChangedEventArgs>(action);
        }

        private static EventHandler Client_DownloadFileCompleted()
        {
            Action<object, EventArgs> action = (sender, e) =>
            {
                LogWrite($" Done!", LogType.Empty);
                Console.WriteLine();
            };
            return new EventHandler(action);
        }
    }
}
