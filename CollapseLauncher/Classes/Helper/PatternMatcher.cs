using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
// ReSharper disable MemberCanBePrivate.Global

namespace CollapseLauncher.Helper
{
    public static class PatternMatcher
    {
        /// <summary>
        /// Determines whether the specified input string matches the given pattern.
        /// The pattern can contain wildcards ('*').
        /// </summary>
        /// <param name="input">The input string to match.</param>
        /// <param name="pattern">The pattern to match against, which can contain wildcards ('*').</param>
        /// <returns>True if the input matches the pattern; otherwise, false.</returns>
        public static bool MatchSimpleExpression(string input, string pattern)
        {
            // Escape special regex characters in the pattern except for the wildcard '*'
            string regexPattern = "^" + Regex.Escape(pattern).Replace("\\*", ".*") + "$";
            return Regex.IsMatch(input,
                                 regexPattern,
                                 RegexOptions.IgnoreCase |
                                 RegexOptions.Compiled |
                                 RegexOptions.NonBacktracking);
        }

        /// <summary>
        /// Determines whether the specified input string matches any pattern in the given list of patterns.
        /// </summary>
        /// <param name="input">The input string to match.</param>
        /// <param name="patterns">A list of patterns to match against, each of which can contain wildcards ('*').</param>
        /// <returns>True if the input matches any pattern in the list; otherwise, false.</returns>
        public static bool MatchesAnyPattern(string input, List<string> patterns)
        {
            return patterns.Any(pattern => MatchSimpleExpression(input, pattern));
        }

        /// <summary>
        /// Perform Regex Matching and enumerate element.
        /// </summary>
        /// <typeparam name="T">The type of element to enumerate.</typeparam>
        /// <param name="enumerable">The enumerable input instance.</param>
        /// <param name="selector">Determines which string element to match the pattern from.</param>
        /// <param name="isMatchNegate">Whether to select negated match.</param>
        /// <param name="regexPatterns">Single or multiple patterns to use.</param>
        /// <returns>Enumerated element matched by the pattern.</returns>
        public static IEnumerable<T> WhereMatchPattern<T>(
            this IEnumerable<T> enumerable,
            Func<T, string>     selector,
            bool                isMatchNegate,
            params string[]     regexPatterns)
        {
            if (regexPatterns.Length == 0)
            {
                return enumerable;
            }

            string mergedPattern = MergeRegexPattern(regexPatterns);
            return WhereMatchPattern(enumerable, selector, isMatchNegate, mergedPattern);
        }

        /// <summary>
        /// Perform Regex Matching and enumerate element.
        /// </summary>
        /// <typeparam name="T">The type of element to enumerate.</typeparam>
        /// <param name="enumerable">The enumerable input instance.</param>
        /// <param name="selector">Determines which string element to match the pattern from.</param>
        /// <param name="isMatchNegate">Whether to select negated match.</param>
        /// <param name="regexPattern">Regex pattern to use.</param>
        /// <returns>Enumerated element matched by the pattern.</returns>
        public static IEnumerable<T> WhereMatchPattern<T>(
            this IEnumerable<T> enumerable,
            Func<T, string>     selector,
            bool                isMatchNegate,
            string              regexPattern)
        {
            Regex regex = new(regexPattern,
                              RegexOptions.IgnoreCase |
                              RegexOptions.NonBacktracking |
                              RegexOptions.Compiled);

            if (string.IsNullOrEmpty(regexPattern))
            {
                return enumerable;
            }

            return enumerable.Where(x => regex.IsMatch(selector(x)) != isMatchNegate);
        }

        /// <summary>
        /// Merge multiple Regex pattern strings into a single Regex pattern string.
        /// </summary>
        /// <param name="regexPatterns">Single or multiple patterns to merge.</param>
        /// <returns>Merged multiple Regex pattern.</returns>
        public static string MergeRegexPattern(params ReadOnlySpan<string> regexPatterns)
        {
            if (regexPatterns.IsEmpty)
            {
                return string.Empty;
            }

            StringBuilder builder = new();
            for (int i = 0; i < regexPatterns.Length; i++)
            {
                builder.Append($"(?:{regexPatterns[i]})");
                if (i < regexPatterns.Length - 1)
                {
                    builder.Append('|');
                }
            }

            return builder.ToString();
        }
    }
}