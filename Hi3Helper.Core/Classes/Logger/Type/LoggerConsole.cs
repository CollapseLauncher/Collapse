using System;
using System.Text;

namespace Hi3Helper
{
    public class LoggerConsole : LoggerBase, ILog
    {
        public LoggerConsole(string folderPath, Encoding encoding) : base(folderPath, encoding) => AllocateConsole();

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

            InvokeProp.AllocConsole();
            InvokeProp.m_consoleHandle = InvokeProp.GetStdHandle(-11);

            if (!InvokeProp.GetConsoleMode(InvokeProp.m_consoleHandle, out uint mode))
            {
                throw new ContextMarshalException("Failed to initialize console mode!");
            }

            if (!InvokeProp.SetConsoleMode(InvokeProp.m_consoleHandle, mode | 12))
            {
                throw new ContextMarshalException($"Failed to set console mode with error code: {InvokeProp.GetLastError()}");
            }
        }
        #endregion
    }
}
