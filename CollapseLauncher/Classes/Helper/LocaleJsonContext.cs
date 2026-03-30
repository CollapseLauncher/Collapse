using Hi3Helper.LocaleSourceGen;
using System.Text.Json.Serialization;

#pragma warning disable IDE0130

namespace CollapseLauncher.Helper;

[JsonSerializable(typeof(LangParams))]
[JsonSerializable(typeof(LangParamsBase))]
internal partial class LocaleJsonContext : JsonSerializerContext;
