using System;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

// ReSharper disable CheckNamespace
namespace Hi3Helper.Log;

public interface ILog : IDisposable
{
    StreamWriter LogWriter { get; }

    void WriteLine(bool writeToLogFile          = false,
                   bool writeTimestampOnLogFile = true);

    void WriteLine(ReadOnlySpan<char> line,
                   LogType            type                    = LogType.Info,
                   bool               writeToLogFile          = false,
                   bool               writeTimestampOnLogFile = true);

    void WriteLine(
        [InterpolatedStringHandlerArgument] ref DefaultInterpolatedStringHandler interpolatedLine,
        LogType                                                                  type                    = LogType.Info,
        bool                                                                     writeToLogFile          = false,
        bool                                                                     writeTimestampOnLogFile = true);

    void Write(ReadOnlySpan<char> line,
               LogType            type                    = LogType.Info,
               bool               appendNewLine           = false,
               bool               writeToLogFile          = false,
               bool               writeTypeTag            = false,
               bool               writeTimestampOnLogFile = true);

    void Write([InterpolatedStringHandlerArgument] ref DefaultInterpolatedStringHandler interpolatedLine,
               LogType                                                                  type = LogType.Info,
               bool                                                                     appendNewLine = false,
               bool                                                                     writeToLogFile = false,
               bool                                                                     writeTypeTag = false,
               bool                                                                     writeTimestampOnLogFile = true);

    ValueTask WriteLineAsync(CancellationToken token = default);

    ValueTask WriteLineAsync(string            line,
                             LogType           type                    = LogType.Info,
                             bool              writeToLogFile          = false,
                             bool              writeTimestampOnLogFile = true,
                             CancellationToken token                   = default);

    ValueTask WriteAsync(string            line,
                         LogType           type                    = LogType.Info,
                         bool              appendNewLine           = false,
                         bool              writeToLogFile          = false,
                         bool              writeTypeTag            = false,
                         bool              writeTimestampOnLogFile = true,
                         CancellationToken token                   = default);

    void SetFolderPathAndInitialize(string folderPath, Encoding? logEncoding = null);

    void ResetLogFiles(string reloadToPath, Encoding? encoding = null);
}