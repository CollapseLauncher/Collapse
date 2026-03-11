using System;

namespace Hi3Helper.LocaleSourceGen;

[AttributeUsage(AttributeTargets.Class)]
public sealed class LocaleSourceGeneratedAttribute : Attribute
{
    public string? LocalePath { get; set; }
}