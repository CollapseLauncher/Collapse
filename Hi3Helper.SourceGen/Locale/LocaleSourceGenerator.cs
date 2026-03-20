using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Text.Json;
// ReSharper disable ForeachCanBePartlyConvertedToQueryUsingAnotherGetEnumerator Dog sh*t

#pragma warning disable RS1036 // Specify analyzer banned API enforcement setting
namespace Hi3Helper.SourceGen.Locale;

[Generator]
public sealed partial class LocaleSourceGenerator : IIncrementalGenerator
{
    private const string AttributeName = "Hi3Helper.LocaleSourceGen.LocaleSourceGeneratedAttribute";

    private static readonly string GeneratedCodeAttribute = $"[GeneratedCodeAttribute(\"{typeof(LocaleSourceGenerator).FullName}\", \"{typeof(LocaleSourceGenerator).Assembly.GetName().Version}\")]";

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        /*
        context.RegisterPostInitializationOutput(i =>
        {
            // Add the source code to the user's compilation
            i.AddSource("LocaleSourceGeneratedAttribute.g.cs", @"
namespace Hi3Helper.Locale;

[global::System.AttributeUsage(global::System.AttributeTargets.Class)]
public sealed partial class LocaleSourceGeneratedAttribute : global::System.Attribute
{
    public LocaleSourceGeneratedAttribute() { }
    public string? LocalePath { get; set; }
}");
        });
        */

        IncrementalValuesProvider<JsonSourceGenerationContext> localeJsonContent =
            context.SyntaxProvider.ForAttributeWithMetadataName(
                AttributeName,
                predicate: static (node, _) => node is ClassDeclarationSyntax,
                transform: static (ctx, _) => GetSemanticTargetForGeneration(ctx));

        context.RegisterSourceOutput(localeJsonContent, EmitSourceFile);
    }

    private sealed record PropertyContext(
        string  Name,
        string  Type,
        bool    IsFallbackToSelf,
        string? LangParamType = null,
        bool    IsInner       = false);

    private sealed record JsonSourceGenerationContext(
        JsonDocument Document,
        string?      Namespace,
        string?      ClassName,
        string?      Modifier,
        Diagnostic?  Diagnostic,
        bool         IsOnlyProduceBase = false);
}