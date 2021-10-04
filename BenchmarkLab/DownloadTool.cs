using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;

namespace BenchmarkLab
{
    public class DownloadTool
    {
        long bufflength = 262144;
        HttpClient httpClient = new HttpClient(new HttpClientHandler { AutomaticDecompression = DecompressionMethods.Deflate | DecompressionMethods.GZip });
        Stream localStream;
        Stream remoteStream;

        public async Task<bool> Dunlud(string inp, string outp)
        {
            long ExistingLength = 0;
            int byteSize;
            localStream = new FileStream(outp, FileMode.Create, FileAccess.Write);

            var request = new HttpRequestMessage { RequestUri = new Uri(inp) };
            request.Headers.Range = new RangeHeaderValue(ExistingLength, null);

            byte[] buffer = new byte[bufflength];

            using (var response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead))
            {
                Console.WriteLine($"{response.Content.Headers.ContentLength ?? 0}");
                using (remoteStream = await response.Content.ReadAsStreamAsync())
                {
                    while ((byteSize = await remoteStream.ReadAsync(buffer, 0, buffer.Length)) > 0)
                    {
                        await localStream.WriteAsync(buffer, 0, byteSize);
                    }
                }
            }

            localStream.Dispose();
            remoteStream.Dispose();

            return true;
        }
    }
}
