using Hi3Helper;
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
            TestNotPassed,
        }

        private IntPtr _context;
        private byte[] _paramBuffer;
        private byte[] _modelBuffer;
        private bool _testPassed;

        public Waifu2XStatus Status
        {
            get
            {
                // Usability testing
                if (_context == 0)
                    return Waifu2XStatus.NotAvailable;
                else if (!_testPassed)
                    return Waifu2XStatus.TestNotPassed;

                if (waifu2x_support_gpu(_context))
                    return Waifu2XStatus.Ok;
                else
                    return Waifu2XStatus.CpuMode;
            }
        }

        public Waifu2X(int gpuId = 0, bool ttaMode = false, int numThreads = 1)
        {
            try
            {
                _context = waifu2x_create(gpuId, ttaMode, numThreads);
                if (_context == 0)
                {
                    // Fallback to CPU mode
                    Logger.LogWriteLine("No available Vulkan GPU device was found and CPU mode will be used. This will greatly increase image processing time.", LogType.Warning, true);
                    _context = waifu2x_create(-1, ttaMode, numThreads);
                }
            }
            catch (DllNotFoundException)
            {
                Logger.LogWriteLine("Dll file \"waifu2x-ncnn-vulkan.dll\" can not be found. Waifu2X feature will be disabled.", LogType.Error, true);
            }
        }

        public void Dispose()
        {
            if (_context != 0)
            {
                waifu2x_destroy(_context);
                _context = 0;
            }
        }

        public unsafe bool Load(ReadOnlySpan<byte> param, ReadOnlySpan<byte> model)
        {
            if (_context == 0) throw new NotSupportedException();
            fixed (byte* pParam = param, pModel = model)
            {
                return waifu2x_load(_context, pParam, pModel) == 0 && Test();
            }
        }

        public bool Load(string paramPath, string modelPath)
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
            catch (IOException)
            {
                _testPassed = false;
                Logger.LogWriteLine("Waifu2X model file can not be found. Waifu2X feature will be disabled.", LogType.Error, true);
                return false;
            }

            return Load(_paramBuffer, _modelBuffer);
        }

        public unsafe int Process(int w, int h, int c, ReadOnlySpan<byte> inData, Span<byte> outData)
        {
            if (_context == 0) throw new NotSupportedException();
            fixed (byte* pInData = inData, pOutData = outData)
            {
                Logger.LogWriteLine($"Waifu2X processing begins. Source image resolution: {w}x{h}.");
                var timeBegin = DateTime.Now;
                var ret = waifu2x_process(_context, w, h, c, pInData, pOutData);
                Logger.LogWriteLine($"Waifu2X processing ends. Source image resolution: {w}x{h}. Elapsed time: {(DateTime.Now - timeBegin).TotalMilliseconds} ms.");
                return ret;
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

        private bool Test()
        {
            if (Status == Waifu2XStatus.NotAvailable)
            {
                _testPassed = false;
            }
            else
            {
                // Test scaling a 1x1 white image to 2x2 size
                var inData = new byte[] { 0xFF, 0xFF, 0xFF };
                var outData = new byte[2 * 2 * 3];
                Process(1, 1, 3, inData, outData);
                _testPassed = outData[0] != 0;
                Logger.LogWriteLine("Waifu2X self test failed, got an empty output image.", LogType.Error, true);
            }
            return _testPassed;
        }
    }
}
