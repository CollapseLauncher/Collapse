/*
 * Credit
 * =============================================================================================
 * Original code by: Thomas Freudenberg ©2003 - 2005
 * https://www.codeproject.com/Articles/4502/RegistryMonitor-a-NET-wrapper-class-for-RegNotifyC
 *
 * This code might have been modified to adjust the use for Collapse project.
 * Hence, the original code is licensed under The Code Project Open License (CPOL) 1.02
 * https://www.codeproject.com/info/cpol10.aspx
 */

using Hi3Helper;
using Hi3Helper.Data;
using Hi3Helper.SentryHelper;
using Hi3Helper.Win32.Native.Enums.Registry;
using Hi3Helper.Win32.Native.LibraryImport;
using Hi3Helper.Win32.Native.Structs;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Threading;
// ReSharper disable CommentTypo
// ReSharper disable PartialTypeWithSinglePart
// ReSharper disable StringLiteralTypo
// ReSharper disable UnusedMember.Global
// ReSharper disable CheckNamespace

#nullable enable
namespace CollapseLauncher.RegistryUtils;

public sealed partial class RegistryMonitor : IDisposable
{
    #region Event handling

    /// <summary>
    /// Occurs when the specified registry key has changed.
    /// </summary>
    public event EventHandler? RegChanged;

    /// <summary>
    /// Occurs when the access to the registry fails.
    /// </summary>
    public event ErrorEventHandler? Error;

    /// <summary>
    /// Raises the <see cref="RegChanged"/> event.
    /// </summary>
    /// <remarks>
    /// <p>
    /// <b>OnRegChanged</b> is called when the specified registry key has changed.
    /// </p>
    /// <note type="inheritinfo">
    /// When overriding <see cref="OnRegChanged"/> in a derived class, be sure to call
    /// the base class's <see cref="OnRegChanged"/> method.
    /// </note>
    /// </remarks>
    private void OnRegChanged() => RegChanged?.Invoke(this, null!);

    /// <summary>
    /// Raises the <see cref="Error"/> event.
    /// </summary>
    /// <param name="e">The <see cref="Exception"/> which occured while watching the registry.</param>
    /// <remarks>
    /// </remarks>
    private void OnError(Exception e)
    {
#if DEBUG
        Logger.LogWriteLine($"[RegistryMonitor] An error has occurred:\r\n" +
                            $"  Hive: {_registryHive}\r\n" +
                            $"  SubKey: {_registrySubKey}" +
                            $"  Error: {e}", LogType.Debug, true);
#endif
        
        SentryHelper.ExceptionHandler(e, SentryHelper.ExceptionType.UnhandledOther);
        Error?.Invoke(this, new ErrorEventArgs(e));
    }

    #endregion

    #region Private member variables

    private const RegChangeNotifyFilter DefaultRegFilter =
        RegChangeNotifyFilter.Key
        | RegChangeNotifyFilter.Attribute
        | RegChangeNotifyFilter.Value
        | RegChangeNotifyFilter.Security;

    private readonly HKEY             _registryHive;
    private readonly string           _registrySubKey;
    private          Thread?          _thread;
    private readonly Lock             _threadLock = new();
    private          bool             _disposed;
    private readonly ManualResetEvent _eventTerminate = new(false);

    #endregion

    /// <summary>
    /// Initializes a new instance of the <see cref="RegistryMonitor"/> class.
    /// </summary>
    /// <param name="registryKey">The registry key to monitor.</param>
    public RegistryMonitor(RegistryKey registryKey) : this(registryKey.Name)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="RegistryMonitor"/> class.
    /// </summary>
    /// <param name="subKeyOrFullKeyPath">
    /// The subpath of the key or full path of the registry key, which include these valid Hive names:<br/>
    /// <code>
    ///   - HKEY_CLASSES_ROOT<br/>
    ///   - HKEY_CURRENT_CONFIG<br/>
    ///   - HKEY_CURRENT_USER<br/>
    ///   - HKEY_LOCAL_MACHINE<br/>
    ///   - HKEY_PERFORMANCE_DATA<br/>
    ///   - HKEY_USERS
    /// </code>
    /// If no valid Hive name is found at the beginning of the <paramref name="subKeyOrFullKeyPath"/> path,
    /// <c>HKEY_CURRENT_USER</c> or the value from <paramref name="registryHive"/> will be used by default.
    /// <br/>
    /// And if <see langword="null"/> is provided, the root key of <c>HKEY_CURRENT_USER</c> or <paramref name="registryHive"/> will be used by default.
    /// </param>
    /// <param name="registryHive">
    /// The registry hive to be used for monitoring. If a valid hive name from <paramref name="subKeyOrFullKeyPath"/> is defined and
    /// both the value from <paramref name="registryHive"/> is defined too, the hive from <paramref name="registryHive"/> will
    /// be used instead.
    /// </param>
    public RegistryMonitor(string? subKeyOrFullKeyPath, HKEY? registryHive = null)
    {
        _registryHive        = registryHive ?? GetRegistryHiveFromName(subKeyOrFullKeyPath) ?? HKEY.HKEY_CURRENT_USER;
        _registrySubKey      = GetRegistrySubKeyFromName(subKeyOrFullKeyPath);
        RegistryNotifyFilter = DefaultRegFilter;

#if DEBUG
        Logger.LogWriteLine($"RegistryMonitor Initialized!\r\n" +
                            $"  Hive: {_registryHive}\r\n" +
                            $"  subKey: {_registrySubKey}", LogType.Debug, true);
#endif
    }

    #region String Utils

    private const string PathSeparators       = @"\/";
    private const string UnderscoreSeparators = "_-";

    private static readonly Dictionary<string, HKEY> AlternateHKeyNames = GenerateAlternateHKeyNames();

    private static readonly Dictionary<string, HKEY>.AlternateLookup<ReadOnlySpan<char>> AlternateHKeyNamesL =
        AlternateHKeyNames.GetAlternateLookup<ReadOnlySpan<char>>();
    
    private static HKEY? GetRegistryHiveFromName(ReadOnlySpan<char> fullName)
    {
        ReadOnlySpan<char> firstSplit = ConverterTool.GetSplit(fullName, 0, PathSeparators);
        return Enum.TryParse(firstSplit, true, out HKEY key) ||
               AlternateHKeyNamesL.TryGetValue(firstSplit, out key) ? key : null;
    }

    private static string GetRegistrySubKeyFromName(ReadOnlySpan<char> fullName)
    {
        if (fullName.IsEmpty)
        {
            return "";
        }
        
        fullName = fullName.Trim(PathSeparators);

        HKEY? keyHive = GetRegistryHiveFromName(fullName);
        ReadOnlySpan<char> firstSplit = ConverterTool.GetSplit(keyHive.HasValue ? fullName : ReadOnlySpan<char>.Empty,
                                                               0,
                                                               PathSeparators);

        if (firstSplit.IsEmpty)
        {
            return fullName.ToString();
        }

        int offset = (int)firstSplit.GetByteOffsetFromSource(fullName) +
                     firstSplit.Length;

        return fullName[offset..].Trim(PathSeparators).ToString();
    }

    private static Dictionary<string, HKEY> GenerateAlternateHKeyNames()
    {
        Dictionary<string, HKEY> returnDict = new(StringComparer.OrdinalIgnoreCase);
        Dictionary<string, HKEY>.AlternateLookup<ReadOnlySpan<char>> returnDictL =
            returnDict.GetAlternateLookup<ReadOnlySpan<char>>();
        Span<char> buffer = stackalloc char[64];

        foreach (HKEY value in Enum.GetValues<HKEY>())
        {
            if (!Enum.TryFormat(value, buffer, out int written))
            {
                continue;
            }

            ReadOnlySpan<char> name         = buffer[..written];
            ReadOnlySpan<char> nameNoPrefix = GetNameWithoutPrefix(name);

            // Add general name with and without prefix
            returnDictL.TryAdd(name,         value);
            returnDictL.TryAdd(nameNoPrefix, value);

            // Add general name with and without prefix for no underscore version
            string nameNoUnderscore         = GetNameNoUnderscore(name);
            string nameNoUnderscoreNoPrefix = GetNameNoUnderscore(nameNoPrefix);

            returnDict.TryAdd(nameNoUnderscore,         value);
            returnDict.TryAdd(nameNoUnderscoreNoPrefix, value);
        }

        return returnDict;

        static string GetNameNoUnderscore(ReadOnlySpan<char> name)
        {
            return string.Create(name.Length, name, Action);

            static void Action(Span<char> span, ReadOnlySpan<char> source)
            {
                int index = 0;
                foreach (char c in source)
                {
                    if (!UnderscoreSeparators.Contains(c))
                    {
                        span[index++] = c;
                    }
                }

                span[index..].Clear();
            }
        }

        static ReadOnlySpan<char> GetNameWithoutPrefix(ReadOnlySpan<char> name)
        {
            const string prefix = "HKEY";

            return name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
                ? name[prefix.Length..].Trim(UnderscoreSeparators) // Slice
                : name;                                            // Use previous span
        }
    }
    
    #endregion

    ~RegistryMonitor() => Dispose();

    /// <summary>
    /// Disposes this object.
    /// </summary>
    public void Dispose()
    {
        try
        {
            Stop();
        }
        catch (Exception ex)
        {
            Logger.LogWriteLine($"Error at stopping RegistryWatcher!\r\n{ex}", LogType.Error, true);
            SentryHelper.ExceptionHandler(ex, SentryHelper.ExceptionType.UnhandledOther);
            throw new Exception($"Error in RegistryMonitor Dispose routine!\r\n{ex}");
        }
        finally
        {
            Interlocked.Exchange(ref _disposed, true);
        }
#if DEBUG
        Logger.LogWriteLine("RegistryMonitor Disposed!", LogType.Debug, true);
#endif
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Gets or sets the <see cref="RegChangeNotifyFilter"/>.
    /// </summary>
    public RegChangeNotifyFilter RegistryNotifyFilter { get; set; }

    /// <summary>
    /// <see langword="true"/> if the monitoring thread is running, otherwise <see langword="false"/>.
    /// </summary>
    private bool IsMonitoring => _thread != null;

    /// <summary>
    /// Start monitoring.
    /// </summary>
    public void Start()
    {
        if (IsMonitoring)
        {
            return;
        }

        if (Volatile.Read(ref _disposed))
            throw new ObjectDisposedException(null, "This instance is already disposed");

        using (_threadLock.EnterScope())
        {
            _eventTerminate.Reset();
            Interlocked.Exchange(ref _thread, new Thread(MonitorThread)
            {
                IsBackground = true
            });
            _thread.Start();
        }
    }

    /// <summary>
    /// Stops the monitoring thread.
    /// </summary>
    public void Stop()
    {
        if (Volatile.Read(ref _disposed) ||
            Volatile.Read(ref _thread) == null)
        {
            return;
        }

        using (_threadLock.EnterScope())
        {
            _eventTerminate.Set();
            _thread?.Join();
        }
    }

    private void MonitorThread()
    {
        try
        {
            ThreadLoop();
        }
        catch (Exception e)
        {
            OnError(e);
        }

        Interlocked.Exchange(ref _thread, null);
    }

    private void ThreadLoop()
    {
        HResult result =
            PInvoke.RegOpenKeyEx(_registryHive,
                                 _registrySubKey,
                                 RegOption.NonVolatile,
                                 RegSAM.Read | RegSAM.QueryValue | RegSAM.Notify,
                                 out nint registryKey);

        Exception? resultEx = result.GetException();
        if (resultEx != null)
        {
            OnError(resultEx);
            return;
        }

        AutoResetEvent eventNotify = new(false);
        try
        {
            WaitHandle[] waitHandles = [eventNotify, _eventTerminate];

            while (!_eventTerminate.WaitOne(0, true))
            {
                if (_disposed) break;

                int resultNotify = PInvoke.RegNotifyChangeKeyValue(registryKey,
                                                                   true,
                                                                   RegistryNotifyFilter,
                                                                   eventNotify.SafeWaitHandle,
                                                                   true);
                if (resultNotify != 0)
                    throw new Win32Exception(resultNotify);

                int waitHandlerAny = WaitHandle.WaitAny(waitHandles);
                if (waitHandlerAny != 0)
                {
                    continue;
                }

#if DEBUG
                Logger.LogWriteLine($"[RegistryMonitor] Found change(s) in registry!\r\n" +
                                    $"  Hive: {_registryHive}\r\n" +
                                    $"  SubKey: {_registrySubKey}", LogType.Debug, true);
#endif
                OnRegChanged();
            }
        }
        finally
        {
            if (registryKey != nint.Zero)
            {
                ((HResult)PInvoke.RegCloseKey(registryKey)).ThrowOnFailure();
            }

            eventNotify.Dispose();
        }
    }
}
