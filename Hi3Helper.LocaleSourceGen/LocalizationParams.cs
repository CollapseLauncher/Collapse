using Microsoft.UI.Xaml.Data;
using System;
using System.Collections.Generic;
using WinRT;

namespace Hi3Helper.LocaleSourceGen;

[LocaleSourceGenerated(LocalePath = @"..\Hi3Helper.Core\Lang\en_US.json")]
public sealed partial class LangParams;

[GeneratedBindableCustomProperty]
public partial class LangParamsBase : LangParamsBindingNotifier
{
    public string LanguageName
    {
        get;
        set
        {
            field = value;
            OnPropertyChanged();
        }
    } = "";

    public string LanguageID
    {
        get;
        set
        {
            field = value;
            OnPropertyChanged();
        }
    } = "";

    public string Author
    {
        get;
        set
        {
            field = value;
            OnPropertyChanged();
        }
    } = "Unknown";

    private static readonly Dictionary<string, BindableCustomProperty> LangParamsBaseLookups = new(StringComparer.OrdinalIgnoreCase)
    {
        {
            nameof(LanguageName), new BindableCustomProperty(true,
                                                             true,
                                                             nameof(LanguageName),
                                                             typeof(string),
                                                             static instance => ((LangParamsBase)instance).LanguageName,
                                                             static (instance, value) => ((LangParamsBase)instance).LanguageName = (string)value,
                                                             null,
                                                             null)
        },
        {
            nameof(LanguageID), new BindableCustomProperty(true,
                                                           true,
                                                           nameof(LanguageID),
                                                           typeof(string),
                                                           static instance => ((LangParamsBase)instance).LanguageID,
                                                           static (instance, value) => ((LangParamsBase)instance).Author = (string)value,
                                                           null,
                                                           null)
        },
        {
            nameof(Author), new BindableCustomProperty(true,
                                                       true,
                                                       nameof(Author),
                                                       typeof(string),
                                                       static instance => ((LangParamsBase)instance).Author,
                                                       static (instance, value) => ((LangParamsBase)instance).Author = (string)value,
                                                       null,
                                                       null)
        }
    };

    public override BindableCustomProperty GetProperty(string name) =>
        !LangParamsBaseLookups.TryGetValue(name, out BindableCustomProperty? property)
            ? throw ExceptionPropertyNotFound(name)
            : property;
}