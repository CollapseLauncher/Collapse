using System.Text;
using System.Text.Json;

namespace Hi3Helper.SourceGen.Locale;

public partial class LocaleSourceGenerator
{
    private static void WriteClassBinding(string className, StringBuilder builder, JsonElement rootElement)
    {
        builder.AppendLine($$"""
                                 private static readonly Dictionary<string, BindableCustomProperty> {{className}}Lookups = new(StringComparer.OrdinalIgnoreCase)
                                 {
                             """);

        foreach (JsonProperty property in rootElement.EnumerateObject())
        {
            GetPropertyType(property, className, out string? propertyType, out _);
            builder.AppendLine($$"""
                                         {
                                             nameof({{property.Name}}), new BindableCustomProperty(
                                                 true,
                                                 true,
                                                 nameof({{property.Name}}),
                                                 typeof({{propertyType}}),
                                                 static instance => (({{className}})instance).{{property.Name}},
                                                 static (instance, value) => (({{className}})instance).{{property.Name}} = ({{propertyType}})value,
                                                 null,
                                                 null)
                                         },
                                 """);
        }

        builder.AppendLine($$"""
                                 };
                                 
                                 public override BindableCustomProperty GetProperty(string name) =>
                                     !{{className}}Lookups.TryGetValue(name, out BindableCustomProperty? property)
                                         ? throw ExceptionPropertyNotFound(name)
                                         : property;
                             """);
    }
}
