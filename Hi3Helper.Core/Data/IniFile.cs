/*
 * This code was originally ported from https://github.com/Enichan/Ini
 * Some changes have been made to adjust the usage to our main project, Collapse Launcher
 */
using System;
using System.Buffers;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

#nullable enable
namespace Hi3Helper.Data
{
    public sealed class IniFile : IDictionary<string, IniSection?>
    {
        #region Constants
        const char SectionStart = '[';
        const char SectionEnd = ']';
        const char KeyValueSeparatorMark = '=';
        const char ValueQuoteStartEndMark = '"';
        #endregion

        #region Fields
        private IDictionary<string, IniSection?> Sections;
        private bool IsSaveOrdered;

        internal static readonly IEqualityComparer<string> DefaultComparer = StringComparer.OrdinalIgnoreCase;
        internal static readonly CultureInfo DefaultCulture = CultureInfo.InvariantCulture;

        public bool SaveEmptySections;
        public IEqualityComparer<string> CurrentStringComparer;

        private const int OpenStreamBufferSize = 4 << 10;
        private const int WriteBufferSize = 1 << 7;
        private const int CreateStreamBufferSize = 1 << 10;

        // Create default section if the current one is not defined.
        private IniSection DefaultSection;
        #endregion

        #region Properties
        public IniSection this[string section]
        {
            get
            {
                // Get existing section or create a new one if it doesn't exist
                GetOrCreateSection(section, out IniSection iniSection);
                return iniSection;
            }
#pragma warning disable CS8767 // Nullability of reference types in type of parameter doesn't match implicitly implemented member (possibly because of nullability attributes).
            set
            {
                IniSection valueSet = value;

                // If the value set is not null and the comparer is not equal,
                // recreate the section to match the comparer
                // ReSharper disable once PossibleUnintendedReferenceComparison
                if (valueSet.Comparer != CurrentStringComparer)
                {
                    valueSet = new IniSection(valueSet, CurrentStringComparer);
                }

                // If the existing section key exist, override it
                if (Sections.ContainsKey(section))
                {
                    Sections[section] = valueSet;
                    return;
                }

                // If the key doesn't exist, create a new one
                Sections.Add(section, valueSet);
            }
#pragma warning restore CS8767 // Nullability of reference types in type of parameter doesn't match implicitly implemented member (possibly because of nullability attributes).
        }

        public ICollection<string> Keys => Sections.Keys;

        public ICollection<IniSection?> Values => Sections.Values;

        public int Count => Sections.Count;

        public bool IsReadOnly => false;
        #endregion

        #region Constructions
        public IniFile()
            : this(DefaultComparer) { }

        public IniFile(IEqualityComparer<string>? stringComparer, bool isSaveOrdered = true)
        {
            CurrentStringComparer = stringComparer ?? DefaultComparer;
            Sections = new Dictionary<string, IniSection?>(CurrentStringComparer);
            IsSaveOrdered = isSaveOrdered;
            DefaultSection = new IniSection(CurrentStringComparer);
        }
        #endregion

        #region Load and Save Methods
        public void Save(string filePath)
            => Save(new FileInfo(filePath));

        public void Save(FileInfo fileInfo)
        {
            // Throw if FileInfo is null
            ArgumentNullException.ThrowIfNull(fileInfo, nameof(fileInfo));

            // Ensure that the folder will always be exist
            EnsureFolderExist(fileInfo);

            // Load the stream and writer, then store the data
            using FileStream stream = new FileStream(fileInfo.FullName, FileMode.Create, FileAccess.Write, FileShare.ReadWrite, CreateStreamBufferSize, FileOptions.None);
            using TextWriter writer = new StreamWriter(stream, encoding: Encoding.UTF8, leaveOpen: false);
            SaveInner(writer);
        }

        public void Save(Stream stream)
        {
            // Stream cannot be null
            ArgumentNullException.ThrowIfNull(stream, nameof(stream));

            // Assign the writer
            using TextWriter writer = new StreamWriter(stream, encoding: Encoding.UTF8, leaveOpen: true, bufferSize: WriteBufferSize);
            SaveInner(writer);
        }

        private void SaveInner(TextWriter writer)
        {
            // Enumerate Ini sections
            foreach (KeyValuePair<string, IniSection?> iniSectionKvp in Sections)
            {
                // Get section value
                IniSection? valueSection = iniSectionKvp.Value;

                // Write the section line
                writer.WriteLine($"[{iniSectionKvp.Key}]");

                // Enumerate values in section if it's not null or empty
                if (valueSection != null && valueSection.Count != 0)
                {
                    // Get value enumerable
                    IEnumerable<KeyValuePair<string, IniValue>> valueEnumerable = IsSaveOrdered ?
                        valueSection.OrderBy(x => x.Key) :
                        valueSection;

                    // Enumerate values
                    foreach (KeyValuePair<string, IniValue> sectionValueKvp in valueEnumerable)
                    {
                        // Append the key name and value
                        writer.WriteLine($"{sectionValueKvp.Key}={sectionValueKvp.Value.Value}");
                    }
                }

                // Write new blank line as its separator
                writer.WriteLine();
            }
        }

        public static IniFile LoadFrom(string filePath, IEqualityComparer<string>? stringComparer = null, bool isSaveOrdered = true, bool createIfNotExist = false)
            => LoadFrom(new FileInfo(filePath), stringComparer, isSaveOrdered, createIfNotExist);

        public static IniFile LoadFrom(FileInfo fileInfo, IEqualityComparer<string>? stringComparer = null, bool isSaveOrdered = true, bool createIfNotExist = false)
        {
            // Throw if FileInfo is null
            ArgumentNullException.ThrowIfNull(fileInfo, nameof(fileInfo));

            // Create new instance
            IniFile instance = new IniFile(stringComparer, isSaveOrdered);

            // If the "create if not exist" flag is set to false and file is not exist,
            // then ignore and create a new instance
            if (!createIfNotExist && !fileInfo.Exists)
            {
                return instance;
            }

            // Load the file into instance
            instance.Load(fileInfo, createIfNotExist);

            // Return the instance
            return instance;
        }

        public static IniFile LoadFrom(Stream stream, IEqualityComparer<string>? stringComparer = null, bool isSaveOrdered = true)
        {
            // Create a new instance
            IniFile instance = new IniFile(stringComparer, isSaveOrdered);

            // Load the stream of .ini file
            instance.Load(stream);

            // Return the instance
            return instance;
        }

        public void Load(string filePath, bool createIfNotExist = false)
            => Load(new FileInfo(filePath), createIfNotExist);

        public void Load(FileInfo fileInfo, bool createIfNotExist = false)
        {
            // Throw if FileInfo is null
            ArgumentNullException.ThrowIfNull(fileInfo, nameof(fileInfo));

            // Assign the FileMode
            FileMode fileMode = createIfNotExist ? FileMode.OpenOrCreate : FileMode.Open;
            if (createIfNotExist)
            {
                // Always ensure folder existence if createIfNotExist was toggled
                EnsureFolderExist(fileInfo);
            }

            // Set the stream and reader
            using FileStream stream = new FileStream(fileInfo.FullName, fileMode, FileAccess.Read, FileShare.ReadWrite, OpenStreamBufferSize, FileOptions.None);
            using TextReader reader = new StreamReader(stream, leaveOpen: false);
            LoadInner(reader);
        }

        public void Load(Stream stream)
        {
            // Stream cannot be null
            ArgumentNullException.ThrowIfNull(stream, nameof(stream));

            // Assign the stream to reader and leave it open after using it.
            using TextReader reader = new StreamReader(stream, leaveOpen: true);
            LoadInner(reader);
        }

        private void LoadInner(TextReader reader)
        {
            string? currentSectionToReadName;
            IniSection? currentSectionToRead = null;

            // Starting in this line, we don't use EndOfStream anymore as the reader.ReadLine()
            // will return as null if the stream is already at the end instead.
            // 
            // Docs: https://github.com/dotnet/runtime/issues/98834
            string? curLine;
            while ((curLine = reader.ReadLine()) != null)
            {
                // Always trim whitespaces and convert to span, just in case.
                ReadOnlySpan<char> curLineSpanTrimmed = curLine.AsSpan().Trim();

                // If empty, read next line
                if (curLineSpanTrimmed.Length == 0)
                {
                    continue;
                }

                #region Check and Get Section
                // If the trimmed line starts with [ and ], then assign it as section name
                if (curLineSpanTrimmed.StartsWith(SectionStart) && curLineSpanTrimmed.EndsWith(SectionEnd))
                {
                    // Assign the section to read, also in case there's an additional
                    // whitespaces between the section name, always trim it.
                    ReadOnlySpan<char> currentSectionNameInner = curLineSpanTrimmed[1..^1];

                    // If the section name is actually empty (where the current
                    // line only consist of "[]", then continue reading the next line
                    if (currentSectionNameInner.Length == 0)
                    {
                        continue;
                    }

                    // Get the section name
                    currentSectionToReadName = currentSectionNameInner.ToString();

                    // Try to get the section and if it's not exist yet, then add it
                    GetOrCreateSection(currentSectionToReadName, out currentSectionToRead);

                    // Read the next line
                    continue;
                }
                #endregion

                #region Read key and value pairs
                // Get the section to use. If the actual section to read is empty,
                // then use the default section.
                IniSection sectionToUse = currentSectionToRead ?? DefaultSection;
                ReadLineAsValue(curLineSpanTrimmed, sectionToUse);
                #endregion
            }

            // If default section is not empty, add it to the section
            if (DefaultSection.Count > 0)
            {
                Add("default", DefaultSection);
            }
        }

        private void EnsureFolderExist(FileInfo fileInfo)
        {
            // Always create the directory if none of it exist
            if (fileInfo.Directory != null && !fileInfo.Directory.Exists)
                fileInfo.Directory.Create();
        }

        private void GetOrCreateSection(string sectionName, [NotNull] out IniSection? iniSection)
        {
            // Try to get the section and if it's not exist yet, then add it
            if (!Sections.TryGetValue(sectionName, out iniSection))
            {
                // Create a new section, then add it to the dictionary
                iniSection = new IniSection(CurrentStringComparer);
                Sections.Add(sectionName, iniSection);

                return;
            }

            // If the key exist but the section exist, create a new one
            if (iniSection == null)
            {
                // Create a new section, then set it to existing key name
                iniSection = new IniSection(CurrentStringComparer);
                Sections[sectionName] = iniSection;
            }
        }

        private void ReadLineAsValue(ReadOnlySpan<char> curLineTrimmed, IniSection curSection)
        {
            // Get the range of key and value
            int kvpRangeOffset = curLineTrimmed.IndexOf(KeyValueSeparatorMark);

            // If the key value pair mark isn't found, then leave it empty
            // and return to avoid reading the line
            if (kvpRangeOffset < 1)
            {
                return;
            }

            // Get key span (and also trim end whitespaces if any)
            ReadOnlySpan<char> keySpan = curLineTrimmed[..kvpRangeOffset].TrimEnd();

            // Get value span (and also trim whitespaces and quote mark if any)
            ReadOnlySpan<char> valueSpan = curLineTrimmed[(kvpRangeOffset + 1)..]
                .TrimStart()
                .Trim(ValueQuoteStartEndMark);

            // Get key string
            string keyString = keySpan.ToString();

            // If the valueSpan is empty, create a new or override existing value to empty ones.
            if (valueSpan.Length == 0)
            {
                // Add new or override existing value
                TryAddOrOverrideSectionValue(curSection, keyString, new IniValue());
            }

            // Get value as string
            string valueString = valueSpan.ToString();

            // Add new or override existing value
            TryAddOrOverrideSectionValue(curSection, keyString, new IniValue(valueString));
        }

        private void TryAddOrOverrideSectionValue(IniSection curSection, string keyString, IniValue value)
        {
            // If the existing value already exist, then set the new value to override
            if (curSection.TryGetValue(keyString, out _))
            {
                // Set the new value
                curSection[keyString] = value;
                return;
            }

            // Otherwise, add as a new value
            curSection.Add(keyString, value);
        }
        #endregion

        #region IDictionary and ICollection Methods
        public bool TryGetValue(string key, out IniSection? section) => Sections.TryGetValue(key, out section);

        public bool Remove(string section)
        {
            if (!string.IsNullOrEmpty(section))
            {
                return Sections.Remove(section);
            }

            return false;
        }

        public void Add(string key, IniSection? value)
        {
            // If the key doesn't exist and the section value
            // to add is not null, then add it
            if (!Sections.ContainsKey(key) && value != null)
            {
                Sections.Add(key, value);
                return;
            }

            // Otherwise, Try get or create a new section so
            // the value will be merged
            IniSection section = this[key];

            // If the value is null, then return
            if (value == null)
                return;

            // Merge or add to the section
            foreach (KeyValuePair<string, IniValue> keyValuePair in value)
            {
                TryAddOrOverrideSectionValue(section, keyValuePair.Key, keyValuePair.Value);
            }
        }

        public IniSection? Add(string section) => this[section];

        public bool ContainsKey(string key) => Sections.ContainsKey(key);

        public void Add(KeyValuePair<string, IniSection?> item) => Sections.Add(item.Key, item.Value);

        public void Clear() => Sections.Clear();

        public bool Contains(KeyValuePair<string, IniSection?> item) => Sections.Contains(item);

        public void CopyTo(KeyValuePair<string, IniSection?>[] array, int arrayIndex) => Sections.CopyTo(array, arrayIndex);

        public bool Remove(KeyValuePair<string, IniSection?> item) => Sections.Remove(item);

        public IEnumerator<KeyValuePair<string, IniSection?>> GetEnumerator() => Sections.GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
        #endregion

        #region ToString Methods
        public override string ToString()
        {
            // Use MemoryStream as temporary stream to write into
            using MemoryStream memoryStream = new MemoryStream();

            // Save the Ini content to memoryStream
            Save(memoryStream);

            // Ensure to flush remained buffer
            memoryStream.Flush();

            // Create task factory to return actual string value
            return GetIniContentStringInner(memoryStream);
        }

        private string GetIniContentStringInner(MemoryStream memoryStream)
        {
            // Get the buffer segment and build the string
            if (memoryStream.TryGetBuffer(out ArraySegment<byte> buffer) && buffer.Array != null)
            {
                // Get buffer span
                ReadOnlySpan<byte> bufferSpan = buffer.Array;
                char[] bufferString = new char[bufferSpan.Length];

                try
                {
                    // Try get the unicode string and write it to bufferString.
                    // If it fails, then return an empty string
                    if (!Encoding.UTF8.TryGetChars(bufferSpan, bufferString, out int charsWritten))
                    {
                        // Return an empty string
                        return "";
                    }

                    // Create a string
                    string returnString = new string(bufferString, 0, charsWritten);
                    return returnString;
                }
                finally
                {
                    ArrayPool<char>.Shared.Return(bufferString);
                }
            }

            // If the buffer cannot be obtained, then return an empty string
            return "";
        }
        #endregion
    }
}
