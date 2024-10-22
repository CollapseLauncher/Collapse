using Hi3Helper;
using Hi3Helper.Shared.Region;
using System;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;

namespace CollapseLauncher.Helper.Image
{
    #region ncnn Defines
    public static class Ncnn
    {
        public const string DllName = "Lib\\waifu2x-ncnn-vulkan";

        [DllImport(DllName, ExactSpelling = true)]
        private static extern int ncnn_get_default_gpu_index();

        [DllImport(DllName, ExactSpelling = true)]
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

#nullable enable
        private static string? appDirPath = null;
#nullable restore

        static Waifu2X()
        {
            // Use custom Dll import resolver
            NativeLibrary.SetDllImportResolver(Assembly.GetExecutingAssembly(), DllImportResolver);
        }

        private static IntPtr DllImportResolver(string libraryName, Assembly assembly, DllImportSearchPath? searchPath)
        {
            appDirPath ??= LauncherConfig.AppFolder;

            if (libraryName.EndsWith(DllName, StringComparison.OrdinalIgnoreCase))
            {
                string dllPath = Path.Combine(appDirPath, DllName);
                return LoadInternal(libraryName, assembly, null);
            }

            return LoadInternal(libraryName, assembly, searchPath);
        }

        private static IntPtr LoadInternal(string path, Assembly assembly, DllImportSearchPath? searchPath)
        {
            bool isLoadSuccessful = NativeLibrary.TryLoad(path, assembly, null, out IntPtr pResult);
            if (!isLoadSuccessful || pResult == IntPtr.Zero)
                throw new FileLoadException($"Failed while loading library from this path: {path} with Search Path: {searchPath}\r\nMake sure that the library/.dll is a valid Win32 library and not corrupted!");

            return pResult;
        }

        #region DllImports
        [DllImport(DllName, ExactSpelling = true)]
        private static extern IntPtr waifu2x_create(int gpuId = 0, bool ttaMode = false, int numThreads = 0);

        [DllImport(DllName, ExactSpelling = true)]
        private static extern void waifu2x_destroy(IntPtr context);

        [DllImport(DllName, ExactSpelling = true)]
        private static extern unsafe int waifu2x_load(IntPtr context, byte* param, byte* model);

        [DllImport(DllName, ExactSpelling = true)]
        private static extern unsafe int waifu2x_process(IntPtr context, int w, int h, int c, byte* inData, byte* outData);

        [DllImport(DllName, ExactSpelling = true)]
        private static extern unsafe int waifu2x_process_cpu(IntPtr context, int w, int h, int c, byte* inData, byte* outData);

        [DllImport(DllName, ExactSpelling = true)]
        private static extern void waifu2x_set_param(IntPtr context, Param param, int value);

        [DllImport(DllName, ExactSpelling = true)]
        private static extern int waifu2x_get_param(IntPtr context, Param param);

        [DllImport(DllName, ExactSpelling = true)]
        private static extern Waifu2XStatus waifu2x_self_test(IntPtr context);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
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
            D3DMappingLayers,

            Error,
            NotAvailable = Error,
            TestNotPassed,
            NotInitialized,
        }
        #endregion

        #region Properties
        private IntPtr _context;
        private byte[] _paramBuffer;
        private byte[] _modelBuffer;
        private Waifu2XStatus _status;
        #endregion

        public Waifu2XStatus Status => _status;

        #region Main Methods
        public Waifu2X()
        {
            try
            {
                var gpuId = -1; // Do not touch Vulkan before VulkanTest.
                _status = VulkanTest();
                if (_status == Waifu2XStatus.Ok)
                    gpuId = Ncnn.DefaultGpuIndex;
                _context = waifu2x_create(gpuId);
                Logger.LogWriteLine($"Waifu2X initialized successfully with device: {Ncnn.GetGpuName(gpuId)}", LogType.Default, true);
            }
            catch ( DllNotFoundException )
            {
                _status = Waifu2XStatus.NotAvailable;
                Logger.LogWriteLine("Dll file \"waifu2x-ncnn-vulkan.dll\" can not be found. Waifu2X feature will be disabled.", LogType.Error, true);
            }
            catch ( Exception ex )
            {
                _status = Waifu2XStatus.Error;
                Logger.LogWriteLine($"There was an error when loading Waifu2X!\r\n{ex}", LogType.Error, true);
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
                return waifu2x_load(_context, pParam, pModel) == 0 && ProcessTest();
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
        public static Waifu2XStatus VulkanTest()
        {
            try
            {
                if (CheckD3DMappingLayersPackageInstalled())
                {
                    Logger.LogWriteLine("D3DMappingLayers package detected. Fallback to CPU mode.", LogType.Warning, true);
                    return Waifu2XStatus.D3DMappingLayers;
                }
                var status = waifu2x_self_test(0);
                switch (status)
                {
                    case Waifu2XStatus.CpuMode:
                        Logger.LogWriteLine("No available Vulkan GPU device was found and CPU mode will be used. This will greatly increase image processing time.", LogType.Warning, true);
                        break;
                    case Waifu2XStatus.NotAvailable:
                        Logger.LogWriteLine("An error occurred while initializing Vulkan. Fallback to CPU mode.", LogType.Warning, true);
                        status = Waifu2XStatus.CpuMode;
                        break;
                    case Waifu2XStatus.Ok:
                        Logger.LogWriteLine("Vulkan test passes and GPU mode can be used.", LogType.Default, true);
                        break;
                    default:
                        Logger.LogWriteLine("Waifu2X: Unknown return value from waifu2x_self_test.", LogType.Error, true);
                        status = Waifu2XStatus.NotAvailable;
                        break;
                }
                return status;
            }
            catch (FileLoadException ex)
            {
                return ReturnAsFailedDllInit(ex);
            }
            catch (DllNotFoundException ex)
            {
                return ReturnAsFailedDllInit(ex);
            }

            Waifu2XStatus ReturnAsFailedDllInit<T>(T ex)
                where T : Exception
            {
                Logger.LogWriteLine($"Cannot load Waifu2X as the library failed to load!\r\n{ex}", LogType.Error, true);
                return Waifu2XStatus.Error;
            }
        }

        private bool ProcessTest()
        {
            if (Status < Waifu2XStatus.Error)
            {
                _status = waifu2x_self_test(_context);
                switch (_status)
                {
                    case Waifu2XStatus.TestNotPassed:
                        Logger.LogWriteLine("Waifu2X self-test failed, got an empty output image.", LogType.Error, true);
                        break;
                    case Waifu2XStatus.NotAvailable:
                        Logger.LogWriteLine("An error occurred while processing the image.", LogType.Error, true);
                        break;
                    case Waifu2XStatus.Ok:
                        Logger.LogWriteLine("Waifu2X self-test passed, you can use Waifu2X function normally.", LogType.Default, true);
                        break;
                    default:
                        Logger.LogWriteLine("Waifu2X: Unknown return value from waifu2x_self_test.", LogType.Error, true);
                        _status = Waifu2XStatus.NotAvailable;
                        break;
                }
            }
            return _status == Waifu2XStatus.Ok;
        }

        private static bool CheckD3DMappingLayersPackageInstalled()
        {
            const string FAMILY_NAME = "Microsoft.D3DMappingLayers_8wekyb3d8bbwe";
            uint count = 0, bufferLength = 0;
            GetPackagesByPackageFamily(FAMILY_NAME, ref count, 0, ref bufferLength, 0);
            return count != 0;
        }
        #endregion
    }
}
