// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Hi3Helper.CommunityToolkit.WinUI.Controls;

// Adapted from https://github.com/rudyhuyn/XamlPlus
internal static class ResourceDictionaryExtensions
{
    /// <summary>
    /// Copies  the <see cref="ResourceDictionary"/> provided as a parameter into the calling dictionary, includes overwriting the source location, theme dictionaries, and merged dictionaries.
    /// </summary>
    /// <param name="destination">ResourceDictionary to copy values to.</param>
    /// <param name="source">ResourceDictionary to copy values from.</param>
    internal static void CopyFrom(this ResourceDictionary destination, ResourceDictionary source)
    {
        if (source.Source != null)
        {
            destination.Source = source.Source;
        }
        else
        {
            // Clone theme dictionaries
            if (source.ThemeDictionaries != null)
            {
                foreach (KeyValuePair<object, object> theme in source.ThemeDictionaries)
                {
                    if (theme.Value is ResourceDictionary themedResource)
                    {
                        var themeDictionary = new ResourceDictionary();
                        themeDictionary.CopyFrom(themedResource);
                        destination.ThemeDictionaries[theme.Key] = themeDictionary;
                    }
                    else
                    {
                        destination.ThemeDictionaries[theme.Key] = theme.Value;
                    }
                }
            }

            // Clone merged dictionaries
            if (source.MergedDictionaries != null)
            {
                foreach (var mergedResource in source.MergedDictionaries)
                {
                    var themeDictionary = new ResourceDictionary();
                    themeDictionary.CopyFrom(mergedResource);
                    destination.MergedDictionaries.Add(themeDictionary);
                }
            }

            // Clone all contents
            foreach (KeyValuePair<object, object> item in source)
            {
                destination[item.Key] = item.Value;
            }
        }
    }
}