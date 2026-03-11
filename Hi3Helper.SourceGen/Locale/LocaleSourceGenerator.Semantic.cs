using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Text.Json;
// ReSharper disable ForeachCanBePartlyConvertedToQueryUsingAnotherGetEnumerator

namespace Hi3Helper.SourceGen.Locale;

public partial class LocaleSourceGenerator
{
    private static JsonSourceGenerationContext GetSemanticTargetForGeneration(GeneratorAttributeSyntaxContext ctx)
    {
        string? jsonFilePath = "";

        ClassDeclarationSyntax? classSyntax = ctx.TargetNode as ClassDeclarationSyntax;
        NamespaceDeclarationSyntax? namespaceSyntax = classSyntax?.Parent as NamespaceDeclarationSyntax;
        FileScopedNamespaceDeclarationSyntax? fileNamespaceSyntax = classSyntax?.Parent as FileScopedNamespaceDeclarationSyntax;
        Location? location = classSyntax?.GetLocation();

        string? sourceParentFile = location?.SourceTree?.FilePath;

        string? @namespace = TryGetNamespaceFromSyntax(namespaceSyntax?.Name) ??
                             TryGetNamespaceFromSyntax(fileNamespaceSyntax?.Name);
        string? className = classSyntax?.Identifier.ValueText;
        string? classModifier = classSyntax?.Modifiers.ToString();

        Diagnostic diagnostic;
        if (classSyntax == null)
        {
            diagnostic = Diagnostic.Create(new DiagnosticDescriptor(
                "CL003",
                "Localization class cannot be determined 1",
                "Localization class cannot be determined. 1",
                className ?? "",
                DiagnosticSeverity.Error,
                true),
                location,
                classSyntax?.Identifier.ValueText);
            return new JsonSourceGenerationContext(null!, null, null, null, diagnostic);
        }

        bool isPartial = classSyntax.Modifiers.Any(modifier => modifier.IsKind(SyntaxKind.PartialKeyword));

        if (!isPartial)
        {
            diagnostic = Diagnostic.Create(new DiagnosticDescriptor(
                "CL001",
                "Localization class has no partial modifier",
                "Localization class '{0}' must have partial modifier in order to use this source-generated map.",
                "Generator",
                DiagnosticSeverity.Error,
                true),
                location,
                classSyntax.Identifier.ValueText);
            return new JsonSourceGenerationContext(null!, null, null, null, diagnostic);
        }

        foreach (AttributeData attribute in ctx.TargetSymbol.GetAttributes())
        {
            if (attribute.AttributeClass?.Name != "LocaleSourceGeneratedAttribute" ||
                attribute.AttributeClass.ToDisplayString() != AttributeName)
            {
                continue;
            }

            ImmutableArray<KeyValuePair<string, TypedConstant>> args = attribute.NamedArguments;
            foreach (KeyValuePair<string, TypedConstant> arg in args)
            {
                string argName = arg.Key;
                string? value = arg.Value.Value?.ToString();

                if (argName == "LocalePath")
                {
                    jsonFilePath = value;
                }
            }
        }

        if (string.IsNullOrEmpty(jsonFilePath))
        {
            foreach (AttributeSyntax attributeSyntax in classSyntax.AttributeLists.SelectMany(attributeListSyntax => attributeListSyntax.Attributes))
            {
                if (attributeSyntax.ArgumentList == null)
                {
                    continue;
                }

                foreach (AttributeArgumentSyntax argument in attributeSyntax.ArgumentList.Arguments)
                {
                    string? key = argument.NameEquals?.Name.ToString();
                    if (key == "LocalePath")
                    {
                        jsonFilePath = (argument.Expression as LiteralExpressionSyntax)?.Token.ValueText;
                    }
                }
            }
        }

        if (string.IsNullOrEmpty(jsonFilePath) ||
            string.IsNullOrEmpty(@namespace) ||
            string.IsNullOrEmpty(className) ||
            string.IsNullOrEmpty(classModifier))
        {
            diagnostic = Diagnostic.Create(new DiagnosticDescriptor(
                "CL004",
                "Localization class cannot be determined 2",
                $"Localization class cannot be determined. 2 jsonFilePath: {jsonFilePath} @namespace: {@namespace} ({fileNamespaceSyntax?.Name.GetType().Name}) className: {className} classModifier: {classModifier}",
                "Generator",
                DiagnosticSeverity.Error,
                true),
                location);
            return new JsonSourceGenerationContext(null!, null, null, null, diagnostic);
        }

        try
        {
            string? absolutePath = FindLocaleActualPath(jsonFilePath, sourceParentFile);
            if (string.IsNullOrEmpty(absolutePath))
            {
                diagnostic = Diagnostic.Create(new DiagnosticDescriptor(
                    "CL003",
                    "Cannot find absolute path of the locale file!",
                    "The absolute path of the locale file cannot be found!",
                    "Generator",
                    DiagnosticSeverity.Error,
                    true),
                location);
                return new JsonSourceGenerationContext(null!, null, null, null, diagnostic);
            }

            if (!File.Exists(absolutePath))
            {
                diagnostic = Diagnostic.Create(new DiagnosticDescriptor(
                    "CL002",
                    "Localization source file doesn't exist",
                    "Localization source file '{0}' {1} cannot be found!",
                    "Generator",
                    DiagnosticSeverity.Error,
                    true),
                location, jsonFilePath, absolutePath);
                return new JsonSourceGenerationContext(null!, null, null, null, diagnostic);
            }

            using FileStream jsonFileStream = File.OpenRead(absolutePath);
            return new JsonSourceGenerationContext(JsonDocument.Parse(jsonFileStream), @namespace, className, classModifier, null);
        }
        catch (Exception ex)
        {
            diagnostic = Diagnostic.Create(new DiagnosticDescriptor(
                "CL000",
                "An unknown error has occurred!",
                "The locale source generator has experienced an unexpected error! {0}",
                "Generator",
                DiagnosticSeverity.Error,
                true),
            location, ex);
            return new JsonSourceGenerationContext(null!, null, null, null, diagnostic);
        }
    }

    private static string? FindLocaleActualPath(string? filePath, string? sourceParentFile)
    {
        if (sourceParentFile == null ||
            filePath == null ||
            Path.IsPathRooted(filePath))
        {
            return filePath;
        }

    RecurseBacktrack:
        sourceParentFile = Path.GetDirectoryName(sourceParentFile);
        if (string.IsNullOrEmpty(sourceParentFile))
        {
            return filePath;
        }

        string checkPath = Path.Combine(sourceParentFile, filePath);
        if (File.Exists(checkPath))
        {
            return checkPath;
        }
        goto RecurseBacktrack;
    }

    private static string? TryGetNamespaceFromSyntax(SyntaxNode? syntax)
    {
        return syntax switch
        {
            QualifiedNameSyntax qualifiedNameSyntax when qualifiedNameSyntax.ToString() is var fromQualifiedNameSyntax => fromQualifiedNameSyntax,
            IdentifierNameSyntax identifierNameSyntax when identifierNameSyntax.ToString() is var fromIdentifierNameSyntax => fromIdentifierNameSyntax,
            _ => null
        };
    }
}
