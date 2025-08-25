using Hi3Helper.Win32.ManagedTools;
using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Buffers;
using System.Runtime.InteropServices;

#if !APPLYUPDATE
using Hi3Helper.Shared.Region;
// ReSharper disable CheckNamespace
// ReSharper disable StringLiteralTypo
#endif

#nullable enable
namespace Hi3Helper;

public abstract class LoggerBase : ILog
{
    protected const           string       DateTimeFormat = "HH:mm:ss.fff";
    protected static readonly string       NewLine        = Environment.NewLine;
    protected static readonly Lock         LockObject     = new();
    protected static readonly UTF8Encoding EncodingUtf8   = new UTF8Encoding();

    public  StreamWriter LogWriter { get; set; } = StreamWriter.Null;
    private string?      LogFolder { get; set; }

#if !APPLYUPDATE
    public static string? LogPath { get; set; }
#endif

    ~LoggerBase()
    {
        LogWriter.BaseStream.Flush();
    }

    protected LoggerBase()
    {
    }

    protected LoggerBase(string logFolder, Encoding? logEncoding)
    {
        // Initialize the writer
        SetFolderPathAndInitialize(logFolder, logEncoding);
    }

    #region Public Methods
    public void SetFolderPathAndInitialize(string folderPath, Encoding? logEncoding)
    {
        // Set the folder path of the stored log
        LogFolder = folderPath;

    #if !APPLYUPDATE
        // Check if the directory exist. If not, then create.
        if (!string.IsNullOrEmpty(LogFolder) && !Directory.Exists(LogFolder))
        {
            Directory.CreateDirectory(LogFolder);
        }

        try
        {
            // Initialize writer and the path of the log file.
            InitializeWriter(false, logEncoding);
        }
        catch (Exception ex)
        {
            SentryHelper.SentryHelper.ExceptionHandler(ex);
            // If the initialization above fails, then use fallback.
            InitializeWriter(true, logEncoding);
        }
    #endif
    }

    public void ResetLogFiles(string? reloadToPath, Encoding? encoding = null)
    {
        using (LockObject.EnterScope())
        {
            DisposeBase();

            if (!string.IsNullOrEmpty(LogFolder) && Directory.Exists(LogFolder))
                DeleteLogFilesInner(LogFolder);

            if (!string.IsNullOrEmpty(reloadToPath) && !Directory.Exists(reloadToPath))
                Directory.CreateDirectory(reloadToPath);

            if (!string.IsNullOrEmpty(reloadToPath))
                LogFolder = reloadToPath;

            encoding ??= Encoding.UTF8;

            SetFolderPathAndInitialize(LogFolder ?? "", encoding);
        }
    }
    #endregion

    #region Private Methods
#if !APPLYUPDATE
    private void InitializeWriter(bool isFallback, Encoding? logEncoding)
    {
        using (LockObject.EnterScope())
        {
            DisposeBase();
            DateTime dateTimeNow = DateTime.Now;

            // Initialize _logPath and get fallback string at the end of the filename if true or none if false.
            string fallbackString = isFallback ? "-f" + Path.GetFileNameWithoutExtension(Path.GetTempFileName()) : string.Empty;
            string dateString = dateTimeNow.ToString("yyyy-MM-dd");

            // Append the build name
            fallbackString += LauncherConfig.IsPreview ? "-pre" : "-sta";
            fallbackString += LauncherConfig.AppCurrentVersionString;

            // Append the current instance number
            int numOfInstance = ProcessChecker.EnumerateInstances(ILoggerHelper.GetILogger());
            fallbackString += $"-id{numOfInstance}";
            LogPath = Path.Combine(LogFolder ?? "", $"log-{dateString + fallbackString}-{dateTimeNow:HH-mm-ss}.log");
            Console.WriteLine("\e[37;44m[LOGGER]\e[0m Log will be written to: " + LogPath);

            // Initialize _logWriter to the given _logPath.
            // The FileShare.ReadWrite is still being used to avoid potential conflict if the launcher needs
            // to warm-restart itself in rare occasion (like update mechanism with Squirrel).
            FileStream fileStream = new FileStream(LogPath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.ReadWrite);
            // Seek the file to the EOF
            fileStream.Seek(0, SeekOrigin.End);

            // Initialize the StreamWriter
            LogWriter = new StreamWriter(fileStream, logEncoding ?? Encoding.UTF8, 16 << 10, false);
        }
    }

    private static void DeleteLogFilesInner(string folderPath)
    {
        DirectoryInfo dirInfo = new DirectoryInfo(folderPath);
        foreach (FileInfo fileInfo in dirInfo.EnumerateFiles("log-*-id*.log", SearchOption.TopDirectoryOnly))
        {
            try
            {
                fileInfo.Delete();
                Logger.LogWriteLine($"Removed log file: {fileInfo.FullName}");
            }
            catch (Exception ex)
            {
                SentryHelper.SentryHelper.ExceptionHandler(ex, SentryHelper.SentryHelper.ExceptionType.UnhandledOther);
                Logger.LogWriteLine($"Cannot remove log file: {fileInfo.FullName}\r\n{ex}", LogType.Error);
            }
        }
    }
#endif
    #endregion

    protected void DisposeBase()
    {
        LogWriter.Dispose(); // Automatically dispose the FileStream inside
    }

    #region Util Methods
    private static ReadOnlySpan<byte> NoTagNoTimestampPadding => "                        "u8;

    private static ReadOnlySpan<byte> LogColorDefault => "\e[32;1m"u8;
    private static ReadOnlySpan<byte> LogColorError   => "\e[31;1m"u8;
    private static ReadOnlySpan<byte> LogColorWarning => "\e[33;1m"u8;
    private static ReadOnlySpan<byte> LogColorScheme  => "\e[34;1m"u8;
    private static ReadOnlySpan<byte> LogColorGame    => "\e[35;1m"u8;
    private static ReadOnlySpan<byte> LogColorDebug   => "\e[36;1m"u8;
    private static ReadOnlySpan<byte> LogColorGLC     => "\e[91;1m"u8;
    private static ReadOnlySpan<byte> LogColorSentry  => "\e[42;1m"u8;
    private static ReadOnlySpan<byte> LogColorEmpty   => "\e[0m"u8;

    private static ReadOnlySpan<byte> LogTagTypeDefault => "[Info]    "u8;
    private static ReadOnlySpan<byte> LogTagTypeError   => "[Error]   "u8;
    private static ReadOnlySpan<byte> LogTagTypeWarning => "[Warning] "u8;
    private static ReadOnlySpan<byte> LogTagTypeScheme  => "[Scheme]  "u8;
    private static ReadOnlySpan<byte> LogTagTypeGame    => "[Game]    "u8;
    private static ReadOnlySpan<byte> LogTagTypeDebug   => "[Debug]   "u8;
    private static ReadOnlySpan<byte> LogTagTypeGLC     => "[GLC Cmd] "u8;
    private static ReadOnlySpan<byte> LogTagTypeSentry  => "[Sentry]  "u8;
    private static ReadOnlySpan<byte> LogTagTypeEmpty   => "          "u8;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected static int CopyToBuffer(Span<byte> utf8CharBuffer, ReadOnlySpan<byte> utf8Str)
    {
        ref byte utf8CharBufferRef = ref MemoryMarshal.GetReference(utf8CharBuffer);
        ref byte utf8StrRef        = ref MemoryMarshal.GetReference(utf8Str);

        int len = utf8Str.Length;
        Unsafe.CopyBlock(ref utf8CharBufferRef, ref utf8StrRef, (uint)len);
        return len;
    }

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "get_Text")]
    protected static extern ReadOnlySpan<char> GetInterpolateStringSpan(ref DefaultInterpolatedStringHandler element);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "Clear")]
    protected static extern void ClearInterpolateString(ref DefaultInterpolatedStringHandler element);
    #endregion

    #region Logging Methods
    [MethodImpl(MethodImplOptions.NoInlining)]
    protected static void WriteLineToStreamCore(Stream             stream,
                                                ReadOnlySpan<char> line,
                                                LogType            type             = LogType.Default,
                                                bool               appendNewLine    = true,
                                                bool               isWriteColor     = true,
                                                bool               isWriteTagType   = true,
                                                bool               isWriteTimestamp = false)
    {
        int        lineUtf8Len = line.Length * 2 + NewLine.Length + 48;
        byte[]?    buffer      = lineUtf8Len > 512 ? ArrayPool<byte>.Shared.Rent(lineUtf8Len) : null;
        Span<byte> bufferSpan  = buffer ?? stackalloc byte[lineUtf8Len];

        try
        {
            int len;
            if (isWriteTagType)
            {
                int iteration = 0;
                foreach (Range range in line.SplitAny('\n'))
                {
                    ReadOnlySpan<char> currentLine = line[range];
                    if (currentLine.Length != 0 &&
                        currentLine[^1] == '\r')
                    {
                        currentLine = currentLine[..^1];
                    }

                    if (type == LogType.Game &&
                        currentLine.Length != 0)
                    {
                        if (currentLine[0] == ' ' ||
                            currentLine[0] == '\t')
                        {
                            type = LogType.NoTag;
                            iteration++;
                        }
                    }

                    len = WriteToBufferCore(currentLine,
                                            bufferSpan,
                                            type,
                                            appendNewLine,
                                            isWriteColor,
                                            isWriteTagType,
                                            isWriteTimestamp,
                                            iteration > 0 && isWriteTimestamp);
                    stream.Write(bufferSpan[..len]);
                    type = LogType.NoTag;
                    ++iteration;
                }

                return;
            }

            len = WriteToBufferCore(line,
                                    bufferSpan,
                                    type,
                                    appendNewLine,
                                    isWriteColor,
                                    isWriteTagType,
                                    isWriteTimestamp);
            stream.Write(bufferSpan[..len]);
        }
        finally
        {
            stream.Flush();
            if (buffer != null)
            {
                ArrayPool<byte>.Shared.Return(buffer);
            }
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    protected static async Task WriteLineToStreamCoreAsync(Stream            stream,
                                                           string            line,
                                                           LogType           type             = LogType.Default,
                                                           bool              appendNewLine    = true,
                                                           bool              isWriteColor     = true,
                                                           bool              isWriteTagType   = true,
                                                           bool              isWriteTimestamp = false,
                                                           CancellationToken token            = default)
    {
        int    lineUtf8Len = Math.Max(line.Length * 2 + NewLine.Length + 48, 512);
        byte[] buffer      = ArrayPool<byte>.Shared.Rent(lineUtf8Len);

        try
        {
            int len = WriteToBufferCore(line, buffer, type, appendNewLine, isWriteColor, isWriteTagType, isWriteTimestamp);
            await stream.WriteAsync(buffer.AsMemory(0, len), token);
        }
        finally
        {
            await stream.FlushAsync(token);
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static int WriteToBufferCore(ReadOnlySpan<char> line,
                                         Span<byte>         buffer,
                                         LogType            type,
                                         bool               appendNewLine,
                                         bool               isWriteColor,
                                         bool               isWriteTagType,
                                         bool               isWriteTimestamp,
                                         bool               isWriteTimeTagPadding = false)
    {
        const byte squareBracketOpen  = 0x5B; // [
        const byte squareBracketClose = 0x5D; // ]

        int len = 0;

        if (isWriteTimeTagPadding && isWriteTimestamp)
        {
            len += CopyToBuffer(buffer[len..], NoTagNoTimestampPadding);
        }

        if (!isWriteTimeTagPadding && isWriteTagType)
        {
            if (isWriteTimestamp)
            {
                DateTimeOffset offsetNow = DateTimeOffset.Now;
                buffer[len++] = squareBracketOpen;
                if (offsetNow.TryFormat(buffer[len..], out int dateTimeFormatWritten, DateTimeFormat))
                {
                    len += dateTimeFormatWritten;
                }
                buffer[len++] = squareBracketClose;
            }

            len += isWriteColor ?
                CopyToBuffer(buffer[len..],
                             type switch
                             {
                                 LogType.Default => LogColorDefault,
                                 LogType.Error => LogColorError,
                                 LogType.Warning => LogColorWarning,
                                 LogType.Scheme => LogColorScheme,
                                 LogType.Game => LogColorGame,
                                 LogType.Debug => LogColorDebug,
                                 LogType.GLC => LogColorGLC,
                                 LogType.Sentry => LogColorSentry,
                                 _ => LogColorEmpty
                             }) :
                0;

            len += CopyToBuffer(buffer[len..],
                                type switch
                                {
                                    LogType.Default => LogTagTypeDefault,
                                    LogType.Error => LogTagTypeError,
                                    LogType.Warning => LogTagTypeWarning,
                                    LogType.Scheme => LogTagTypeScheme,
                                    LogType.Game => LogTagTypeGame,
                                    LogType.Debug => LogTagTypeDebug,
                                    LogType.GLC => LogTagTypeGLC,
                                    LogType.Sentry => LogTagTypeSentry,
                                    LogType.NoTag => LogTagTypeEmpty,
                                    _ => throw new ArgumentException("Type must be a defined value of LogType!")
                                });

            if (isWriteColor)
            {
                len += CopyToBuffer(buffer[len..], LogColorEmpty);
            }
        }

        len += EncodingUtf8.GetBytes(line, buffer[len..]);
        if (appendNewLine)
        {
            len += EncodingUtf8.GetBytes(NewLine, buffer[len..]);
        }

        return len;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public virtual void LogWriteLine() { }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public virtual void LogWriteLine(ReadOnlySpan<char> line,
                                     LogType            type                    = LogType.Default,
                                     bool               writeToLogFile          = false,
                                     bool               writeTimestampOnLogFile = true)
    {
        if (!writeToLogFile)
        {
            return;
        }

        WriteLineToStreamCore(LogWriter.BaseStream,
                              line,
                              type,
                              isWriteColor: false,
                              isWriteTimestamp: writeTimestampOnLogFile);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public virtual void LogWriteLine(ref DefaultInterpolatedStringHandler interpolatedLine,
                                     LogType                              type                    = LogType.Default,
                                     bool                                 writeToLogFile          = false,
                                     bool                                 writeTimestampOnLogFile = true)
    {
        ReadOnlySpan<char> line = GetInterpolateStringSpan(ref interpolatedLine);

        try
        {
            if (!writeToLogFile)
            {
                return;
            }

            LogWriteLine(line, type, writeToLogFile, writeTimestampOnLogFile);
        }
        finally
        {
            ClearInterpolateString(ref interpolatedLine);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public virtual void LogWrite(ReadOnlySpan<char> line,
                                 LogType            type                    = LogType.Default,
                                 bool               appendNewLine           = false,
                                 bool               writeToLogFile          = false,
                                 bool               writeTypeTag            = false,
                                 bool               writeTimestampOnLogFile = true)
    {
        if (!writeToLogFile)
        {
            return;
        }

        WriteLineToStreamCore(LogWriter.BaseStream,
                              line,
                              type,
                              appendNewLine: appendNewLine,
                              isWriteColor: false,
                              isWriteTimestamp: writeTimestampOnLogFile,
                              isWriteTagType: writeTimestampOnLogFile);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public virtual void LogWrite(ref DefaultInterpolatedStringHandler interpolatedLine,
                                 LogType                              type                    = LogType.Default,
                                 bool                                 appendNewLine           = false,
                                 bool                                 writeToLogFile          = false,
                                 bool                                 writeTypeTag            = false,
                                 bool                                 writeTimestampOnLogFile = true)
    {

        ReadOnlySpan<char> line = GetInterpolateStringSpan(ref interpolatedLine);

        try
        {
            if (!writeToLogFile)
            {
                return;
            }

            LogWrite(line, type, appendNewLine, writeToLogFile, writeTypeTag, writeTimestampOnLogFile);
        }
        finally
        {
            ClearInterpolateString(ref interpolatedLine);
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    public virtual Task LogWriteLineAsync(CancellationToken token = default)
        => Task.CompletedTask;

    [MethodImpl(MethodImplOptions.NoInlining)]
    public virtual Task LogWriteLineAsync(string            line,
                                          LogType           type                    = LogType.Default,
                                          bool              writeToLogFile          = false,
                                          bool              writeTimestampOnLogFile = true,
                                          CancellationToken token                   = default)
        => !writeToLogFile ?
            Task.CompletedTask :
            WriteLineToStreamCoreAsync(LogWriter.BaseStream,
                                       line,
                                       type,
                                       isWriteColor: false,
                                       isWriteTimestamp: writeTimestampOnLogFile,
                                       token: token);

    [MethodImpl(MethodImplOptions.NoInlining)]
    public virtual Task LogWriteAsync(string            line,
                                      LogType           type                    = LogType.Default,
                                      bool              appendNewLine           = false,
                                      bool              writeToLogFile          = false,
                                      bool              writeTypeTag            = false,
                                      bool              writeTimestampOnLogFile = true,
                                      CancellationToken token                   = default)
        => !writeToLogFile ?
            Task.CompletedTask :
            WriteLineToStreamCoreAsync(LogWriter.BaseStream,
                                       line,
                                       type,
                                       appendNewLine: appendNewLine,
                                       isWriteColor: false,
                                       isWriteTimestamp: writeTimestampOnLogFile,
                                       isWriteTagType: writeTimestampOnLogFile,
                                       token: token);

    public virtual void Dispose()
    {
        DisposeBase();
        GC.SuppressFinalize(this);
    }
    #endregion
}