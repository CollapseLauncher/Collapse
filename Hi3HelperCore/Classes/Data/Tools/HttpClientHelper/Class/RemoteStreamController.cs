using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;

namespace Hi3Helper.Data
{
    public partial class HttpClientHelper
    {
        private long _TotalSizeToDownload = 0,
                     _DownloadedSize = 0,
                     _LastContinuedSize = 0,
                     _SelfReadFileSize = 0;

        private int _DownloadBufferSize = 4 << 11; // 8 KiB

        private Stream _OutputStream;
        private bool _UseStreamOutput;

        private Stream GetFileStream(string FileName, bool ForceCreateNew = false) =>
            new FileStream(FileName, ForceCreateNew ? FileMode.Create : FileMode.OpenOrCreate, FileAccess.Write, FileShare.Write);

        private long GetFileSize(string FileName)
        {
            FileInfo File = new FileInfo(FileName);
            return File.Exists ? File.Length : 0;
        }

        private long GetSlicesSize(string FileName)
        {
            long SlicesSize = 0;
            FileInfo File;
            for (int i = 0; i < _ThreadNumber; i++)
            {
                File = new FileInfo(string.Format("{0}.{1:000}", _OutputPath, i + 1));
                SlicesSize += File.Exists ? File.Length : 0;
            }
            return SlicesSize;
        }

        private Stream SeekStreamToEnd(Stream stream)
        {
            stream.Seek(0, SeekOrigin.End);
            return stream;
        }

        private async Task<Stream> GetRemoteStream(HttpResponseMessage Response) => await Response.Content.ReadAsStreamAsync();

        private HttpResponseMessage CheckResponseStatusCode(HttpResponseMessage Input)
        {
            if (Input.StatusCode == HttpStatusCode.RequestedRangeNotSatisfiable)
                throw new InvalidDataException($"Return Code: {(int)Input.StatusCode} ({Input.StatusCode}). File may already completed or Server cannot respond ContentLength. Ignoring!");
            if (!Input.IsSuccessStatusCode)
                throw new HttpRequestException($"Error Occured while Reading Response from {Input.RequestMessage?.RequestUri} with Return Code: {(int)Input.StatusCode} ({Input.StatusCode})");

            return Input;
        }

        private void ReadStream(Stream RemoteStream, Stream LocalStream)
        {
            int read = 0;
            byte[] buffer = new byte[_DownloadBufferSize];
            while ((read = RemoteStream.Read(buffer, 0, buffer.Length)) > 0)
            {
                _DownloadedSize += read;
                _LastContinuedSize += read;
                _ThreadToken.ThrowIfCancellationRequested();
                LocalStream.Write(buffer, 0, read);
                UpdateProgress(new _DownloadProgress(_DownloadedSize, _TotalSizeToDownload, read, _LastContinuedSize, _Stopwatch.Elapsed, _DownloadState));
            }
        }

        private async Task ReadStreamAsync(Stream RemoteStream, Stream LocalStream)
        {
            int read = 0;
            byte[] buffer = new byte[_DownloadBufferSize];
            while ((read = await RemoteStream.ReadAsync(buffer, 0, buffer.Length, _ThreadToken)) > 0)
            {
                _DownloadedSize += read;
                _LastContinuedSize += read;
                LocalStream.Write(buffer, 0, read);
                UpdateProgress(new _DownloadProgress(_DownloadedSize, _TotalSizeToDownload, read, _LastContinuedSize, _Stopwatch.Elapsed, _DownloadState));
            }
        }
    }
}
