using Hi3Helper;
using Libsql.Client;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Windows.System;
using static Hi3Helper.Logger;

namespace CollapseLauncher.Helper.Database
{
    internal class DbHandler
    {
        #region Config Properties

        private static string _uri;

        private static string Uri
        {
            get
            {
                if (!string.IsNullOrEmpty(_uri)) return _uri;
                var c = DbConfig.DbUrl;
                _uri = c;
                return c;
            }
        }

        private static string _token;

        private static string Token
        {
            get
            {
                if (!string.IsNullOrEmpty(_token)) return _token;
                var c = DbConfig.DbToken;
                if (string.IsNullOrEmpty(c)) throw new InvalidDataException("Database token could not be empty!");
                _token = c;
                return c;
            }
        }

        private static Guid?  _userId;
        private static string _userIdHash;

        private static Guid UserId
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
        }

        #endregion

        private static IDatabaseClient _database;

        public static async void Init()
        {
            DbConfig.Init();

            // Init props
            _ = Token;
            _ = Uri;
            _ = UserId;
            try
            {
                _database = await DatabaseClient.Create(opts =>
                                                        {
                                                            opts.Url       = Uri;
                                                            opts.AuthToken = Token;
                                                        });
                await
                    _database.Execute($"CREATE TABLE IF NOT EXISTS \"uid-{_userIdHash}\" (Id INTEGER PRIMARY KEY AUTOINCREMENT, 'key' TEXT UNIQUE NOT NULL, 'value' TEXT)");

                // test code
                // await StoreKeyValue("testdate", DateTime.Now.ToString(CultureInfo.CurrentCulture));
                // var res = await QueryKey("testdate");
                // LogWriteLine($"Test SQL data {res}", LogType.Error);
            }
            catch (Exception e)
            {
                LogWriteLine($"[DBHandler::Init] Error!\r\n{e}", LogType.Error, true);
            }
        }

        public static async Task<string> QueryKey(string key)
        {
        #if DEBUG
            LogWriteLine($"[DBHandler::QueryKey] Invoked!\r\n\tKey: {key}", LogType.Debug, true);
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
                        LogWriteLine($"[DBHandler::QueryKey] Got value!\r\n\tKey: {key}\r\n\t{str}", LogType.Debug,
                                     true);
                    #endif
                        return str;
                    }
                }
                catch (LibsqlException ex) when ((ex.Message.Contains("STREAM_EXPIRED") ||
                                                  ex.Message.Contains("Received an invalid baton")) &&
                                                 i < retryCount - 1)
                {
                    if (i == 0)
                        LogWriteLine("[DBHandler::QueryKey] Database stream expired, retrying...", LogType.Error, true);

                    Init();
                }
                catch (Exception ex) when (i < retryCount - 1)
                {
                    LogWriteLine($"[DBHandler::QueryKey] Failed when getting value for key {key}! Retrying...\r\n{ex}",
                                 LogType.Error, true);
                    break;
                }
                catch (Exception ex)
                {
                    LogWriteLine($"[DBHandler::QueryKey] Failed when getting value for key {key} after {retryCount} retries! Returning null...\r\n{ex}",
                                 LogType.Error, true);
                    return null;
                }
            }

            return null;
        }

        public static async Task StoreKeyValue(string key, string value)
        {
        #if DEBUG
            LogWriteLine($"[DBHandler::StoreKeyValue] Invoked!\r\n\tKey: {key}\r\n\tValue: {value}", LogType.Debug,
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
                catch (Exception ex)
                {
                    LogWriteLine($"[DBHandler::StoreKeyValue] Failed when saving value for key {key} after {retryCount} tries!\r\n{ex}",
                                 LogType.Error, true);
                    throw;
                }
                
            }
        }
    }
}