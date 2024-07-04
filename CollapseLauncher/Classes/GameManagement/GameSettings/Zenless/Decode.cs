using System.Collections.Generic;
using System.IO;
using System.Linq;
using static System.Text.Encoding;

namespace CollapseLauncher.GameSettings.Zenless;

public static class Decode
{ 
    public static string RunDecode(byte[] input, byte[] magic)
    {
        using var mem = new MemoryStream(input);
        using var reader = new BinaryReader(mem);
        reader.ReadByte();
        reader.ReadInt32();
        reader.ReadInt32();
        reader.ReadInt32();
        reader.ReadInt32();
        reader.ReadByte();
        reader.ReadInt32();
        byte[] body = reader.ReadBytes(reader.Read7BitEncodedInt());

        bool[] evil = magic.ToList().ConvertAll(ch => (ch & 0xC0) == 0xC0).ToArray();
        var result = new List<byte>(body.Length);
        var sleepy = false;
        for (int i = 0; i < body.Length; i++)
        {
            int n = i % magic.Length;
            byte ch = (byte)(body[i] ^ magic[n]);
            if (evil[n])
            {
                if (ch != 0)
                {
                    sleepy = true;
                }
            }
            else
            {
                if (sleepy)
                {
                    ch += 0x40;
                    sleepy = false;
                }
                result.Add(ch);
            }
        }

        return UTF8.GetString(result.ToArray());
    }

    public static byte[] RunEncode(string input, byte[] magic)
    {
        byte[] plain = UTF8.GetBytes(input);
        bool[] evil = magic.ToList().ConvertAll(ch => (ch & 0xC0) == 0xC0).ToArray();

        var body = new List<byte>(plain.Length * 2);
        for (int i = 0, j = 0; j < plain.Length; i++, j++)
        {
            int n = i % magic.Length;
            if (evil[n])
            {
                byte ch = plain[j];
                byte sleepy = 0;
                if (plain[j] > 0x40)
                {
                    ch -= 0x40;
                    sleepy = 1;
                }
                body.Add((byte)(sleepy ^ magic[n]));
                i++;
                n = i % magic.Length;
                body.Add((byte)(ch ^ magic[n]));
                continue;
            }
            body.Add((byte)(plain[j] ^ magic[n]));
        }

        using var mem = new MemoryStream();
        using var writer = new BinaryWriter(mem);
        writer.Write((byte)0);
        writer.Write(1);
        writer.Write(-1);
        writer.Write(1);
        writer.Write(0);
        writer.Write((byte)6);
        writer.Write(1);
        writer.Write7BitEncodedInt(body.Count);
        writer.Write(body.ToArray());
        writer.Write((byte)0x0B);
        return mem.ToArray();
    }
}
