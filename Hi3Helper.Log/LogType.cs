// ReSharper disable CheckNamespace
namespace Hi3Helper.Log;

public enum LogType
{
    Info    = 0,
    Default = Info,
    Warning,
    Error,
    Scheme,
    Game,
    Debug,
    GLC,
    Sentry,
    NoTag
}