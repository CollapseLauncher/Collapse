using System;
using System.Collections.Generic;
using System.Linq;

namespace CollapseLauncher.Classes.Extension
{
#nullable enable
    public static class DictionaryExtension
    {
        /// <summary>
        /// Gets the value from a dictionary by key, ignoring case.
        /// </summary>
        /// <param name="dict">Dictionary you want to get value from</param>
        /// <param name="key">Key of the value you want to get</param>
        /// <param name="comparer">Comparison culture you want to use. Default: OrdinalIgnoreCase</param>
        /// <returns>String of the key, null when dictionary is null or not found or value is null</returns>
        public static string? TryGetValueIgnoreCase(this IDictionary<string, string>? dict,
                                                    string key,
                                                    StringComparison comparer = StringComparison.OrdinalIgnoreCase)
        {
            if (dict == null || dict.Count == 0) return null;
    
            // Check if dictionary already implements case-insensitive comparison
            if (comparer == StringComparison.OrdinalIgnoreCase && 
                dict is Dictionary<string, string?> typedDict && 
                Equals(typedDict.Comparer, StringComparer.OrdinalIgnoreCase))
            {
                return dict.TryGetValue(key, out var value) ? value : null;
            }
    
            var matchingPair = dict.FirstOrDefault(x => string.Equals(x.Key, key, comparer));
            return matchingPair.Value;
        }
    }
}