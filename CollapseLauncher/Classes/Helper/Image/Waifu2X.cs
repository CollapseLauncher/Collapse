using Hi3Helper;
using System;
using System.IO;
using System.Runtime.InteropServices;

namespace CollapseLauncher.Helper.Image
{
    #region ncnn Defines
    public static class Ncnn
    {
        public const string DllName = "Lib\\waifu2x-ncnn-vulkan";

        [DllImport(DllName)]
        private static extern int ncnn_get_default_gpu_index();

        [DllImport(DllName)]
        private static extern IntPtr ncnn_get_gpu_name(int gpuId);

        public static int DefaultGpuIndex => ncnn_get_default_gpu_index();

        public static string GetGpuName(int gpuId)
        {
            return Marshal.PtrToStringUTF8(ncnn_get_gpu_name(gpuId));
        }
    }
    #endregion

    public class Waifu2X : IDisposable
    {
        public const string DllName = "Lib\\waifu2x-ncnn-vulkan";

        #region DllImports
        [DllImport(DllName)]
        private static extern IntPtr waifu2x_create(int gpuId = 0, bool ttaMode = false, int numThreads = 0);

        [DllImport(DllName)]
        private static extern void waifu2x_destroy(IntPtr context);

        [DllImport(DllName)]
        private static extern unsafe int waifu2x_load(IntPtr context, byte* param, byte* model);

        [DllImport(DllName)]
        private static extern unsafe int waifu2x_process(IntPtr context, int w, int h, int c, byte* inData, byte* outData);

        [DllImport(DllName)]
        private static extern unsafe int waifu2x_process_cpu(IntPtr context, int w, int h, int c, byte* inData, byte* outData);

        [DllImport(DllName)]
        private static extern void waifu2x_set_param(IntPtr context, Param param, int value);

        [DllImport(DllName)]
        private static extern int waifu2x_get_param(IntPtr context, Param param);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
        private static extern long GetPackagesByPackageFamily(string packageFamilyName, ref uint count, [Out] IntPtr packageFullNames, ref uint bufferLength, [Out] IntPtr buffer);
        #endregion

        #region Enums
        public enum Param
        {
            Noise,
            Scale,
            TileSize,
        }

        public enum Waifu2XStatus
        {
            Ok,

            Warning,
            CpuMode = Warning,

            Error,
            NotAvailable = Error,
            TestNotPassed,
            NotInitialized,
            D3DMappingLayers,
        }
        #endregion

        #region Properties
        private IntPtr _context;
        private byte[] _paramBuffer;
        private byte[] _modelBuffer;
        private bool _testPassed;
        private Waifu2XStatus _status;
        #endregion

        public Waifu2XStatus Status => _status;

        #region Main Methods
        public Waifu2X()
        {
            try
            {
                if (CheckD3DMappingLayersPackage())
                {
                    _status = Waifu2XStatus.D3DMappingLayers;
                    Logger.LogWriteLine("D3DMappingLayers package detected. Waifu2X feature will be disabled.", LogType.Warning, true);
                    return;
                }

                var gpuId = Ncnn.DefaultGpuIndex;
                if (gpuId == -1)
                {
                    // Fallback to CPU mode
                    _status = Waifu2XStatus.CpuMode;
                    Logger.LogWriteLine("No available Vulkan GPU device was found and CPU mode will be used. This will greatly increase image processing time.", LogType.Warning, true);
                }
                _context = waifu2x_create(gpuId);
                _status = Waifu2XStatus.Ok;
                Logger.LogWriteLine($"Waifu2X initialized successfully with device: {Ncnn.GetGpuName(gpuId)}", LogType.Default, true);
            }
            catch (DllNotFoundException)
            {
                _status = Waifu2XStatus.NotAvailable;
                Logger.LogWriteLine("Dll file \"waifu2x-ncnn-vulkan.dll\" can not be found. Waifu2X feature will be disabled.", LogType.Error, true);
            }
        }

        public void Dispose()
        {
            if (_context != 0)
            {
                waifu2x_destroy(_context);
                _context = 0;
                _status = Waifu2XStatus.NotInitialized;
                Logger.LogWriteLine("Waifu2X is destroyed!");
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
                _status = Waifu2XStatus.TestNotPassed;
                Logger.LogWriteLine("Waifu2X model file can not be found. Waifu2X feature will be disabled.", LogType.Error, true);
                return false;
            }

            return Load(_paramBuffer, _modelBuffer);
        }
        #endregion

        #region Process Methods
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
        #endregion

        #region Parameters
        public void SetParam(Param param, int value)
        {
            if (_context == 0) throw new NotSupportedException();
            waifu2x_set_param(_context, param, value);
        }

        public int GetParam(Param param)
        {
            if (_context == 0) throw new NotSupportedException();
            return waifu2x_get_param(_context, param);
        }
        #endregion

        #region Misc
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
                if (!_testPassed)
                {
                    Logger.LogWriteLine("Waifu2X self test failed, got an empty output image.", LogType.Error, true);
                }
            }
            return _testPassed;
        }

        private bool CheckD3DMappingLayersPackage()
        {
            const string FAMILY_NAME = "Microsoft.D3DMappingLayers_8wekyb3d8bbwe";
            uint count = 0, bufferLength = 0;
            GetPackagesByPackageFamily(FAMILY_NAME, ref count, 0, ref bufferLength, 0);
            return count != 0;
        }
        #endregion
    }
}
