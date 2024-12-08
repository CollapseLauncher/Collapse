using Hi3Helper.Win32.Native.LibraryImport;
using Hi3Helper.Win32.Native.ManagedTools;
using System;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using System.Text;
#if !APPLYUPDATE
using static Hi3Helper.Shared.Region.LauncherConfig;
#endif

namespace Hi3Helper
{
    [SuppressMessage("ReSharper", "MethodOverloadWithOptionalParameter")]
    public class LoggerConsole : LoggerBase, ILog
    {
        public static IntPtr ConsoleHandle;
        private static bool _virtualTerminal;

        public LoggerConsole(string folderPath, Encoding encoding) : base(folderPath, encoding)
#if !APPLYUPDATE
            => AllocateConsole();
#else
        { }
#endif

        // Only dispose base on deconstruction.
        ~LoggerConsole() => DisposeBase();

        #region Methods
        public void Dispose()
        {
            // Dispose console and base if requested.
            DisposeConsole();
            DisposeBase();
        }

#pragma warning disable CS1998 // Async method lacks 'await' operators and will run synchronously
        public override async void LogWriteLine() => Console.WriteLine();

        public override async void LogWriteLine(string line = null) => LogWriteLine(line, LogType.Default);

        public override async void LogWriteLine(string line, LogType type)
        {
            // If the line is null, then print a new line.
            if (line == null)
            {
                Console.WriteLine();
                return;
            }

            // Decorate the line
            line = GetLine(line, type, _virtualTerminal, false);

            // Write using new async write line output and use .Error for error type
            if (type == LogType.Error) await Console.Error.WriteLineAsync(line);
            else await Console.Out.WriteLineAsync(line);
        }

        public override async void LogWriteLine(string line, LogType type, bool writeToLog)
        {
            LogWriteLine(line, type);
            if (writeToLog) WriteLog(line, type);
        }

        public override async void LogWrite(string line, LogType type, bool writeToLog, bool resetLinePosition)
        {
            if (resetLinePosition && writeToLog)
            {
                throw new ArgumentException("You can't write to log file while resetLinePosition is true!");
            }

            if (resetLinePosition)
            {
                Console.Write('\r' + line);
                return;
            }

            line = GetLine(line, type, _virtualTerminal, false);
            Console.Write(line);

            if (writeToLog) WriteLog(line, type);
        }
#pragma warning restore CS1998 // Async method lacks 'await' operators and will run synchronously
        #endregion

        #region StaticMethods
        public static void DisposeConsole()
        {
            if (ConsoleHandle != IntPtr.Zero)
            {
                IntPtr consoleWindow = PInvoke.GetConsoleWindow();
                PInvoke.ShowWindow(consoleWindow, 0);
            }
        }

        public static void AllocateConsole()
        {
            if (ConsoleHandle != IntPtr.Zero)
            {
                IntPtr consoleWindow = PInvoke.GetConsoleWindow();
                PInvoke.ShowWindow(consoleWindow, 5);
                return;
            }

            if (!PInvoke.AllocConsole())
            {
                throw new ContextMarshalException($"Failed to allocate console with error code: {Marshal.GetLastPInvokeError()}");
            }

            const uint GENERIC_READ = 0x80000000;
            const uint GENERIC_WRITE = 0x40000000;
            const uint FILE_SHARE_WRITE = 2;
            const uint OPEN_EXISTING = 3;
            ConsoleHandle = PInvoke.CreateFile("CONOUT$", GENERIC_READ | GENERIC_WRITE, FILE_SHARE_WRITE, (uint)0, OPEN_EXISTING, 0, 0);

            const int STD_OUTPUT_HANDLE = -11;
            PInvoke.SetStdHandle(STD_OUTPUT_HANDLE, ConsoleHandle);

            Console.OutputEncoding = Encoding.UTF8;

            var instanceIndicator = "";
            var instanceCount = ProcessChecker.EnumerateInstances(ILoggerHelper.GetILogger());

            if (instanceCount > 1) instanceIndicator = $" - #{instanceCount}";
            Console.Title = $"Collapse Console{instanceIndicator}";

            if (PInvoke.GetConsoleMode(ConsoleHandle, out uint mode))
            {
                const uint ENABLE_PROCESSED_OUTPUT            = 1;
                const uint ENABLE_VIRTUAL_TERMINAL_PROCESSING = 4;
                const uint DISABLE_NEWLINE_AUTO_RETURN        = 8;
                mode |= ENABLE_PROCESSED_OUTPUT | ENABLE_VIRTUAL_TERMINAL_PROCESSING | DISABLE_NEWLINE_AUTO_RETURN;
                if (PInvoke.SetConsoleMode(ConsoleHandle, mode))
                {
                    _virtualTerminal = true;
                }
            }

#if !APPLYUPDATE
            Windowing.SetWindowIcon(PInvoke.GetConsoleWindow(), AppIconLarge, AppIconSmall);
#endif
        }
#endregion
    }
}
