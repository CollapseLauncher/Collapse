using System;
using System.Runtime.InteropServices;
using System.Text;

namespace Hi3Helper
{
    public class LoggerConsole : LoggerBase, ILog
    {
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

        public override async void LogWriteLine(string line) => LogWriteLine(line, LogType.Default);

        public override async void LogWriteLine(string line, LogType type)
        {
            // If the line is null, then print a new line.
            if (line == null)
            {
                Console.WriteLine();
                return;
            }

            // Decorate the line
            line = GetLine(line, type, true);
            Console.WriteLine(line);
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

            line = GetLine(line, type, true);
            Console.Write(line);

            if (writeToLog) WriteLog(line, type);
        }
#pragma warning restore CS1998 // Async method lacks 'await' operators and will run synchronously
        #endregion

        #region StaticMethods
        public static void DisposeConsole()
        {
            if (InvokeProp.m_consoleHandle != IntPtr.Zero)
            {
                IntPtr consoleWindow = InvokeProp.GetConsoleWindow();
                InvokeProp.ShowWindow(consoleWindow, 0);
            }
        }

        public static void AllocateConsole()
        {
            if (InvokeProp.m_consoleHandle != IntPtr.Zero)
            {
                IntPtr consoleWindow = InvokeProp.GetConsoleWindow();
                InvokeProp.ShowWindow(consoleWindow, 5);
                return;
            }

            if (!InvokeProp.AllocConsole())
            {
                throw new ContextMarshalException($"Failed to allocate console with error code: {Marshal.GetLastPInvokeError()}");
            }

            const uint GENERIC_READ = 0x80000000;
            const uint GENERIC_WRITE = 0x40000000;
            const uint FILE_SHARE_WRITE = 2;
            const uint OPEN_EXISTING = 3;
            InvokeProp.m_consoleHandle = InvokeProp.CreateFile("CONOUT$", GENERIC_READ | GENERIC_WRITE, FILE_SHARE_WRITE, 0, OPEN_EXISTING, 0, 0);

            const int STD_OUTPUT_HANDLE = -11;
            InvokeProp.SetStdHandle(STD_OUTPUT_HANDLE, InvokeProp.m_consoleHandle);

            Console.OutputEncoding = Encoding.UTF8;
            Console.Title = "Collapse Console";

            if (!InvokeProp.GetConsoleMode(InvokeProp.m_consoleHandle, out uint mode))
            {
                throw new ContextMarshalException($"Failed to get console mode with error code: {Marshal.GetLastPInvokeError()}");
            }

            const uint ENABLE_VIRTUAL_TERMINAL_PROCESSING = 4;
            const uint DISABLE_NEWLINE_AUTO_RETURN = 8;
            if (!InvokeProp.SetConsoleMode(InvokeProp.m_consoleHandle, mode | ENABLE_VIRTUAL_TERMINAL_PROCESSING | DISABLE_NEWLINE_AUTO_RETURN))
            {
                throw new ContextMarshalException($"Failed to set console mode with error code: {Marshal.GetLastPInvokeError()}");
            }
        }
        #endregion
    }
}
