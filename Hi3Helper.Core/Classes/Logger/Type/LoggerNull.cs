using System.Text;
// ReSharper disable MethodOverloadWithOptionalParameter
// ReSharper disable CheckNamespace

#nullable enable
namespace Hi3Helper;

public class LoggerNull : LoggerBase
{
    public LoggerNull(string folderPath, Encoding encoding) : base(folderPath, encoding) { }
    internal LoggerNull() { }
}
