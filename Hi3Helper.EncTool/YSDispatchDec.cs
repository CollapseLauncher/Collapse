using System;
using System.IO;
using System.Security.Cryptography;

namespace Hi3Helper.EncTool
{
    public class YSDispatchDec : mhyEncTool
    {
        private protected RSAEncryptionPadding Padding;
        private protected int EncBitlength;

        public void InitYSDecoder(string PrivKey, RSAEncryptionPadding Padding = null, int EncBitlength = 0x100)
        {
            base._778 = PrivKey;
            base._ooh = RSA.Create();

            this.EncBitlength = EncBitlength;
            if (Padding == null)
            {
                this.Padding = RSAEncryptionPadding.Pkcs1;
                return;
            }

            this.Padding = Padding;
        }

        public void InitRSA() => base.FromXmlStringA(base._ooh);

        public byte[] DecryptYSDispatch(string ContentBase64)
        {
            byte[] EncContent = Convert.FromBase64String(ContentBase64);
            MemoryStream DecContent = new MemoryStream();

            int j = 0;

            while (j < EncContent.Length)
            {
                byte[] chunk = new byte[this.EncBitlength];
                Array.Copy(EncContent, j, chunk, 0, this.EncBitlength);
                byte[] chunkDec = base._ooh.Decrypt(chunk, this.Padding);
                DecContent.Write(chunkDec, 0, chunkDec.Length);
                j += this.EncBitlength;
            }

            return DecContent.ToArray();
        }
    }
}
