using System.Text;
// ReSharper disable MethodOverloadWithOptionalParameter
// ReSharper disable CheckNamespace

namespace Hi3Helper.Log;

public sealed class LoggerNull : LoggerBase
{
    public LoggerNull(string folderPath, Encoding encoding) : base(folderPath, encoding) { }
    internal LoggerNull() { }
}
