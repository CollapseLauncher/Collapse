using Hi3Helper;
using Libsql.Client;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using static Hi3Helper.Logger;

namespace CollapseLauncher.Helper.Database
{
    internal static class DbHandler
    {
        #region Config Properties

        private static bool? _enabled;
        public static bool IsEnabled
        {
            get
            {
                if (_enabled != null) return (bool)_enabled;
                var c = DbConfig.DbEnabled;
                _enabled = c;
                return c;
            }
            set
            {
                _enabled           = value;
                DbConfig.DbEnabled = value;

                if (value) Init();
                else Dispose();
            }
        }
        
        
        private static string _uri;
        public static string Uri
        {
            get
            {
                if (!string.IsNullOrEmpty(_uri)) return _uri;
                var c = DbConfig.DbUrl;
                _uri = c;
                return c;
            }
            set
            {
                _uri           = value;
                DbConfig.DbUrl = value;
                _isFirstInit   = true;
            }
        }

        private static string _token;
        public static string Token
        {
            get
            {
                if (!string.IsNullOrEmpty(_token)) return _token;
                var c = DbConfig.DbToken;
                if (string.IsNullOrEmpty(c)) throw new InvalidDataException("Database token could not be empty!");
                _token = c;
                return c;
            }
            set
            {
                _token           = value;
                DbConfig.DbToken = value;
                _isFirstInit     = true;
            }
        }

        private static Guid?  _userId;
        private static string _userIdHash;
        public static Guid UserId
        {
            get
            {
                if (_userId != null) return (Guid)_userId;
                var c = DbConfig.UserGuid;
                _userId = c;
                var byteUidH = System.IO.Hashing.XxHash64.Hash(c.ToByteArray());
                _userIdHash = BitConverter.ToString(byteUidH).Replace("-", "").ToLowerInvariant();
                return c;
            }
            set
            {
                _userId           = value;
                DbConfig.UserGuid = value;
                
                var byteUidH = System.IO.Hashing.XxHash64.Hash(value.ToByteArray());
                _userIdHash  = BitConverter.ToString(byteUidH).Replace("-", "").ToLowerInvariant();
                _isFirstInit = true;
            }
        }

        private static bool _isFirstInit = true;
        #endregion

        private static IDatabaseClient _database;

        public static async Task Init(bool redirectThrow = false)
        {
            DbConfig.Init();
            
            if (!IsEnabled)
            {
                LogWriteLine("[DbHandler::Init] Database functionality is disabled!");
                return;
            }

            try
            {
                // Init props
                _ = Token;
                _ = Uri;
                _ = UserId;

                _database = await DatabaseClient.Create(opts =>
                                                        {
                                                            opts.Url       = Uri;
                                                            opts.AuthToken = Token;
                                                        });

                if (_isFirstInit)
                {
                    LogWriteLine("[DbHandler::Init] Initializing database system...");
                    await
                        _database
                           .Execute($"CREATE TABLE IF NOT EXISTS \"uid-{_userIdHash}\" (Id INTEGER PRIMARY KEY AUTOINCREMENT, 'key' TEXT UNIQUE NOT NULL, 'value' TEXT)");
                    _isFirstInit = false;
                }
                else LogWriteLine("[DbHandler::Init] Reinitializing database system...");
            }
            catch (Exception e) when (!redirectThrow)
            {
                LogWriteLine($"[DBHandler::Init] Error!\r\n{e}", LogType.Error, true);
            }
        }

        private static void Dispose()
        {
            _database = null;
            _token = null;
            _uri = null;
            _userId = null;
            _userIdHash = null;
        }

        public static async Task<string> QueryKey(string key, bool redirectThrow = false)
        {
            if (!IsEnabled) return null;
        #if DEBUG
            var r   = new Random();
            var sId = Math.Abs(r.Next(0, 1000).ToString().GetHashCode());
            LogWriteLine($"[DBHandler::QueryKey][{sId}] Invoked!\r\n\tKey: {key}", LogType.Debug, true);
            var t = System.Diagnostics.Stopwatch.StartNew();
        #endif
            const int retryCount = 3;
            for (var i = 0; i < retryCount; i++)
            {
                try
                {
                    var rs =
                        await
                            _database
                               .Execute($"SELECT value FROM \"uid-{_userIdHash}\" WHERE key LIKE concat('%', ?, '%')",
                                        key);
                    if (rs != null)
                    {
                        var str =
                            string.Join("", rs.Rows.Select(row => string.Join("", row.Select(x => x.ToString()))));
                    #if DEBUG
                        LogWriteLine($"[DBHandler::QueryKey] Got value!\r\n\tKey: {key}\r\n\tValue:\r\n{str}", LogType.Debug,
                                     true);
                    #endif
                        return str;
                    }
                }
                catch (LibsqlException ex) when ((ex.Message.Contains("STREAM_EXPIRED") ||
                                                  ex.Message.Contains("Received an invalid baton")) &&
                                                 i < retryCount - 1)
                {
                    if (i > 0)
                        LogWriteLine("[DBHandler::QueryKey] Database stream expired, retrying...", LogType.Error, true);

                    Init();
                }
                catch (Exception ex) when (i < retryCount - 1)
                {
                    LogWriteLine($"[DBHandler::QueryKey] Failed when getting value for key {key}! Retrying...\r\n{ex}",
                                 LogType.Error, true);
                    break;
                }
                catch (Exception ex) when (!redirectThrow)
                {
                    LogWriteLine($"[DBHandler::QueryKey] Failed when getting value for key {key} after {retryCount} retries! Returning null...\r\n{ex}",
                                 LogType.Error, true);
                    return null;
                }
            #if DEBUG
                finally
                {
                    t.Stop();
                    LogWriteLine($"[DBHandler::QueryKey][{sId}] Operation took {t.ElapsedMilliseconds} ms!", LogType.Debug, true);
                }
            #endif
            }

            return null;
        }

        public static async Task StoreKeyValue(string key, string value, bool redirectThrow = false)
        {
            if (!IsEnabled) return;
        #if DEBUG
            var t   = System.Diagnostics.Stopwatch.StartNew();
            var r   = new Random();
            var sId = Math.Abs(r.Next(0, 1000).ToString().GetHashCode());
            LogWriteLine($"[DBHandler::StoreKeyValue][{sId}] Invoked!\r\n\tKey: {key}\r\n\tValue: {value}", LogType.Debug,
                         true);
        #endif
            const int retryCount = 5;
            for (var i = 0; i < retryCount; i++)
            {
                try
                {
                    var command = $"INSERT INTO \"uid-{_userIdHash}\" (key, value) VALUES (?, ?) " +
                                  $"ON CONFLICT(key) DO UPDATE SET value = ?";
                    var parameters = new object[] { key, value, value };
                    await _database.Execute(command, parameters);
                    break;
                }
                catch (LibsqlException ex) when ((ex.Message.Contains("STREAM_EXPIRED") ||
                                                  ex.Message.Contains("Received an invalid baton")) &&
                                                 i < retryCount - 1)
                {
                    if (i > 0)
                        LogWriteLine("[DBHandler::StoreKeyValue] Database stream expired, retrying...", LogType.Error,
                                     true);

                    Init();
                }
                catch (Exception ex) when (i < retryCount - 1)
                {
                    LogWriteLine($"[DBHandler::StoreKeyValue] Failed when saving value for key {key}! Retrying...\r\n{ex}",
                                 LogType.Error, true);
                }
                catch (Exception ex) when (!redirectThrow)
                {
                    LogWriteLine($"[DBHandler::StoreKeyValue] Failed when saving value for key {key} after {retryCount} tries!\r\n{ex}",
                                 LogType.Error, true);
                    throw;
                }
            #if DEBUG
                finally
                {
                    t.Stop();
                    LogWriteLine($"[DBHandler::StoreKeyValue][{sId}] Operation took {t.ElapsedMilliseconds} ms!",
                                 LogType.Debug, true);
                }
            #endif
            }
        }
    }
}