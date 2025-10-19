using Hi3Helper.Win32.ManagedTools;
using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Buffers;
using System.Collections.Generic;
using System.Linq;
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
    protected const          string   DateTimeFormat = "HH:mm:ss.fff";
    protected readonly       Encoding Encoding;
    internal static readonly Lock     LockObject = new();

    /// <summary>
    /// NOTE FOR COLLAPSE DEVELOPER:<br/>
    /// Edit this dictionary to map the color of the log type tag.<br/>
    /// If the enum isn't defined, then it will use the default (non-colored) tag.
    /// </summary>
    private static readonly Dictionary<LogType, string> ConsoleColorMap = new()
    {
        { LogType.Info, "\e[32;1m" },
        { LogType.Warning, "\e[33;1m" },
        { LogType.Error, "\e[31;1m" },
        { LogType.Scheme, "\e[34;1m" },
        { LogType.Game, "\e[35;1m" },
        { LogType.Debug, "\e[36;1m" },
        { LogType.GLC, "\e[91;1m" },
        { LogType.Sentry, "\e[42;1m" }
    };

    protected static               ReadOnlySpan<byte> NewLineBytes => "\r\n"u8;
    private static readonly unsafe byte*              NewLineBytesP    = GetSpanPointer(NewLineBytes);
    private static readonly        uint               NewLineBytesPLen = (uint)NewLineBytes.Length;

    protected StreamWriter LogWriterField = StreamWriter.Null;
    public    StreamWriter LogWriter => LogWriterField;
    private   string?      LogFolder { get; set; }

#if !APPLYUPDATE
    public static string? LogPath { get; set; }
#endif

    #region Static Class Constructor
    static unsafe LoggerBase()
    {
        HashSet<int> distinctIndex = [];
        foreach (LogType logType in Enum.GetValues<LogType>())
        {
            distinctIndex.Add((int)logType);
        }

        int maxCharInTag = distinctIndex
                          .Select(x => (LogType)x)
                          .Max(x => x.ToString().Length) + 3;
        string defaultEmptyTag = new string(' ', maxCharInTag);

        List<string> colorCodes = [];
        List<string> tagTypes   = [];
        foreach (int index in distinctIndex)
        {
            LogType type      = (LogType)index;
            string  colorCode = ConsoleColorMap.GetValueOrDefault(type, DefaultEmptyColor);

            colorCodes.Add(colorCode);
            tagTypes.Add(type == LogType.NoTag ? defaultEmptyTag : $"[{type}] ");
        }

        int maxLen     = tagTypes.Max(x => x.Length);
        int elementLen = tagTypes.Count;

        nint[] logTagTypePArray    = new nint[elementLen];
        uint[] logTagTypePLenArray = new uint[elementLen];
        nint[] logColorPArray      = new nint[elementLen];
        uint[] logColorPLenArray   = new uint[elementLen];

        for (int i = 0; i < elementLen; i++)
        {
            byte* allocTag = (byte*)NativeMemory.Alloc((nuint)maxLen);

            // Write tag type
            ReadOnlySpan<char> tagChar    = tagTypes[i].AsSpan();
            int                tagCharLen = tagChar.Length;
            char*              tagCharPtr = (char*)Unsafe.AsPointer(ref MemoryMarshal.GetReference(tagChar));
            new Span<byte>(allocTag, maxLen).Fill((byte)' ');

            logTagTypePArray[i]    = (nint)allocTag;
            logTagTypePLenArray[i] = (uint)maxLen;
            _                      = Encoding.UTF8.GetBytes(tagCharPtr, tagCharLen, allocTag, maxLen);

            // Write color code
            ReadOnlySpan<char> colorChar    = colorCodes[i];
            int                colorCharLen = colorChar.Length;
            char*              colorCharPtr = (char*)Unsafe.AsPointer(ref MemoryMarshal.GetReference(colorChar));
            byte*              allocColor   = (byte*)NativeMemory.Alloc((nuint)colorCharLen);

            logColorPArray[i]    = (nint)allocColor;
            logColorPLenArray[i] = (uint)Encoding.UTF8.GetBytes(colorCharPtr, colorCharLen, allocColor, colorCharLen);
        }

        // Alloc and pin array to GC
        GCHandle logColorPArrayGc      = GCHandle.Alloc(logColorPArray,      GCHandleType.Pinned);
        GCHandle logColorPLenArrayGc   = GCHandle.Alloc(logColorPLenArray,   GCHandleType.Pinned);
        GCHandle logTagTypePArrayGc    = GCHandle.Alloc(logTagTypePArray,    GCHandleType.Pinned);
        GCHandle logTagTypePLenArrayGc = GCHandle.Alloc(logTagTypePLenArray, GCHandleType.Pinned);

        LogColorP    = (byte**)logColorPArrayGc.AddrOfPinnedObject();
        LogColorPLen = (uint*)logColorPLenArrayGc.AddrOfPinnedObject();

        LogTagTypeP    = (byte**)logTagTypePArrayGc.AddrOfPinnedObject();
        LogTagTypePLen = (uint*)logTagTypePLenArrayGc.AddrOfPinnedObject();

        // Create empty color code
        int   colorEmptyLen = DefaultEmptyColor.Length;
        char* colorEmptyPtr = (char*)Unsafe.AsPointer(ref MemoryMarshal.GetReference(DefaultEmptyColor.AsSpan()));

        LogColorEmptyP    = (byte*)NativeMemory.Alloc((nuint)DefaultEmptyColor.Length);
        LogColorEmptyPLen = (uint)Encoding.UTF8.GetBytes(colorEmptyPtr, colorEmptyLen, LogColorEmptyP, colorEmptyLen);

        // Create no tag no timestamp
        int noTagNoTimestampLen = maxCharInTag + DateTimeFormat.Length + 2;
        NoTagNoTimestampPaddingP    = (byte*)NativeMemory.Alloc((nuint)noTagNoTimestampLen);
        NoTagNoTimestampPaddingPLen = (uint)noTagNoTimestampLen;
        new Span<byte>(NoTagNoTimestampPaddingP, noTagNoTimestampLen).Fill((byte)' ');
    }
    #endregion

    #region Auto-generated Properties (by Static Class Constructor)
    private const                  string DefaultEmptyColor = "\e[0m";
    private static readonly unsafe byte*  NoTagNoTimestampPaddingP;
    private static readonly        uint   NoTagNoTimestampPaddingPLen;

    private static readonly unsafe byte** LogColorP;
    private static readonly unsafe uint*  LogColorPLen;
    private static readonly unsafe byte*  LogColorEmptyP;
    private static readonly        uint   LogColorEmptyPLen;

    private static readonly unsafe byte** LogTagTypeP;
    private static readonly unsafe uint*  LogTagTypePLen;
    #endregion

    #region De/Constructors
    ~LoggerBase()
    {
        LogWriterField.Flush();
    }

    protected LoggerBase()
    {
        Encoding = Encoding.UTF8;
    }

    protected LoggerBase(string logFolder, Encoding? logEncoding)
    {
        Encoding = logEncoding ?? Encoding.UTF8;

        // Initialize the writer
        SetFolderPathAndInitialize(logFolder, logEncoding);
    }
    #endregion

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

        // Reset state
        DisposeCore(true);

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
            DisposeCore(true);

            if (!string.IsNullOrEmpty(LogFolder) && Directory.Exists(LogFolder))
            {
                DeleteLogFilesInner(LogFolder);
            }

            if (!string.IsNullOrEmpty(reloadToPath) && !Directory.Exists(reloadToPath))
            {
                Directory.CreateDirectory(reloadToPath);
            }

            if (!string.IsNullOrEmpty(reloadToPath))
            {
                LogFolder = reloadToPath;
            }

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
            LogWriterField = new StreamWriter(fileStream, logEncoding ?? Encoding.UTF8, 16 << 10, false);
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

    protected virtual void DisposeCore(bool onlyReset = false)
    {
        LogWriter.Dispose(); // Automatically dispose the FileStream inside
        Interlocked.Exchange(ref LogWriterField, StreamWriter.Null);
    }
    #endregion

    #region Util Methods
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected static unsafe uint CopyToBuffer(void* destination, void* source, uint len)
    {
        Unsafe.CopyBlock(destination, source, len);
        return len;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected static unsafe T* GetSpanPointer<T>(ReadOnlySpan<T> span)
        where T : unmanaged => (T*)Unsafe.AsPointer(ref MemoryMarshal.GetReference(span));

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "get_Text")]
    protected static extern ReadOnlySpan<char> GetInterpolateStringSpan(ref DefaultInterpolatedStringHandler element);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "Clear")]
    protected static extern void ClearInterpolateString(ref DefaultInterpolatedStringHandler element);
    #endregion

    #region Logging Methods
    private readonly ArrayPool<byte> _logBufferPool = ArrayPool<byte>.Create();
    private          byte[]          _buffer        = [];

    protected bool IsBufferBudgetSufficient(int requestedSize)
        => _buffer.Length >= requestedSize;

    protected void ResizeBuffer(int requestedSize)
    {
        if (_buffer.Length == 0)
        {
            _buffer = _logBufferPool.Rent(requestedSize);
            return;
        }

        _logBufferPool.Return(_buffer);
        _buffer = _logBufferPool.Rent(requestedSize);
    }

    protected unsafe void WriteLineToStreamCore(Stream             stream,
                                                ReadOnlySpan<char> line,
                                                LogType            type             = LogType.Info,
                                                bool               appendNewLine    = true,
                                                bool               isWriteColor     = true,
                                                bool               isWriteTagType   = true,
                                                bool               isWriteTimestamp = false)
    {
        using (LockObject.EnterScope())
        {
            int  lineUtf8Len   = Encoding.GetMaxByteCount(line.Length + 48);
            bool useStackalloc = lineUtf8Len <= 512;
            if (!useStackalloc && !IsBufferBudgetSufficient(lineUtf8Len))
            {
                ResizeBuffer(lineUtf8Len);
            }

            scoped Span<byte> buffer = useStackalloc ? stackalloc byte[lineUtf8Len] : _buffer;

            int len = isWriteTagType ?
                WriteToBufferWithIndentCore(line,
                                            buffer,
                                            type,
                                            appendNewLine,
                                            isWriteColor,
                                            isWriteTagType,
                                            isWriteTimestamp) :
                WriteToBufferCore(line,
                                  buffer,
                                  type,
                                  appendNewLine,
                                  isWriteColor,
                                  isWriteTagType,
                                  isWriteTimestamp,
                                  false);

            stream.Write(buffer[..len]);
            stream.Flush();
        }
    }

    private static ReadOnlySpan<char> GetCurrentSplitLine(in Range           range,
                                                          ReadOnlySpan<char> line,
                                                          ref LogType        type,
                                                          ref int            iteration)
    {
        ReadOnlySpan<char> currentLine = line[range];

        if (currentLine.Length != 0 &&
            currentLine[^1] == '\r')
        {
            currentLine = currentLine[..^1];
        }

        if (type != LogType.Game ||
            currentLine.Length == 0 ||
            (currentLine[0] != ' ' &&
             currentLine[0] != '\t'))
        {
            return currentLine;
        }

        type = LogType.NoTag;
        ++iteration;

        return currentLine;
    }

    private int WriteToBufferWithIndentCore(ReadOnlySpan<char> line,
                                            Span<byte>         bufferSpan,
                                            LogType            type,
                                            bool               appendNewLine,
                                            bool               isWriteColor,
                                            bool               isWriteTagType,
                                            bool               isWriteTimestamp)
    {
        int written = 0;
        int iteration = 0;

        foreach (Range range in line.SplitAny('\n'))
        {
            ReadOnlySpan<char> currentLine = GetCurrentSplitLine(in range, line, ref type, ref iteration);
            written += WriteToBufferCore(currentLine,
                                         bufferSpan[written..],
                                         type,
                                         appendNewLine,
                                         isWriteColor,
                                         isWriteTagType,
                                         isWriteTimestamp,
                                         iteration > 0 && isWriteTimestamp);

            type = LogType.NoTag;
            iteration++;
        }

        return written;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private unsafe int WriteToBufferCore(ReadOnlySpan<char> line,
                                         Span<byte>         buffer,
                                         LogType            type,
                                         bool               appendNewLine,
                                         bool               isWriteColor,
                                         bool               isWriteTagType,
                                         bool               isWriteTimestamp,
                                         bool               isWriteTimeTagPadding)
    {
        const byte squareBracketOpen  = 0x5B; // [
        const byte squareBracketClose = 0x5D; // ]

        char* lineP        = GetSpanPointer(line);
        byte* bufferP      = GetSpanPointer(buffer);
        byte* bufferPStart = bufferP;

        int   typeIndex    = (int)type;
        byte* tagColorP    = *(LogColorP + typeIndex);
        uint  tagColorPLen = *(LogColorPLen + typeIndex);
        byte* tagTypeP     = *(LogTagTypeP + typeIndex);
        uint  tagTypePLen  = *(LogTagTypePLen + typeIndex);

        if (isWriteTimeTagPadding && isWriteTimestamp)
        {
            bufferP += CopyToBuffer(bufferP, NoTagNoTimestampPaddingP, NoTagNoTimestampPaddingPLen);
        }

        if (!isWriteTimeTagPadding && isWriteTagType)
        {
            if (isWriteTimestamp)
            {
                DateTimeOffset offsetNow = DateTimeOffset.Now;
                *bufferP++ = squareBracketOpen;
                if (offsetNow.TryFormat(new Span<byte>(bufferP, 16), out int dateTimeFormatWritten, DateTimeFormat))
                {
                    bufferP += dateTimeFormatWritten;
                }
                *bufferP++ = squareBracketClose;
            }

            if (isWriteColor)
            {
                bufferP += CopyToBuffer(bufferP, tagColorP,      tagColorPLen);
                bufferP += CopyToBuffer(bufferP, tagTypeP,       tagTypePLen);
                bufferP += CopyToBuffer(bufferP, LogColorEmptyP, LogColorEmptyPLen);
            }
            else
            {
                bufferP += CopyToBuffer(bufferP, tagTypeP, tagTypePLen);
            }
        }

        int bufferSpaceLeft = buffer.Length - (int)(bufferP - bufferPStart);
        bufferP += Encoding.GetBytes(lineP, line.Length, bufferP, bufferSpaceLeft);
        if (appendNewLine)
        {
            bufferP += CopyToBuffer(bufferP, NewLineBytesP, NewLineBytesPLen);
        }

        return (int)(bufferP - bufferPStart);
    }


    protected async Task WriteLineToStreamCoreAsync(Stream            stream,
                                                    string            line,
                                                    LogType           type             = LogType.Info,
                                                    bool              appendNewLine    = true,
                                                    bool              isWriteColor     = true,
                                                    bool              isWriteTagType   = true,
                                                    bool              isWriteTimestamp = false,
                                                    CancellationToken token            = default)
    {
        int    lineUtf8Len = Encoding.GetMaxByteCount(line.Length);
        byte[] buffer      = _logBufferPool.Rent(lineUtf8Len);

        try
        {
            int len = WriteToBufferCore(line,
                                        buffer,
                                        type,
                                        appendNewLine,
                                        isWriteColor,
                                        isWriteTagType,
                                        isWriteTimestamp,
                                        false);
            await stream.WriteAsync(buffer.AsMemory(0, len), token);
        }
        finally
        {
            await stream.FlushAsync(token);
            _logBufferPool.Return(buffer);
        }
    }

    public virtual void LogWriteLine() { }

    public virtual void LogWriteLine(ReadOnlySpan<char> line,
                                     LogType            type                    = LogType.Info,
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

    public virtual void LogWriteLine(ref DefaultInterpolatedStringHandler interpolatedLine,
                                     LogType                              type                    = LogType.Info,
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

    public virtual void LogWrite(ReadOnlySpan<char> line,
                                 LogType            type                    = LogType.Info,
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

    public virtual void LogWrite(ref DefaultInterpolatedStringHandler interpolatedLine,
                                 LogType                              type                    = LogType.Info,
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

    public virtual Task LogWriteLineAsync(CancellationToken token = default)
        => Task.CompletedTask;

    public virtual Task LogWriteLineAsync(string            line,
                                          LogType           type                    = LogType.Info,
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

    public virtual Task LogWriteAsync(string            line,
                                      LogType           type                    = LogType.Info,
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

    public void Dispose()
    {
        DisposeCore();
        GC.SuppressFinalize(this);
    }
    #endregion
}