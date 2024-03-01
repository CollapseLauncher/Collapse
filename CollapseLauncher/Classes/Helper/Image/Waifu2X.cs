using System;
using System.IO;
using System.Runtime.InteropServices;

namespace CollapseLauncher.Helper.Image
{
    public class Waifu2X : IDisposable
    {
        public const string DllName = "Lib\\waifu2x-ncnn-vulkan";

        [DllImport(DllName)]
        private static extern IntPtr waifu2x_create(int gpuId = 0, bool ttaMode = false, int numThreads = 1);

        [DllImport(DllName)]
        private static extern void waifu2x_destroy(IntPtr context);

        [DllImport(DllName)]
        private static extern unsafe int waifu2x_load(IntPtr context, byte* param, byte* model);

        [DllImport(DllName)]
        private static extern unsafe int waifu2x_process(IntPtr context, int w, int h, int c, byte* inData, byte* outData);

        [DllImport(DllName)]
        private static extern unsafe int waifu2x_process_cpu(IntPtr context, int w, int h, int c, byte* inData, byte* outData);

        [DllImport(DllName)]
        private static extern void waifu2x_set(IntPtr context, Param param, int value);

        [DllImport(DllName)]
        private static extern int waifu2x_get(IntPtr context, Param param);

        [DllImport(DllName)]
        [return:MarshalAs(UnmanagedType.I1)]
        private static extern bool waifu2x_support_gpu(IntPtr context);

        public enum Param
        {
            Noise,
            Scale,
            TileSize,
        }

        public enum Waifu2XStatus
        {
            Ok,
            CpuMode,
            NotAvailable,
        }

        private IntPtr _context;
        private byte[] _paramBuffer;
        private byte[] _modelBuffer;

        public Waifu2XStatus Status
        {
            get
            {
                if (_context == 0)
                    return Waifu2XStatus.NotAvailable;
                else if (waifu2x_support_gpu(_context))
                    return Waifu2XStatus.Ok;
                else
                    return Waifu2XStatus.CpuMode;
            }
        }

        public Waifu2X(int gpuId = 0, bool ttaMode = false, int numThreads = 1)
        {
            _context = waifu2x_create(gpuId, ttaMode, numThreads);
        }

        public void Dispose()
        {
            if (_context != 0)
            {
                waifu2x_destroy(_context);
                _context = 0;
            }
        }

        public unsafe int Load(ReadOnlySpan<byte> param, ReadOnlySpan<byte> model)
        {
            if (_context == 0) throw new NotSupportedException();
            fixed (byte* pParam = param, pModel = model)
            {
                return waifu2x_load(_context, pParam, pModel);
            }
        }

        public int Load(string paramPath, string modelPath)
        {
            if (_context == 0) throw new NotSupportedException();
            try
            {
                using (var ms = new MemoryStream())
                using (var fs = new FileStream(paramPath, FileMode.Open))
                {
                    fs.CopyTo(ms);
                    _paramBuffer = ms.ToArray();
                }

                using (var ms = new MemoryStream())
                using (var fs = new FileStream(modelPath, FileMode.Open))
                {
                    fs.CopyTo(ms);
                    _modelBuffer = ms.ToArray();
                }
            }
            catch (FileNotFoundException)
            {
                return -1;
            }

            return Load(_paramBuffer, _modelBuffer);
        }

        public unsafe int Process(int w, int h, int c, ReadOnlySpan<byte> inData, Span<byte> outData)
        {
            if (_context == 0) throw new NotSupportedException();
            fixed (byte* pInData = inData, pOutData = outData)
            {
                return waifu2x_process(_context, w, h, c, pInData, pOutData);
            }
        }

        public unsafe int ProcessCpu(int w, int h, int c, ReadOnlySpan<byte> inData, Span<byte> outData)
        {
            if (_context == 0) throw new NotSupportedException();
            fixed (byte* pInData = inData, pOutData = outData)
            {
                return waifu2x_process_cpu(_context, w, h, c, pInData, pOutData);
            }
        }

        public void Set(Param param, int value)
        {
            if (_context == 0) throw new NotSupportedException();
            waifu2x_set(_context, param, value);
        }

        public int Get(Param param)
        {
            if (_context == 0) throw new NotSupportedException();
            return waifu2x_get(_context, param);
        }
    }
}
