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
        private bool _UseStreamOutput,
                     _DisposeStream;

        private Stream SeekStreamToEnd(Stream stream)
        {
            if (this._DisposeStream)
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

        private async Task ReadStreamAsync(_ThreadProperty Property)
        {
            int read = 0;
            byte[] buffer = new byte[_DownloadBufferSize];
            while ((read = await Property.RemoteStream.ReadAsync(buffer, 0, buffer.Length, _ThreadToken)) > 0)
            {
                _DownloadedSize += read;
                _LastContinuedSize += read;
                Property.LocalStream.Write(buffer, 0, read);
                UpdateProgress(new _DownloadProgress(_DownloadedSize, _TotalSizeToDownload, read, _LastContinuedSize, _Stopwatch.Elapsed, _DownloadState));
                Property.CurrentRetry = 1;
            }
        }
    }
}
