using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace Hi3Helper.EncTool
{
    public class mhyEncTool
    {
        public RSA _ooh;
        public string _778;
        protected HMACSHA1 sha;
        protected RSA _MasterKeyRSA;
        protected string _MasterKey = "";
        protected int _MasterKeyBitLength;
        protected RSAEncryptionPadding _MasterKeyPadding;

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

        public mhyEncTool() { }

        public mhyEncTool(string _i)
        {
            this._778 = _i;
            this._ooh = RSA.Create();
        }

        public mhyEncTool(string _i, string MasterKey)
        {
            this._778 = _i;
            this._ooh = RSA.Create();
            this._MasterKey = MasterKey;
        }

        public RSA GetMasterKeyRSA() => this._MasterKeyRSA;

        public void InitMasterKey(string Key, int KeyBitLength, RSAEncryptionPadding KeyPadding)
        {
            this._MasterKeyRSA = RSA.Create();
            this._MasterKey = Encoding.UTF8.GetString(this._f8j51(Key));
            this._MasterKeyBitLength = KeyBitLength;
            this._MasterKeyPadding = KeyPadding;
            this.FromXmlStringA(this._MasterKeyRSA, _MasterKey);
        }

        public void DecryptStringWithMasterKey(ref string a)
        {
            a = Encoding.UTF8.GetString(this._f8j51(a));
            byte[] buffer = DecryptRSAContent(this._MasterKeyRSA, a, this._MasterKeyBitLength, this._MasterKeyPadding);
            a = Encoding.UTF8.GetString(buffer);
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
                            _0041 = _f8j51(this._MasterKey);
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

        public void FromXmlStringA(in RSA rsa, string xmlString = null)
        {
            if (string.IsNullOrEmpty(xmlString)) xmlString = this._778;
            rsa.FromXmlString(xmlString);
        }

        public byte[] DecryptRSAContent(in RSA rsa, string ContentBase64, int EncBitLength, RSAEncryptionPadding Padding)
        {
            byte[] EncContent = Convert.FromBase64String(ContentBase64);
            MemoryStream DecContent = new MemoryStream();

            int j = 0;

            while (j < EncContent.Length)
            {
                byte[] chunk = new byte[EncBitLength];
                Array.Copy(EncContent, j, chunk, 0, EncBitLength);
                byte[] chunkDec = rsa.Decrypt(chunk, Padding);
                DecContent.Write(chunkDec, 0, chunkDec.Length);
                j += EncBitLength;
            }

            return DecContent.ToArray();
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

        public byte[] _f8j51(string c)
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
