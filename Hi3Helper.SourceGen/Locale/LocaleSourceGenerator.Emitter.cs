using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
using System;
using System.Text;
using System.Text.Json;
// ReSharper disable ForeachCanBePartlyConvertedToQueryUsingAnotherGetEnumerator Dog sh*t

namespace Hi3Helper.SourceGen.Locale;

public partial class LocaleSourceGenerator
{
    private static void EmitSourceFile(SourceProductionContext ctx, JsonSourceGenerationContext? sourceGenCtx)
    {
        if (sourceGenCtx == null)
        {
            return;
        }

        if (sourceGenCtx.Diagnostic is { } diagnostic)
        {
            ctx.ReportDiagnostic(diagnostic);
            return;
        }

        JsonDocument document       = sourceGenCtx.Document;
        string?      classNamespace = sourceGenCtx.Namespace;
        string?      className      = sourceGenCtx.ClassName;
        string?      classModifiers = sourceGenCtx.Modifier;

        if (classNamespace == null ||
            className == null ||
            classModifiers == null)
        {
            return; // TODO
        }

        BeginWriteGeneric(ref ctx, classNamespace, className, classModifiers, document, sourceGenCtx.IsOnlyProduceBase);
        if (sourceGenCtx.IsOnlyProduceBase)
        {
            return;
        }

        BeginWriteJsonProperties(ref ctx, classNamespace, className, classModifiers, document);
    }

    private static void BeginWriteGeneric(ref SourceProductionContext ctx, string classNamespace, string className, string classModifiers, JsonDocument document, bool isOnlyProduceBase)
    {
        StringBuilder builder = new();

        WriteGenericHeader(builder, classNamespace);
        builder.AppendLine($$"""
                             {{GeneratedCodeAttribute}}
                             public abstract partial class {{className}}BindingNotifier : INotifyPropertyChanged, IBindableCustomPropertyImplementation
                             {
                                 public static Action<Action>? DispatchInvoker;
                                 public static Func<bool>? HasUIThreadAccessInvoker;

                                 private PropertyChangedEventHandler? _propertyChanged;

                                 public event PropertyChangedEventHandler? PropertyChanged
                                 {
                                     add => _propertyChanged += value;
                                     remove => _propertyChanged -= value;
                                 }

                                 protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
                                 {
                                     if (HasUIThreadAccessInvoker?.Invoke() ?? true)
                                     {
                                         Impl();
                                         return;
                                     }

                                     DispatchInvoker?.Invoke(Impl);
                                     return;

                                     void Impl() => _propertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
                                 }
                                 
                                 public abstract BindableCustomProperty GetProperty(string name);
                                 
                                 protected static KeyNotFoundException ExceptionPropertyNotFound(string name) => new KeyNotFoundException($"Property name: {name} doesn't exist.");
                                 
                                 BindableCustomProperty? IBindableCustomPropertyImplementation.GetProperty(Type indexParameterType) => default;
                             }

                             {{GeneratedCodeAttribute}}
                             {{classModifiers}} class {{className}} : {{className}}BindingNotifier
                             {
                                 public static {{className}}? Fallback;
                             """);

        WriteClassProperties(className,
                             builder,
                             new PropertyContext("LanguageName", "string?", false),
                             new PropertyContext("LanguageID", "string?", false),
                             new PropertyContext("Author", "string?", false));

        WriteClassBinding(className, builder, document.RootElement, isOnlyProduceBase);

        builder.AppendLine("}");
        ctx.AddSource($"{className}.g.cs", SourceText.From(builder.ToString(), Encoding.UTF8));
    }

    private static void BeginWriteJsonProperties(ref SourceProductionContext ctx, string classNamespace, string className, string classModifiers, JsonDocument document)
    {
        foreach (JsonProperty rootProperty in document.RootElement.EnumerateObject())
        {
            if (rootProperty.Value.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            string innerClassName = GetInnerClassName(rootProperty.Name);

            StringBuilder builder = new();

            WriteGenericHeader(builder, classNamespace);
            builder.AppendLine($$"""
                                 {{classModifiers}} class {{className}}
                                 {
                                 """);

            string langClassTypeName = $"{className}{innerClassName}";

            // Write parent section property
            GetPropertyType(rootProperty,
                            className,
                            out string? sectionPropertyType,
                            out string? sectionPropertyDefaultValue);

            bool isSectionTypeDictStringString = sectionPropertyType is PropertyTypeDictStringString;
            WriteClassPropertySingle(className,
                                     builder,
                                     new PropertyContext(rootProperty.Name, $"{sectionPropertyType!}{(isSectionTypeDictStringString ? "" : "?")}", true),
                                     sectionPropertyDefaultValue!);

            builder.AppendLine("}");

            // If these properties are Dictionary<string, string> type, stop from writing inner properties.
            if (!isSectionTypeDictStringString)
            {
                builder.AppendLine($$"""

                                     {{GeneratedCodeAttribute}}
                                     public sealed partial class {{langClassTypeName}} : {{className}}BindingNotifier
                                     {
                                     """);
                foreach (JsonProperty stringProperty in rootProperty.Value.EnumerateObject())
                {
                    GetPropertyType(stringProperty,
                                    className,
                                    out string? innerPropertyType,
                                    out string? innerPropertyDefaultValue);
                    WriteClassPropertySingle(className,
                                             builder,
                                             new PropertyContext(stringProperty.Name,
                                                                 innerPropertyType!,
                                                                 true,
                                                                 $"{rootProperty.Name}",
                                                                 true),
                                             innerPropertyDefaultValue!);
                }

                WriteClassBinding(langClassTypeName, builder, rootProperty.Value);
                builder.AppendLine("}");
            }

            ctx.AddSource($"{className}{innerClassName}.g.cs", SourceText.From(builder.ToString(), Encoding.UTF8));
        }

        return;

        static string GetInnerClassName(ReadOnlySpan<char> name)
        {
            return name.TrimStart('_').ToString();
        }
    }

    private static void WriteGenericHeader(StringBuilder builder, string classNamespace)
    {
        builder.AppendLine($"""
                            // <auto-generated/>
                            // Generated on: {DateTimeOffset.Now:F}
                            """);
        builder.AppendLine();
        builder.AppendLine($"""
                            using System;
                            using System.CodeDom.Compiler;
                            using System.Collections.Generic;
                            using System.ComponentModel;
                            using System.Runtime.CompilerServices;
                            using Microsoft.UI.Xaml.Data;
                            using WinRT;

                            #nullable enable
                            namespace {classNamespace};
                            """);
        builder.AppendLine();
    }

    private static void WriteClassProperties(string className, StringBuilder builder, params PropertyContext[] contexts)
    {
        foreach (PropertyContext property in contexts)
        {
            WriteClassPropertySingle(className, builder, property);
        }
    }

    private static void WriteClassPropertySingle(string className, StringBuilder builder, PropertyContext property, string defaultValue = "default")
    {
        if (property.IsInner)
        {
            builder.AppendLine($$"""
                                     public {{property.Type}} {{property.Name}}
                                     {
                                         get;
                                         set
                                         {
                                             field = value;
                                             OnPropertyChanged();
                                         }
                                     } = {{className}}.{{(property.IsFallbackToSelf ? $"Fallback{(property.LangParamType != null ? $"?.{property.LangParamType}?." : "?.")}{property.Name}{(defaultValue != "default" ? $" ?? {defaultValue}" : "")}" : defaultValue)}};

                                 """);
            return;
        }

        builder.AppendLine($$"""
                                 public {{property.Type}} {{property.Name}}
                                 {
                                     get;
                                     set
                                     {
                                         field = value;
                                         OnPropertyChanged();
                                     }
                                 } = {{(property.IsFallbackToSelf ? $"Fallback{(property.LangParamType != null ? $"?.{property.LangParamType}?." : "?.")}{property.Name}{(defaultValue != "default" ? $" ?? {defaultValue}" : "")}" : defaultValue)}};

                             """);
    }
}
