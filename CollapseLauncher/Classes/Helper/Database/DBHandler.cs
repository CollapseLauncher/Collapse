using SQLite;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;


namespace CollapseLauncher.Helper.Database
{
    public class DbHandler
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
                if (!string.IsNullOrEmpty(_uri)) return _token;
                
                var c = DbConfig.DbToken;
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
        
        private SQLiteAsyncConnection _database;

        private Dictionary<string, (string Value, DateTime Expiration)> _cache = new();
        private TimeSpan _cacheTTL = TimeSpan.FromMinutes(5);

        public async void Init()
        {
            DbConfig.Init();

            _database = new SQLiteAsyncConnection(Uri);
            await _database.CreateTableAsync<KeyValueDb>();
            await CacheDatabase();
        }

        public async Task<string> QueryKey(string key)
        {
            if (_cache.TryGetValue(key, out var c))
            {
                if (c.Expiration > DateTime.UtcNow) return c.Value;
                
                _cache.Remove(key);
            }

            var kv = await _database.Table<KeyValueDb>().Where(x => x.Key == key).FirstOrDefaultAsync();
            if (kv != null)
            {
                _cache[key] = (kv.Value, DateTime.UtcNow.Add(_cacheTTL));
                return kv.Value;
            }

            return null;
        }

        public async Task StoreKeyValue(string key, string value)
        {
            var kv = new KeyValueDb() { Key = key, Value = value };
            await _database.InsertOrReplaceAsync(kv);
            _cache[key] = (value, DateTime.UtcNow.Add(_cacheTTL));
        }

        private async Task CacheDatabase()
        {
            var allEntries = await _database.Table<KeyValueDb>().ToListAsync();
            foreach (var e in allEntries)
            {
                _cache[e.Key] = (e.Value, DateTime.UtcNow.Add(_cacheTTL));
            }
        }
    }

    public class KeyValueDb
    {
        [PrimaryKey, AutoIncrement]
        public int    Id    { get; set; }
        public string Key   { get; set; }
        public string Value { get; set; }
    }
}