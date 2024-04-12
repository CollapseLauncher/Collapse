using System;
using System.Buffers.Text;
using System.Text;

namespace CollapseLauncher.Helper.Metadata.JsonConverter
{
    internal static class Extension
    {
        internal static string GetServeV3String(ReadOnlySpan<byte> data)
        {
            // Check if the string is a base64 string. If yes, then try to decode them
            bool isValidB64Data = Base64.IsValid(data);
            if (isValidB64Data)
            {
                // Get the output data span and decode the base64 data to raw data
                Span<byte> dataBytes = new byte[data.Length];
                Base64.DecodeFromUtf8(data, dataBytes, out _, out int dataB64DecodedWritten, true);

                // Check if the data is ServeV3 data. If yes, then process the data
                if (DataCooker.IsServeV3Data(dataBytes))
                {
                    // Get the size and initialize the output buffer
                    DataCooker.GetServeV3DataSize(dataBytes, out long compressedSize, out long decompressedSize);
                    byte[] outBuffer = new byte[decompressedSize];

                    // Decode the ServeV3 data to the outBuffer
                    DataCooker.ServeV3Data(dataBytes.Slice(0, dataB64DecodedWritten), outBuffer, (int)compressedSize, (int)decompressedSize, out int dataWritten);

                    // Get the string out of outBuffer and return it
                    string outString = Encoding.UTF8.GetString(outBuffer.AsSpan(0, dataWritten));
                    return outString;
                }

                // If it's not ServeV3 data, then return it as a raw string
                return Encoding.UTF8.GetString(data);
            }

            // If it's not base64 data, then return the string.
            return Encoding.UTF8.GetString(data);
        }
    }

}
