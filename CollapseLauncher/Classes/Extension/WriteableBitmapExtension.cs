using Hi3Helper;
using Hi3Helper.SentryHelper;
using Hi3Helper.Win32.ManagedTools;
using Hi3Helper.Win32.Native.Interfaces;
using Hi3Helper.Win32.WinRT.IBufferCOM;
using Microsoft.UI.Xaml.Media.Imaging;
using System;
// ReSharper disable CheckNamespace
#pragma warning disable IDE0130

#nullable enable
namespace CollapseLauncher.Extension
{
    internal static class WriteableBitmapExtension
    {
        internal static nint GetBufferPointer(this WriteableBitmap writeableBitmap)
        {
            IBufferByteAccess byteAccess = writeableBitmap.PixelBuffer.AsBufferByteAccess();
            try
            {
                byteAccess.Buffer(out nint bufferP);
                return bufferP;
            }
            finally
            {
                if (!ComMarshal<IBufferByteAccess>.TryReleaseComObject(byteAccess, out Exception? ex))
                {
                    SentryHelper.ExceptionHandler(ex, SentryHelper.ExceptionType.UnhandledOther);
                    Logger.LogWriteLine($"Cannot free the instance of IBufferByteAccess from WriteableBitmap\r\n{ex}",
                                        LogType.Error,
                                        true);
                }
            }
        }
    }
}
