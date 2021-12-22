
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace master._7zip.Legacy
{
    public interface IPasswordProvider
    {
        string CryptoGetTextPassword();
    }
}
