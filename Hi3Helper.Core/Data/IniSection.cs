using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace Hi3Helper.Data
{
    public class IniSection : IDictionary<string, IniValue>
    {
        #region Fields
        private readonly Dictionary<string, IniValue> _valuesDict;
        internal         IEqualityComparer<string>    Comparer;
        #endregion

        #region Properties
        public IniValue this[string name]
        {
            get
            {
                return _valuesDict.TryGetValue(name, out var val) ? val : new IniValue();
            }
            set
            {
                _valuesDict[name] = value;
            }
        }

        public ICollection<string> Keys => _valuesDict.Keys;

        public ICollection<IniValue> Values => _valuesDict.Values;

        public int Count => _valuesDict.Count;

        public bool IsReadOnly => false;
        #endregion

        #region Constructors
        public IniSection()
            : this(IniFile.DefaultComparer)
        {
        }

        public IniSection(IEqualityComparer<string> stringComparer)
        {
            Comparer = stringComparer;
            _valuesDict = new Dictionary<string, IniValue>(stringComparer);
        }

        public IniSection(Dictionary<string, IniValue> values)
            : this(values, IniFile.DefaultComparer)
        {
        }

        public IniSection(Dictionary<string, IniValue> values, IEqualityComparer<string> stringComparer)
        {
            Comparer = stringComparer;
            _valuesDict = new Dictionary<string, IniValue>(values, stringComparer);
        }

        public IniSection(IniSection values)
            : this(values, IniFile.DefaultComparer)
        {
        }

        public IniSection(IniSection values, IEqualityComparer<string> stringComparer)
        {
            Comparer = stringComparer;
            if (values == null)
            {
                _valuesDict = new Dictionary<string, IniValue>();
                return;
            }

            _valuesDict = new Dictionary<string, IniValue>(values, stringComparer);
        }
        #endregion

        #region IDictionary and ICollection Methods
        public void Add(string key, IniValue value) => _valuesDict.Add(key, value);

        public bool ContainsKey(string key) => _valuesDict.ContainsKey(key);

        public bool Remove(string key) => _valuesDict.Remove(key);

        public bool TryGetValue(string key, out IniValue value) => _valuesDict.TryGetValue(key, out value);

        public void Add(KeyValuePair<string, IniValue> item) => _valuesDict.Add(item.Key, item.Value);

        public void Clear() => _valuesDict.Clear();

        public bool Contains(KeyValuePair<string, IniValue> item) => _valuesDict.Contains(item);

        public void CopyTo(KeyValuePair<string, IniValue>[] array, int arrayIndex) => ((IDictionary<string, IniValue>)_valuesDict).CopyTo(array, arrayIndex);

        public bool Remove(KeyValuePair<string, IniValue> item) => ((IDictionary<string, IniValue>)_valuesDict).Remove(item);

        public IEnumerator<KeyValuePair<string, IniValue>> GetEnumerator() => _valuesDict.GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
        #endregion

        #region Implicit Cast Operators
        public static implicit operator IniSection(Dictionary<string, IniValue> dict) => new(dict);
        #endregion

        #region Explicit Cast Operators
        public static explicit operator Dictionary<string, IniValue>(IniSection section) => section._valuesDict;
        #endregion
    }
}
