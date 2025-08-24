using System;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

// ReSharper disable CheckNamespace

#nullable enable
namespace Hi3Helper;

public interface ILog : IDisposable
{
    StreamWriter LogWriter { get; }

    void LogWriteLine();

    void LogWriteLine(ReadOnlySpan<char> line,
                      LogType            type                    = LogType.Default,
                      bool               writeToLogFile          = false,
                      bool               writeTimestampOnLogFile = true);

    void LogWriteLine(ref DefaultInterpolatedStringHandler interpolatedLine,
                      LogType                              type                    = LogType.Default,
                      bool                                 writeToLogFile          = false,
                      bool                                 writeTimestampOnLogFile = true);

    void LogWrite(ReadOnlySpan<char> line,
                  LogType            type                    = LogType.Default,
                  bool               appendNewLine           = false,
                  bool               writeToLogFile          = false,
                  bool               writeTypeTag            = false,
                  bool               writeTimestampOnLogFile = true);

    void LogWrite(ref DefaultInterpolatedStringHandler interpolatedLine,
                  LogType                              type                    = LogType.Default,
                  bool                                 appendNewLine           = false,
                  bool                                 writeToLogFile          = false,
                  bool                                 writeTypeTag            = false,
                  bool                                 writeTimestampOnLogFile = true);

    Task LogWriteLineAsync(CancellationToken token = default);

    Task LogWriteLineAsync(string            line,
                           LogType           type                    = LogType.Default,
                           bool              writeToLogFile          = false,
                           bool              writeTimestampOnLogFile = true,
                           CancellationToken token                   = default);

    Task LogWriteAsync(string            line,
                       LogType           type                    = LogType.Default,
                       bool              appendNewLine           = false,
                       bool              writeToLogFile          = false,
                       bool              writeTypeTag            = false,
                       bool              writeTimestampOnLogFile = true,
                       CancellationToken token                   = default);

    void SetFolderPathAndInitialize(string folderPath, Encoding? logEncoding = null);

    void ResetLogFiles(string reloadToPath, Encoding? encoding = null);
}