using System;
using System.Text;

namespace Hi3Helper
{
    public interface ILog : IDisposable
    {
        /// <summary>
        /// Print a blank line to the console.
        /// </summary>
        void LogWriteLine();

        /// <summary>
        /// Print log to the console.
        /// </summary>
        /// <param name="line">The line to print into console or write into the log file</param>
        void LogWriteLine(string line);

        /// <summary>
        /// Print log to the console.
        /// </summary>
        /// <param name="line">The line to print into console or write into the log file</param>
        /// <param name="type">Type of the log line</param>
        /// <param name="writeToLog">Write the log line into the log file</param>
        // ReSharper disable MethodOverloadWithOptionalParameter
        void LogWriteLine(string line = null, LogType type = LogType.Default, bool writeToLog = false);
        // ReSharper restore MethodOverloadWithOptionalParameter

        /// <summary>
        /// Print log to the console but without making a new line.
        /// </summary>
        /// <param name="line">The line to print into console or write into the log file</param>
        /// <param name="type">Type of the log line</param>
        /// <param name="writeToLog">Write the log line into the log file</param>
        /// <param name="resetLinePosition">Reset the current line position to the beginning</param>
        void LogWrite(string line = null, LogType type = LogType.Default, bool writeToLog = false, bool resetLinePosition = false);

        /// <summary>
        /// Write the line into the log file.
        /// </summary>
        /// <param name="line">The line to write into the log file</param>
        /// <param name="type">Type of the log line</param>
        void WriteLog(string line = null, LogType type = LogType.Default);

        /// <summary>
        /// Set the folder path for storing the logs and initialize the log writer
        /// </summary>
        /// <param name="folderPath">The path of the log folder</param>
        /// <param name="logEncoding">Encoding for the log</param>
        void SetFolderPathAndInitialize(string folderPath, Encoding logEncoding);

#nullable enable
        /// <summary>
        /// Reset and clean up all the old log files in the respective folder and
        /// reload the log writer.
        /// </summary>
        /// <param name="reloadToPath">The path of the logs to be cleaned up and reloaded</param>
        /// <param name="encoding">The encoding of the log writer (Default is <see cref="Encoding.UTF8"/> if null)</param>
        void ResetLogFiles(string? reloadToPath, Encoding? encoding = null);
    }
}
