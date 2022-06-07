using System.Diagnostics;
using System.IO;

namespace Hi3Helper.Data
{
    public partial class HttpClientHelper
    {
        private int _MergeBufferSize = 4 << 17; // 512 KiB
        private void MergeSlices()
        {
            this._DownloadedSize = 0;
            this._LastContinuedSize = 0;
            this._Stopwatch = Stopwatch.StartNew();

            byte[] Buffer = new byte[_MergeBufferSize];

            Stream SliceStream, OutputStream;
            string SlicePath;

            int Read = 0;
            UpdateProgress(new _DownloadProgress(_DownloadedSize, _TotalSizeToDownload, Read, _LastContinuedSize, _Stopwatch.Elapsed, _DownloadState));

            using (OutputStream = new FileStream(_OutputPath, FileMode.Create, FileAccess.Write, FileShare.Write))
            {
                for (int i = 0; i < _ThreadNumber; i++)
                {
                    SlicePath = string.Format("{0}.{1:000}", _OutputPath, i + 1);
                    using (SliceStream = new FileStream(SlicePath, FileMode.Open, FileAccess.Read, FileShare.Read, _DownloadBufferSize, FileOptions.DeleteOnClose))
                    {
                        while ((Read = SliceStream.Read(Buffer, 0, Buffer.Length)) > 0)
                        {
                            this._DownloadState = State.Merging;
                            _ThreadToken.ThrowIfCancellationRequested();
                            _DownloadedSize += Read;
                            _LastContinuedSize += Read;
                            OutputStream.Write(Buffer, 0, Read);
                            UpdateProgress(new _DownloadProgress(_DownloadedSize, _TotalSizeToDownload, Read, _LastContinuedSize, _Stopwatch.Elapsed, _DownloadState));
                        }
                    }
                }
            }
        }
    }
}
