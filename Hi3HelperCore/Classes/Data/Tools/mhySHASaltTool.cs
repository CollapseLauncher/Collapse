using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using System.Xml;

namespace Hi3Helper.Data
{
    public class mhySHASaltTool
    {
        protected string _778;
        protected HMACSHA1 sha;
        protected RSA _ooh;
        protected const string _ = @"D4F8D4A6F6CF9AD4E78FF4E1D696CA88D9DF8FF7F5DDF3DC87E1D08A8598A9D1DED7D4DD81C2EC938FECB0E8F493B2F4B2DBD39FC99CA2FBCCD3DBE190E2F494EEDEBAE7F5A0C7F1BDE5EAA3FFD888B2E89BD1D0B198F5D0D2E8D7D8C5DAF5C080E2FD8AFC9CD5CFE3D6B7F383EEB3D0E79985C1C3A1E7C8AED2EAA2EBCEB6C1F0AEB0D6A1ECE3B6C5FBA0C0B1A0CCE5AEFDDFAFD2FC85C0EEADE2C0DE9AC893E5EED4B3F0A5F4F7AAE6EE88DC9CABC6CE81CAEFD0E6E380EACEB5B3B6ACD6E5AD97BBC8F0C587F7EA96F2BAD4EFFF97D2C486ECF2DDC4D5D597BBC8F8D293EDE886EFF0D696D7D98C9CA4EBF191E6C7AFDCE4BEDFCDB0D8C9A1CBD6909EC0DED6C38CC5E186F9C8BBD3D0D18FC3A8B6C396D5B4B0DFBE9FC4CC8FC0C282C2B091C5E8A1D6D291E4EA92C4C5A1C3C281E9DBADC5EABAE4CE83D9ACD2CCDDDEBFBACCD1BAD4FBB99DDBFBBAB6E2B4E8ABACC6E8D485D3D5FAF0BBB5F4BDEFC68D8CC281E7C08CAED581D3B0AF8D9AB3B2BEDAD6B4A3E9B1AAD9C3C8C5DFD7D4F1BAEDCD93DAF2D4D7DC85C6D58CE2D58D85C7A1E8EF89C7FD80FFEBD3CA97DEBEA9B2BFB8ACFAB984D4C28FCDE389F8C083CBD283D3D88DF6C3BBF7C0D8F9F1978ADC8FD7BF87FBCED0C3F2A9C781ACEDCC87D4C983E1CF8EECDEDBFBF092B9C89E99C0938DDAD1DAF5D1B1FC9BF3BE90F9C2B0EBB4BBE7E5D1CEEEB4CA97DEBEA9A7D1BAD4EED6D9FAEE91D4F1A7E5C5BFCFEE8E8AF980EEC5A5EAF7BC9CB6ABE5E98AC6C9D2D9F4BDD2D5B4ED8191FBFCB9AEF7AFE3F38FF0DD93F5F2D4DBE791C4F6AECFEBA0B2CCD4E4C59EE2CAADD0E2AAD2D686E0F0DA9EE8A2D9FBDEBFBACCC5D5D696CE89CBCF91F1E3B2BFD4DC85F180CC9BBBC4B1D4CFBCA385C89FE9C880E8D1A5C2C7D9CEAC918EFDD6FAD1B2CBE685FFB4ACDBFCA5B0F1ACD2F0B2CFCEABE998BBB3B490B6C5A0EEAC92E8CCADBBC9CCF9E0ADE8CE82CCC9B0CED09BD6EE9F97BADB92E38DF4E391F2E1B994BBA383C5D3D5F0B9C2FC9EE7E0B0D8CB97F0FF81C3F2A1DCEBB0EC9ABBB4C5AFC7DDB1CFB5928998A4DAF6AACDEF8ADAB4B7FBDF99CAF08DEAF19FC3F7C8C4EDD0B7D48FEDD7DACFE1A0D7CCABC7B68BE6C5DBC9E69DCFE484CFB084C3C08BECE6D0D4D08DF4EFAAE8D5BCD3D49FFBDF99FBB58ED9F482F9D6A1DCED84E6F08EF4D1B9E6E08088DB82EAE587C6E3BC9EB2D2FBD08CB7C1B0F4AFA1D3D397F98187CCE9A8F8B3D1E4CBB3DFDC89C7BBDFAEC0D696A8B5EEEBA8E7FFB5E0E89DCFB9";

        protected readonly Dictionary<char, byte> __951 = new Dictionary<char, byte>()
        {
            { 'a', 0xA },{ 'b', 0xB },{ 'c', 0xC },{ 'd', 0xD },
            { 'e', 0xE },{ 'f', 0xF },{ 'A', 0xA },{ 'B', 0xB },
            { 'C', 0xC },{ 'D', 0xD },{ 'E', 0xE },{ 'F', 0xF },
            { '0', 0x0 },{ '1', 0x1 },{ '2', 0x2 },{ '3', 0x3 },
            { '4', 0x4 },{ '5', 0x5 },{ '6', 0x6 },{ '7', 0x7 },
            { '8', 0x8 },{ '9', 0x9 }
        };

        protected readonly byte[] sKey = new byte[12]
        {
            232, 170, 135, 231,
            189, 170, 227, 130,
            134, 227, 129, 132
        };

        public mhySHASaltTool(string _i)
        {
            this._778 = _i;
            this._ooh = RSA.Create();
        }

        public byte[] GetSalt()
        {
            byte[] cy_a = default(byte[]);
            byte[] hR = default(byte[]);
            byte[] _0041 = default(byte[]);
            byte[] at = default(byte[]);
            while (true)
            {
                int num = -379967069;
                while (true)
                {
                    uint num2;
                    switch ((num2 = (uint)num ^ 0xD91CA180u) % 8u)
                    {
                        case 0u:
                            break;
                        case 3u:
                            cy_a = new byte[8];
                            num = (int)((num2 * 265579718) ^ 0x2AB6A14D);
                            continue;
                        case 2u:
                            num = (int)((num2 * 1194515468) ^ 0x6BE031F1);
                            continue;
                        case 5u:
                            hR = cy_a;
                            num = ((int)num2 * -1066675674) ^ 0xD7A97C;
                            continue;
                        case 6u:
                            FromXmlStringA(in _ooh, Encoding.UTF8.GetString(_0041));
                            num = (int)(num2 * 1342003605) ^ -355963254;
                            continue;
                        case 4u:
                            Array.Copy(_ooh.Decrypt(at, RSAEncryptionPadding.Pkcs1), 48, cy_a, 0, 8);
                            num = ((int)num2 * -1711149688) ^ -181350819;
                            continue;
                        case 7u:
                            _0041 = _f8j51(_);
                            at = HTb(_778);
                            num = ((int)num2 * -1995578406) ^ 0x57CB1D8;
                            continue;
                        default:
                            return hR;
                    }
                    break;
                }
            }
        }

        private void FromXmlStringA(in RSA rsa, string xmlString)
        {
            RSAParameters parameters = new RSAParameters();
            XmlDocument xmlDoc = new XmlDocument();
            xmlDoc.LoadXml(xmlString);
            if (xmlDoc.DocumentElement.Name.Equals("RSAKeyValue"))
            {
                foreach (XmlNode node in xmlDoc.DocumentElement.ChildNodes)
                {
                    switch (node.Name)
                    {
                        case "Modulus": parameters.Modulus = Convert.FromBase64String(node.InnerText); break;
                        case "Exponent": parameters.Exponent = Convert.FromBase64String(node.InnerText); break;
                        case "P": parameters.P = Convert.FromBase64String(node.InnerText); break;
                        case "Q": parameters.Q = Convert.FromBase64String(node.InnerText); break;
                        case "DP": parameters.DP = Convert.FromBase64String(node.InnerText); break;
                        case "DQ": parameters.DQ = Convert.FromBase64String(node.InnerText); break;
                        case "InverseQ": parameters.InverseQ = Convert.FromBase64String(node.InnerText); break;
                        case "D": parameters.D = Convert.FromBase64String(node.InnerText); break;
                    }
                }
            }
            else
            {
                throw new Exception("Invalid XML RSA key.");
            }

            rsa.ImportParameters(parameters);
        }

        private byte[] HTb(string _a)
        {
            byte[] _p49 = new byte[_a.Length / 2];
            bool f = default(bool);
            int n_94 = default(int);
            int _001 = default(int);
            while (true)
            {
                int kk1 = -1675277297;
                while (true)
                {
                    uint lo_051;
                    switch ((lo_051 = (uint)kk1 ^ 0x8D7A7B5Fu) % 9u)
                    {
                        case 2u:
                            break;
                        case 5u:
                            {
                                int _0051;
                                if (!f)
                                {
                                    _0051 = -1380326733;
                                }
                                else
                                {
                                    _0051 = -189009401;
                                }
                                kk1 = _0051 ^ (int)(lo_051 * 2073484105);
                                continue;
                            }
                        case 3u:
                            n_94 = 0;
                            kk1 = (int)((lo_051 * 2059650746) ^ 0x70BDC4E2);
                            continue;
                        case 8u:
                            {
                                char c = _a[n_94];
                                char c2 = _a[n_94 + 1];
                                _p49[_001] = (byte)((__951[c] << 4) | __951[c2]);
                                kk1 = (int)((lo_051 * 1839013216) ^ 0x5C090E6D);
                                continue;
                            }
                        case 1u:
                            _001 = 0;
                            kk1 = (int)((lo_051 * 316152874) ^ 0x4220ABE6);
                            continue;
                        case 6u:
                            f = n_94 < _a.Length;
                            kk1 = -7386813;
                            continue;
                        case 0u:
                            n_94 += 2;
                            _001++;
                            kk1 = (int)((lo_051 * 196220108) ^ 0x5B6DC890);
                            continue;
                        case 7u:
                            kk1 = -742839246;
                            continue;
                        default:
                            return _p49;
                    }
                    break;
                }
            }
        }

        private byte[] _f8j51(string c)
        {
            byte[] ar_84 = new byte[c.Length / 2];
            int _445 = default(int);
            int nud_e = default(int);
            byte[] r = default(byte[]);
            while (true)
            {
                int _Kj9a = -415293042;
                while (true)
                {
                    uint _99Jm1;
                    switch ((_99Jm1 = (uint)_Kj9a ^ 0xD88AD053u) % 8u)
                    {
                        case 3u:
                            break;
                        case 5u:
                            _445 = 0;
                            nud_e = 0;
                            _Kj9a = ((int)_99Jm1 * -1420181188) ^ 0x2336EBD;
                            continue;
                        case 2u:
                            _Kj9a = ((int)_99Jm1 * -336838899) ^ -1483897312;
                            continue;
                        case 0u:
                            nud_e++;
                            _Kj9a = (int)(_99Jm1 * 1698348363) ^ -1786813222;
                            continue;
                        case 4u:
                            r = ar_84;
                            _Kj9a = (int)(_99Jm1 * 1245963323) ^ -23762559;
                            continue;
                        case 1u:
                            {
                                if (_445 >= c.Length)
                                {
                                    _Kj9a = -481382921;
                                }
                                else
                                {
                                    _Kj9a = -1714879556;
                                }
                                continue;
                            }
                        case 7u:
                            {
                                byte b = (byte)((__951[c[_445]] << 4) | __951[c[_445 + 1]]);
                                ar_84[nud_e] = (byte)(b ^ sKey[nud_e % sKey.Length]);
                                _445 += 2;
                                _Kj9a = -1908881005;
                                continue;
                            }
                        default:
                            return r;
                    }
                    break;
                }
            }
        }
    }
}
