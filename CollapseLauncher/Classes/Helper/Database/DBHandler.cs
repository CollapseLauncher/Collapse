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
        
        private static Guid _userId;
        private static Guid UserId
        {
            get
            {
                if (!string.IsNullOrEmpty(_uri)) return _userId;
                
                var c = DbConfig.UserGuid;
                _userId = c;
                return c;
            }
        }

        #endregion
        
        private static IDatabaseClient _database;

        public static async void Init()
        {
            DbConfig.Init();

            try
            {
                _database = await DatabaseClient.Create(opts =>
                                                        {
                                                            opts.Url       = Uri;
                                                            opts.AuthToken = Token;
                                                        });
                await
                    _database.Execute("CREATE TABLE IF NOT EXISTS KeyValueDb (Id INTEGER PRIMARY KEY AUTOINCREMENT, 'key' TEXT UNIQUE NOT NULL, 'value' TEXT)");
                
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
            var rs  = await _database.Execute("SELECT value FROM 'KeyValueDb' WHERE key LIKE concat('%', ?, '%')", key);
            if (rs != null)
            {
                return string.Join("", rs.Rows.Select(row => string.Join("", row.Select(x => x.ToString()))));
            }

            return null;
        }

        public static async Task StoreKeyValue(string key, string value)
        {
            var command = $"INSERT INTO 'KeyValueDb' (key, value) VALUES ('{key}', '{value}') " +
                          $"ON CONFLICT(key) DO UPDATE SET value = '{value}'";
            await _database.Execute(command);
        }
    }
}