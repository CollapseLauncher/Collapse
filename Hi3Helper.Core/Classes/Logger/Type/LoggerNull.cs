using System.Text;
// ReSharper disable MethodOverloadWithOptionalParameter
// ReSharper disable CheckNamespace

#nullable enable
namespace Hi3Helper;

public sealed class LoggerNull : LoggerBase
{
    public LoggerNull(string folderPath, Encoding encoding) : base(folderPath, encoding) { }
    internal LoggerNull() { }
}
