using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace Hi3Helper.Data
{
    public class IniSection : IDictionary<string, IniValue>
    {
        #region Fields
        private Dictionary<string, IniValue> ValuesDict;
        internal IEqualityComparer<string> Comparer;
        #endregion

        #region Properties
        public IniValue this[string name]
        {
            get
            {
                IniValue val;
                if (ValuesDict.TryGetValue(name, out val))
                {
                    return val;
                }
                return new IniValue();
            }
            set
            {
                ValuesDict[name] = value;
            }
        }

        public ICollection<string> Keys => ValuesDict.Keys;

        public ICollection<IniValue> Values => ValuesDict.Values;

        public int Count => ValuesDict.Count;

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
            ValuesDict = new Dictionary<string, IniValue>(stringComparer);
        }

        public IniSection(Dictionary<string, IniValue> values)
            : this(values, IniFile.DefaultComparer)
        {
        }

        public IniSection(Dictionary<string, IniValue> values, IEqualityComparer<string> stringComparer)
        {
            Comparer = stringComparer;
            ValuesDict = new Dictionary<string, IniValue>(values, stringComparer);
        }

        public IniSection(IniSection? values)
            : this(values, IniFile.DefaultComparer)
        {
        }

        public IniSection(IniSection? values, IEqualityComparer<string> stringComparer)
        {
            Comparer = stringComparer;
            if (values == null)
            {
                ValuesDict = new Dictionary<string, IniValue>();
                return;
            }

            ValuesDict = new Dictionary<string, IniValue>(values, stringComparer);
        }
        #endregion

        #region IDictionary and ICollection Methods
        public void Add(string key, IniValue value) => ValuesDict.Add(key, value);

        public bool ContainsKey(string key) => ValuesDict.ContainsKey(key);

        public bool Remove(string key) => ValuesDict.Remove(key);

        public bool TryGetValue(string key, out IniValue value) => ValuesDict.TryGetValue(key, out value);

        public void Add(KeyValuePair<string, IniValue> item) => ValuesDict.Add(item.Key, item.Value);

        public void Clear() => ValuesDict.Clear();

        public bool Contains(KeyValuePair<string, IniValue> item) => ValuesDict.Contains(item);

        public void CopyTo(KeyValuePair<string, IniValue>[] array, int arrayIndex) => ((IDictionary<string, IniValue>)ValuesDict).CopyTo(array, arrayIndex);

        public bool Remove(KeyValuePair<string, IniValue> item) => ((IDictionary<string, IniValue>)ValuesDict).Remove(item);

        public IEnumerator<KeyValuePair<string, IniValue>> GetEnumerator() => ValuesDict.GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
        #endregion

        #region Implicit Cast Operators
        public static implicit operator IniSection(Dictionary<string, IniValue> dict) => new IniSection(dict);
        #endregion

        #region Explicit Cast Operators
        public static explicit operator Dictionary<string, IniValue>(IniSection section) => section.ValuesDict;
        #endregion
    }
}
