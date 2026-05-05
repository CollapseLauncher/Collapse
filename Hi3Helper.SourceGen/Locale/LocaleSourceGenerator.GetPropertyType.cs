using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

namespace Hi3Helper.SourceGen.Locale;

public partial class LocaleSourceGenerator
{
    private const string PropertyTypeString                = "string";
    private const string PropertyTypeDictStringString      = "Dictionary<string, string>";
    private const string PropertyTypeDictStringStringArray = "Dictionary<string, string[]>";

    private const string DefaultRetTypeString = "string.Empty";
    private const string DefaultRetTypeDict   = "[]";

    private static readonly string ClassNameAsDictStringStringPrefix = "_DictKvp";
    private static readonly HashSet<string> ClassNamesAsDictStringString = [
        "_GameClientTitles",
        "_GameClientRegions",
        "_WpfPackageName"
    ];

    private static void GetPropertyType(JsonProperty property, string classNamePrefix, out string? typeName, out string? defaultValueSyntax)
    {
        JsonElement value = property.Value;

        switch (value.ValueKind)
        {
            // Check for string type
            case JsonValueKind.String:
                typeName           = PropertyTypeString;
                defaultValueSyntax = DefaultRetTypeString;
                return;
            // Check for object types
            case JsonValueKind.Object:
                // If the property type is the language section itself, then return its own type instead.
                if (property.Name[0] == '_' &&
                    !(ClassNamesAsDictStringString.Contains(property.Name) ||
                      property.Name.StartsWith(ClassNameAsDictStringStringPrefix, StringComparison.OrdinalIgnoreCase)))
                {
                    typeName           = $"{classNamePrefix}{property.Name.Substring(1)}";
                    defaultValueSyntax = "default";
                    return;
                }

                if (TryGetSupportedObjectType(ref value, out typeName, out defaultValueSyntax))
                {
                    return;
                }
                goto default;
            case JsonValueKind.Undefined:
            case JsonValueKind.Array:
            case JsonValueKind.Number:
            case JsonValueKind.True:
            case JsonValueKind.False:
            case JsonValueKind.Null:
            default:
                throw new NotSupportedException($"Type of the {property.Name} property is not supported. The value must be a string, Dictionary<string, string> or Dictionary<string, string[]> type!");
        }
    }

    private static bool TryGetSupportedObjectType(ref JsonElement element,
                                                  out string?     typeName,
                                                  out string?     defaultValueSyntax)
    {
        // Check if type is Dictionary<string, string>
        if (element.EnumerateObject()
                   .All(x => x.Value.ValueKind == JsonValueKind.String))
        {
            typeName           = PropertyTypeDictStringString;
            defaultValueSyntax = DefaultRetTypeDict;
            return true;
        }

        // Check if type is Dictionary<string, string[]>
        if (element.EnumerateObject()
                   .All(x => x.Value.ValueKind == JsonValueKind.Array))
        {
            typeName           = PropertyTypeDictStringStringArray;
            defaultValueSyntax = DefaultRetTypeDict;
            return true;
        }

        typeName           = null;
        defaultValueSyntax = null;
        return false;
    }
}
