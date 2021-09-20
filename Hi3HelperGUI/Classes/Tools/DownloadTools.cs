using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Hi3HelperGUI;
using Hi3HelperGUI.Preset;
using static Hi3HelperGUI.Logger;
using static Hi3HelperGUI.ConverterTools;

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

        public async Task<bool> DownloadFileAsync(string DownloadLink, string path, CancellationToken token)
        {
            bool ret = true;
            stop = false;

            long ExistingLength = 0;
            long ContentLength = 0;
            FileStream saveFileStream;

            if (File.Exists(path))
            {
                FileInfo fileInfo = new FileInfo(path);
                ExistingLength = fileInfo.Length;
            }

            saveFileStream = new FileStream(path, (ExistingLength > 0 ? FileMode.Append : FileMode.Create), FileAccess.Write, FileShare.ReadWrite);

            HttpWebRequest request = (HttpWebRequest)HttpWebRequest.Create(DownloadLink);

            request.UserAgent = userAgent;
            request.AddRange(ExistingLength);

            try
            {
                using (var response = (HttpWebResponse)await request.GetResponseAsync())
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
                        saveFileStream.Dispose();
                        File.WriteAllText(path, string.Empty);
                        saveFileStream = new FileStream(path, FileMode.Append, FileAccess.Write, FileShare.ReadWrite);
                    }

                    using (var stream = response.GetResponseStream())
                    {
                        byte[] downBuffer = new byte[4096];
                        int byteSize = 0;
                        long totalReceived = byteSize + ExistingLength;
                        var sw = new Stopwatch();
                        sw.Start();
                        while ((byteSize = await stream.ReadAsync(downBuffer, 0, downBuffer.Length, token)) > 0)
                        {
                            await saveFileStream.WriteAsync(downBuffer, 0, byteSize, token);
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

                            if (stop == true)
                                return false;
                        }
                        sw.Stop();
                    }
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
                saveFileStream.Dispose();
            }

            return ret;
        }


        public bool DownloadFile(string DownloadLink, string path)
        {
            bool ret = true;
            stop = false;

            long ExistingLength = 0;
            long ContentLength = 0;
            FileStream saveFileStream;

            if (File.Exists(path))
            {
                FileInfo fileInfo = new FileInfo(path);
                ExistingLength = fileInfo.Length;
            }

            saveFileStream = new FileStream(path, (ExistingLength > 0 ? FileMode.Append : FileMode.Create), FileAccess.Write, FileShare.ReadWrite);

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
                        saveFileStream.Dispose();
                        File.WriteAllText(path, string.Empty);
                        saveFileStream = new FileStream(path, FileMode.Append, FileAccess.Write, FileShare.ReadWrite);
                    }

                    using (var stream = response.GetResponseStream())
                    {
                        byte[] downBuffer = new byte[4096];
                        int byteSize = 0;
                        long totalReceived = byteSize + ExistingLength;
                        var sw = new Stopwatch();
                        sw.Start();
                        while ((byteSize = stream.Read(downBuffer, 0, downBuffer.Length)) > 0)
                        {
                            saveFileStream.Write(downBuffer, 0, byteSize);
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

                            if (stop == true)
                                return false;
                        }
                        sw.Stop();
                    }
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
                LogWriteLine($"An error occured while downloading {Path.GetFileName(path)}\r\nTraceback: {e}", LogType.Error, true);
                ret = false;
            }
            finally
            {
                saveFileStream.Dispose();
            }

            return ret;
        }

        public bool DownloadFileToBuffer(string DownloadLink, in MemoryStream output)
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

                    using (var stream = response.GetResponseStream())
                    {
                        byte[] downBuffer = new byte[4096];
                        int byteSize = 0;
                        long totalReceived = byteSize + ExistingLength;
                        var sw = new Stopwatch();
                        sw.Start();
                        while ((byteSize = stream.Read(downBuffer, 0, downBuffer.Length)) > 0)
                        {
                            output.Write(downBuffer, 0, byteSize);
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

                            if (stop == true)
                                return false;
                        }
                        sw.Stop();
                    }
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


        public long GetContentLength(string DownloadLink)
        {
            HttpWebRequest request = (HttpWebRequest)HttpWebRequest.Create(DownloadLink);
            HttpWebResponse res = (HttpWebResponse)request.GetResponse();
            return res.ContentLength;
        }

        public void StopDownload() => stop = true;

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
    public class DownloadTools
    {
        public async Task<bool> DownloadUpdateFilesAsync(List<UpdateDataProperties> input, CancellationToken token)
        {
            ushort o = 1;

            foreach (UpdateDataProperties i in input)
            {
                DownloadUtils client = new DownloadUtils();
                if (!Directory.Exists(Path.GetDirectoryName(i.ActualPath)))
                    Directory.CreateDirectory(Path.GetDirectoryName(i.ActualPath));

                client.DownloadProgressChanged += Client_DownloadProgressChanged($"Down: [{i.ZoneName} > {i.DataType}] ({o}/{input.Count}) {Path.GetFileName(i.N)}");
                client.DownloadCompleted += Client_DownloadFileCompleted();

                while (!await client.DownloadFileAsync(i.RemotePath, i.ActualPath, token))
                    LogWriteLine($"Retrying...", LogType.Warning);

                o++;
            }

            return false;
        }

        public bool DownloadToBuffer(string input, in MemoryStream output, string CustomMessage = "")
        {
            DownloadUtils client = new DownloadUtils();

            client.DownloadProgressChanged += Client_DownloadProgressChanged((CustomMessage != "" ? CustomMessage : $"Downloading to buffer"));
            client.DownloadCompleted += Client_DownloadFileCompleted();

            while (!client.DownloadFileToBuffer(input, output))
                LogWriteLine($"Retrying...", LogType.Warning);

            return false;
        }

        public bool DownloadUpdateFiles(List<UpdateDataProperties> input)
        {
            ushort o = 1;
            foreach (UpdateDataProperties i in input)
            {
                DownloadUtils client = new DownloadUtils();
                if (!Directory.Exists(Path.GetDirectoryName(i.ActualPath)))
                    Directory.CreateDirectory(Path.GetDirectoryName(i.ActualPath));

                client.DownloadProgressChanged += Client_DownloadProgressChanged($"Down: ({o}/{input.Count}) {Path.GetFileName(i.N)}");
                client.DownloadCompleted += Client_DownloadFileCompleted();

                client.DownloadFile(i.RemotePath, i.ActualPath);
                o += 1;
            }

            return false;
        }

        static long curRecBytes = 0;
        static long prevRecBytes = 0;
        static long bytesInterval;
        static DateTime beginTimeDownload = DateTime.Now;

        private EventHandler<DownloadProgressChangedEventArgs> Client_DownloadProgressChanged(string customMessage = "")
        {
            Action<object, DownloadProgressChangedEventArgs> action = (sender, e) =>
            {
                curRecBytes = e.BytesReceived;
                //if ((DateTime.Now - beginTime).TotalMilliseconds > 250)
                //{
                if ((curRecBytes - prevRecBytes) < 0)
                    bytesInterval = 0;
                if ((DateTime.Now - beginTimeDownload).TotalMilliseconds > 500)
                {
                    bytesInterval = ((curRecBytes - prevRecBytes) < 0 ? 0 : curRecBytes - prevRecBytes) * 2;
                    prevRecBytes = curRecBytes;
                    beginTimeDownload = DateTime.Now;
                }
                LogWrite($"{customMessage} \u001b[33;1m{(byte)e.ProgressPercentage}%"
                 + $"\u001b[0m ({SummarizeSizeSimple(e.BytesReceived)}) (\u001b[32;1m{(bytesInterval == 0 || e.NoProgress ? "n/a" : (SummarizeSizeSimple(bytesInterval) + "/s"))}\u001b[0m)", LogType.NoTag, false, true);
                //}
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
