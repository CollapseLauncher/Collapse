using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CollapseLauncher.ShellLinkCOM
{
    internal class CLSIDGuid
    {
        internal const string Id_ShellLinkClsId = "00021401-0000-0000-C000-000000000046";
        internal const string Id_ShellLinkIGuid = "000214F9-0000-0000-C000-000000000046";
        internal const string Id_IPersistFileIGuid = "0000010B-0000-0000-C000-000000000046";
        internal const string Id_IPersistIGuid = "0000010C-0000-0000-C000-000000000046";
        internal const string Id_IPropertyStoreIGuid = "886D8EEB-8CF2-4446-8D02-CDBA1DBDCF99";

        internal static readonly Guid ClsId_ShellLink = new Guid(Id_ShellLinkClsId);
        internal static readonly Guid IGuid_ShellLink = new Guid(Id_ShellLinkIGuid);
        internal static readonly Guid IGuid_IPersistFile = new Guid(Id_IPersistFileIGuid);
        internal static readonly Guid IGuid_IPersist = new Guid(Id_IPersistIGuid);
        internal static readonly Guid IGuid_IPropertyStore = new Guid(Id_IPropertyStoreIGuid);
    }
}
