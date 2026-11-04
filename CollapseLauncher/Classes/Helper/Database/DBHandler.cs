using Hi3Helper.Log;
using Hi3Helper.SentryHelper;
using Libsql.Client;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

// ReSharper disable CommentTypo

#nullable enable
namespace CollapseLauncher.Helper.Database;

internal static class DbHandler
{
    #region Config Propertie
    
    public static bool? IsEnabled
    {
        get
        {
            if (field != null) return field;
            field = DbConfig.DbEnabled;
            return field;
        }
        set
        {
            field              = value;
            DbConfig.DbEnabled = value ?? false;

            IsInitialized = false; // Force first init
            if (!(value ?? false)) Dispose(); // Dispose instance if user disabled database function globally
        }
    }
        
        
    public static string? Uri
    {
        get
        {
            if (!string.IsNullOrEmpty(field)) return field;
            field = DbConfig.DbUrl;
            return field;
        }
        set
        {
            if (value != field) IsInitialized = false; // Force first init if value changed
                
            field          = value;
            DbConfig.DbUrl = value;
            IsInitialized   = false;
        }
    }
        
    [DebuggerHidden]
    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    public static string? Token
    {
        get
        {
            if (!string.IsNullOrEmpty(field)) return field;
            field = DbConfig.DbToken;
            return field;
        }
        set
        {
            if (value != field) IsInitialized = false; // Force first init if value changed
                
            field            = value;
            DbConfig.DbToken = value;
            IsInitialized     = false;
        }
    }
        
    //private static string? _userId;
    private static string? _userIdHash;
    public static string? UserId
    {
        get
        {
            if (field != null) return field;
            field = DbConfig.UserGuid; // Get or create (if not yet has one) GUIDv7
            
            _userIdHash = Convert.ToHexStringLower(System.IO.Hashing.XxHash64.Hash(Encoding.ASCII.GetBytes(field))); 
            // Get hash for the UserID to be used as SQL table name
            // I know that this is overkill, but I want it to be totally non-identifiable if for some reason someone
            // has access to their database. It also lowers the amount of query command length to be sent, hopefully
            // reducing access latency.
            // p.s. oh yeah, this is also why user won't be able to get their data back if they lost the GUID,
            // good luck reversing Xxhash64 back to string. Technically possible, but good luck!
            return field;
        }
        set
        {
            if (value != field) IsInitialized = false; // Force first init if value changed
                
            field             = value;
            DbConfig.UserGuid = value;

            if (string.IsNullOrWhiteSpace(value))
            {
                _userIdHash  = null;
                IsInitialized = false;
                return;
            }
            
            var byteUidH = System.IO.Hashing.XxHash64.Hash(Encoding.ASCII.GetBytes(value));
            _userIdHash  = Convert.ToHexStringLower(byteUidH);
            IsInitialized = false;
        }
    }
    
    public static bool IsInitialized { get; private set; } = false;

    #endregion
        
    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    private static IDatabaseClient? _database;

    public static async Task Init(bool redirectThrow = false, bool bypassEnableFlag = false)
    {
        await Task.Factory.StartNew(DbConfig.Init).ConfigureAwait(false);
            
        if (!bypassEnableFlag && !(IsEnabled ?? false))
        {
            Logger.WriteLine("[DbHandler::Init] Database functionality is disabled!");
            return;
        }

        try
        {
            // Init props
            _ = Token;
            _ = Uri;
            _ = UserId;

            if (string.IsNullOrEmpty(Uri))
            {
                IsEnabled = false;
                throw new NullReferenceException($"DB_001 {Locale.Current.Lang?._SettingsPage?.Database_Error_EmptyUri}");
            }
                    
            if (string.IsNullOrEmpty(Token))
            {
                IsEnabled = false;
                throw new NullReferenceException($"DB_002 {Locale.Current.Lang?._SettingsPage?.Database_Error_EmptyToken}");
            }
                    

            // Connect to database
            // Libsql-client-dotnet technically support file based SQLite by pushing `file://` proto in the URL.
            // But what's the point?
            _database = await DatabaseClient.Create(opts =>
                                                    {
                                                        opts.Url       = Uri;
                                                        opts.AuthToken = Token;
                                                    });

            if (!IsInitialized)
            {
                Logger.WriteLine("[DbHandler::Init] Initializing database system...");
                // Ensure table exist at first initialization
                await
                    _database
                       .Execute($"CREATE TABLE IF NOT EXISTS \"uid-{_userIdHash}\" (Id INTEGER PRIMARY KEY AUTOINCREMENT, 'key' TEXT UNIQUE NOT NULL, 'value' TEXT)");

                await
                    _database
                       .Execute($"CREATE TABLE IF NOT EXISTS \"uid-{_userIdHash}-blob\" (Id INTEGER PRIMARY KEY AUTOINCREMENT, 'key' TEXT UNIQUE NOT NULL, 'value' BLOB)");
                IsInitialized = true;
            }
            else Logger.WriteLine("[DbHandler::Init] Reinitializing database system...");
        }
        catch (DllNotFoundException e)
        {
            Logger.WriteLine("[DbHandler::Init] Error when connecting to database system! Probably missing Visual C/C++ redist!\r\n" + e,
                         LogType.Error, true);
            if (redirectThrow) throw;
        }
        // No need to handle all these error catcher with sentry
        // The error should be handled in the method caller instead
        catch (LibsqlException e) when (e.Message.Contains("The JWT is invalid", StringComparison.OrdinalIgnoreCase) 
                                        && !redirectThrow)
        {
            Logger.WriteLine($"[DBHandler::Init] Error when connecting to database system! Token invalid!\r\n{e}",
                         LogType.Error, true);
        }
        catch (Exception e) when (!redirectThrow)
        {
            Logger.WriteLine($"[DBHandler::Init] Error when (re)initializing database system!\r\n{e}", LogType.Error, true);
        }
        catch (LibsqlException e) when (e.Message.Contains("The JWT is invalid", StringComparison.OrdinalIgnoreCase))
        {
            Logger.WriteLine($"[DBHandler::Init] Error when connecting to database system! Token invalid!\r\n{e}",
                         LogType.Error, true);
            var ex = new AggregateException("DB_003 Unauthorized: wrong token inserted", e);
            throw ex;
        }
        catch (Exception e)
        {
            Logger.WriteLine($"[DBHandler::Init] Error when (re)initializing database system!\r\n{e}", LogType.Error, true);
            throw;
        }
    }

    public static async Task TryInit()
    {
        if (string.IsNullOrEmpty(Uri) || string.IsNullOrEmpty(Token))
            return;
        await Init();
    }

    private static void Dispose()
    {
        _database   = null;
        _userIdHash = null;

        Token  = null;
        Uri    = null;
        UserId = null;
    }
    
    private const int MaxAttempts = 5;

    public static async Task<string?> QueryKey(string key, bool redirectThrow = false, bool isBlob = false)
    {
        if (!(IsEnabled ?? false)) return null;
    #if DEBUG
        var r   = new Random();
        var sId = Math.Abs(r.Next(0, 1000).ToString().GetHashCode());
        Logger.WriteLine($"[DBHandler::QueryKey][{sId}] Invoked!\r\n\tKey: {key}", LogType.Debug, true);
        var t = Stopwatch.StartNew();
    #endif
        for (var i = 0; i < MaxAttempts; i++)
        {
            var retVal = await QueryKeyInternal(key, isBlob
                                            #if DEBUG
                                              , sId
                                            #endif
                                               );
            if (retVal.result == 200)
            {
            #if DEBUG
                if (isBlob)
                {
                    Logger.WriteLine($"[DBHandler::QueryKey][{sId}] Got value!\r\n\tKey: {key}\r\n\tIS BLOB",
                                 LogType.Debug, true);
                }
                else
                {
                    Logger.WriteLine($"[DBHandler::QueryKey][{sId}] Got value!\r\n\tKey: {key}\r\n\tValue:\r\n{retVal.returnedValue}",
                                 LogType.Debug, true);
                }
            #endif
                return retVal.returnedValue;
            }

            if (i < MaxAttempts - 1)
            {
                var delayTime = (int)Math.Pow(2, i) * 500; // Exponential backoff delay
                Logger.WriteLine($"[DBHandler::QueryKey] Failed to get value for key {key}, retrying after {delayTime}ms (try {i + 1} of {MaxAttempts})...\r\n\t" +
                             $"Status Code: {retVal.result}", LogType.Error, true);
                
                await Task.Delay(delayTime);
                continue;
            }

            Logger.WriteLine($"[DBHandler::QueryKey] Failed to get value for key {key} after {MaxAttempts} tries!\r\n\t" +
                         $"Status Code: {retVal.result}", LogType.Error, true);

            // Exception handler when exceptionValue is null does not need to be handled by Sentry.
            // Technically, this should never happen, but just in case...
            if (retVal.exceptionValue == null)
                return redirectThrow
                    ? throw new Exception($"DB_005 Failed to get value for key {key} after {MaxAttempts} tries!")
                    : null;
                
            await SentryHelper.ExceptionHandlerAsync(retVal.exceptionValue);
            return redirectThrow
                ? throw new
                    AggregateException($"DB_004 Failed to get value for key {key} after {MaxAttempts} tries!",
                                       retVal.exceptionValue)
                : null;
        }
    #if DEBUG
        t.Stop();
        Logger.WriteLine($"[DBHandler::QueryKey][{sId}] Operation took {t.ElapsedMilliseconds} ms!", LogType.Debug, true);
    #endif

        return null;
    }

    public static async Task StoreKeyValue(string key, string value, bool redirectThrow = false,
                                           bool isBlob = false, byte[]? blobValue = null)
    {
        if (!(IsEnabled ?? false)) return;
    #if DEBUG
        var t   = Stopwatch.StartNew();
        var r   = new Random();
        var sId = Math.Abs(r.Next(0, 1000).ToString().GetHashCode());
        if (isBlob)
        {
            Logger.WriteLine($"[DBHandler::StoreKeyValue][{sId}] Invoked!\r\n\tKey: {key}\r\n\tIS BLOB", LogType.Debug,
                         true);
        }
        else
        {
            Logger.WriteLine($"[DBHandler::StoreKeyValue][{sId}] Invoked!\r\n\tKey: {key}\r\n\tValue: {value}", LogType.Debug,
                         true);
        }
       
    #endif
        for (var i = 0; i < MaxAttempts; i++)
        {
            var retVal = await StoreKeyValueInternal(key, value, isBlob, blobValue);
            if (retVal.result == 200)
            {
            #if DEBUG
                if (isBlob)
                {
                    Logger.WriteLine($"[DBHandler::StoreKeyValue][{sId}] Saved value!\r\n\tKey: {key}\r\n\tIS BLOB",
                                 LogType.Debug, true);
                }
                else
                {
                    Logger.WriteLine($"[DBHandler::StoreKeyValue][{sId}] Saved value!\r\n\tKey: {key}\r\n\tValue: {value}",
                                 LogType.Debug, true);
                }
                
            #endif
                return;
            }

            if (i < MaxAttempts - 1)
            {
                var delayTime = (int)Math.Pow(2, i) * 500; // Exponential backoff delay
                Logger.WriteLine($"[DBHandler::StoreKeyValue] Failed to store value for key {key}, retrying after {delayTime}ms (try {i + 1} of {MaxAttempts})...\r\n\t" +
                             $"Status Code: {retVal.result}", LogType.Error, true);
                await Task.Delay(delayTime);
                continue;
            }
                
            Logger.WriteLine($"[DBHandler::StoreKeyValue] Failed to store value for key {key} after {MaxAttempts} tries!\r\n\t" +
                         $"Status Code: {retVal.result}", LogType.Error, true);

            // Exception handler when exceptionValue is null does not need to be handled by Sentry.
            // Technically, this should never happen, but just in case...
            if (retVal.exceptionValue == null)
            {
                Logger.WriteLine($"[DBHandler::StoreKeyValue] Failed when trying to store key {key}", LogType.Error,
                             true);
                if (redirectThrow) throw new Exception($"DB_005 Failed to get value for key {key} after {MaxAttempts} tries!");
                return;
            }
            
            await SentryHelper.ExceptionHandlerAsync(retVal.exceptionValue);
            Logger.WriteLine($"[DBHandler::StoreKeyValue] Exception value is not null, throwing exception!\r\n{retVal.exceptionValue}", LogType.Error, true);
            if (redirectThrow)
                throw new AggregateException($"DB_004 Failed to store value for key {key} after {MaxAttempts} tries!",
                                             retVal.exceptionValue);
        }
            
    #if DEBUG
        t.Stop();
        Logger.WriteLine($"[DBHandler::StoreKeyValue][{sId}] Operation took {t.ElapsedMilliseconds} ms!",
                     LogType.Debug, true);
    #endif
    }

    #region Private Methods

    private static async Task<(int result, string? returnedValue, Exception? exceptionValue)> QueryKeyInternal(string key, bool isBlob
        #if DEBUG
          , int sId = 0
    #endif
    )
    {
        try
        {
            if (_database == null) await Init(true);
            var tableName = "uid-"  + _userIdHash + (isBlob ? "-blob" : "");
            // Get table row for exact key
            var rs =
                await
                    _database!
                       .Execute($"SELECT value FROM \"{tableName}\" WHERE key = ?", key);
            if (rs == null)
            {
                return (200, null, null);
            }
            
            string str = "";

            if (isBlob)
            {
                var     firstRow = rs.Rows.FirstOrDefault();
                object? rcv      = firstRow?.FirstOrDefault();

                if (rcv is Blob { Value: IEnumerable<byte> byteEnumerable })
                {
                    str = Convert.ToHexString(byteEnumerable.ToArray());
                }
                // ReSharper disable once ConvertTypeCheckPatternToNullCheck
                else if (rcv is Blob { Value: byte[] directBytes })
                {
                    str = Convert.ToHexString(directBytes);
                }
            }
            else
            {
                // freaking black magic to convert the column row to the value
                str =
                    string.Join("", rs.Rows.Select(row => string.Join("", row.Select(x => x.ToString()))));
            }
           
        #if DEBUG
            if (isBlob)
            {
                Logger.WriteLine($"[DBHandler::QueryKey][{sId}] Got value!\r\n\tKey: {key}\r\n\tIS BLOB", LogType.Debug,
                             true);
            }
            else
            {
                Logger.WriteLine($"[DBHandler::QueryKey][{sId}] Got value!\r\n\tKey: {key}\r\n\tValue:\r\n{str}", LogType.Debug,
                             true);
            }
            
        #endif
            return (200, str, null); // 200: OK, return value
        }
        // No need to handle all these error catcher with sentry
        // The error should be handled in the method caller instead
        catch (LibsqlException ex) when (ex.Message.Contains("STREAM_EXPIRED", StringComparison.OrdinalIgnoreCase) ||
                                         ex.Message.Contains("Received an invalid baton", StringComparison.OrdinalIgnoreCase) || 
                                         ex.Message.Contains("stream not found", StringComparison.OrdinalIgnoreCase))
        {
            Logger.WriteLine("[DBHandler::QueryKey] Database stream expired.",
                         LogType.Error, true);

            await Init();
            return (419, null, ex); // 419: Stream expired
        }
        catch (Exception ex)
        {
            Logger.WriteLine($"[DBHandler::QueryKey] Failed when getting value for key {key}! {ex.Message}",
                         LogType.Error, true);
            return (500, null, ex); // 500: Internal Server Error
        }
    }

    private static async Task<(int result, Exception? exceptionValue)> StoreKeyValueInternal(string key, string value, bool isBlob = false, byte[]? blobValue = null)
    {
        try
        {
            if (_database == null) await Init(true);
            var tableName = "uid-"  + _userIdHash + (isBlob ? "-blob" : "");
            object dbValue   = isBlob && blobValue != null ? blobValue : value;
                    
            // Create key for storing value, if key already exist, just update the value (key column is set to UNIQUE)
            var command = $"INSERT INTO \"{tableName}\" (key, value) VALUES (?, ?) " +
                          $"ON CONFLICT(key) DO UPDATE SET value = ?";
            var parameters = new object[] { key, dbValue, dbValue };
            await _database!.Execute(command, parameters);
                
            return (200, null); // 200: OK
        }
        catch (LibsqlException ex) when (ex.Message.Contains("STREAM_EXPIRED", StringComparison.OrdinalIgnoreCase) ||
                                         ex.Message.Contains("Received an invalid baton", StringComparison.OrdinalIgnoreCase) || 
                                         ex.Message.Contains("stream not found", StringComparison.OrdinalIgnoreCase))
        {
            Logger.WriteLine($"[DBHandler::StoreKeyValue] Database stream expired, Reinitializing ({ex.Message})", LogType.Error,
                         true);
                
            await Init();
            return (419, ex); // 419: Stream expired
        }
        // No need to handle all these error catcher with sentry
        // The error should be handled in the method caller instead
        catch (Exception ex)
        {
            Logger.WriteLine($"[DBHandler::StoreKeyValue] Failed when saving value for key {key}! {ex.Message}",
                         LogType.Error, true);
            return (500, ex); // 500: Internal Server Error
        }
    }

    #endregion
}