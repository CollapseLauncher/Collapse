using System;
using System.IO;
using System.Linq;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Unicode;

namespace TLFixer;

public class Program
{
    private const string SourceFile = "en_us.json";

    public static void Main(params string[] args)
    {
        if (args.Length == 0)
        {
            throw new NullReferenceException();
        }

        string workDir = args[0];

        using FileStream stream = File.OpenRead(Path.Combine(workDir, SourceFile));
        JsonDocument sourceTl = JsonDocument.Parse(stream);

        foreach (string path in Directory.EnumerateFiles(workDir, "*.json")
                                         .Where(x => !x.EndsWith(SourceFile, StringComparison.OrdinalIgnoreCase)))
        {
            using FileStream targetStream = File.OpenRead(path);
            JsonDocument targetTl = JsonDocument.Parse(targetStream);

            FileInfo targetFixedFileInfo = new(Path.Combine(Path.GetDirectoryName(path) ?? "", "fixed", Path.GetFileName(path)));
            targetFixedFileInfo.Directory?.Create();

            using FileStream targetFixedStream = targetFixedFileInfo.Create();
            JsonWriterOptions targetWriterOpts = new()
            {
                Indented        = true,
                IndentCharacter = ' ',
                IndentSize      = 2,
                Encoder         = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
            };
            using StreamWriter targetStreamWriter = new(targetFixedStream);
            using Utf8JsonWriter targetJsonWriter = new(targetFixedStream, targetWriterOpts);

            Fix(sourceTl.RootElement, targetTl.RootElement, targetJsonWriter, targetStreamWriter);
        }
    }

    private static void Fix(JsonElement source, JsonElement target, Utf8JsonWriter writer, StreamWriter targetStreamWriter)
    {
        writer.WriteStartObject();

        foreach (JsonProperty element in source.EnumerateObject())
        {
            if (element.Value.ValueKind == JsonValueKind.String)
            {
                WritePropertyString(element.Name, target, writer);
                continue;
            }

            if (!target.TryGetProperty(element.Name, out JsonElement targetProperty))
            {
                continue;
            }

            writer.WriteStartObject(element.Name);
            CompareAndWriteTranslation(element.Value, targetProperty, writer);
            writer.WriteEndObject();
        }

        writer.WriteEndObject();
    }

    private static void CompareAndWriteTranslation(JsonElement sourceDict, JsonElement targetDict, Utf8JsonWriter writer)
    {
        foreach (JsonProperty targetProperty in targetDict.EnumerateObject())
        {
            if (!sourceDict.TryGetProperty(targetProperty.Name, out JsonElement sourceElement))
            {
                continue;
            }

            if (sourceElement.ValueKind == JsonValueKind.Object)
            {
                writer.WriteStartObject(targetProperty.Name);
                WriteCopyTarget(targetProperty.Value, writer);
                writer.WriteEndObject();
                continue;
            }

            string? sourceValue = sourceElement.GetString();
            string? targetValue = targetProperty.Value.GetString();

            if (sourceValue == targetValue)
            {
                continue;
            }

            writer.WriteString(targetProperty.Name, targetValue);
        }
    }

    private static void WriteCopyTarget(JsonElement targetDict, Utf8JsonWriter writer)
    {
        if (targetDict.ValueKind == JsonValueKind.Object)
        {
            foreach (JsonProperty targetProperty in targetDict.EnumerateObject())
            {
                writer.WriteStartArray(targetProperty.Name);
                WriteCopyTarget(targetProperty.Value, writer);
                writer.WriteEndArray();
            }
        }

        if (targetDict.ValueKind == JsonValueKind.Array)
        {
            foreach (JsonElement targetElement in targetDict.EnumerateArray())
            {
                writer.WriteStringValue(targetElement.GetString());
            }
        }
    }

    private static void WritePropertyString(string propertyName, JsonElement source, Utf8JsonWriter writer)
    {
        if (source.TryGetProperty(propertyName, out JsonElement element))
        {
            writer.WriteString(propertyName, element.GetString());
        }
    }
}