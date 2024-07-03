using System;
using System.IO;
using System.Linq;
using static System.Text.Encoding;

namespace CollapseLauncher.GameSettings.Zenless;

public static class Decode
{ 
    public static byte[] RunDecode(string inputFile, byte[] magic)
    {
        if (!File.Exists(inputFile)) throw new FileNotFoundException();
        byte[] raw = File.ReadAllBytes(inputFile).Skip(24).ToArray();

        byte[] result = new byte[raw.Length];
        for (int i = 0; i < raw.Length; i++)
        {
            result[i] = (byte)(raw[i] ^ magic[i % magic.Length]);
        }

        for (int i = 0; i < result.Length; i++)
        {
            if (result[i] == (byte)'!' && i + 1 < result.Length)
            {
                result[i]     =  0;
                result[i + 1] += 0x40;
            }
        }

        result = result.Where(b => b != 0x0 && b != 0x20).ToArray();
        return result;
    }

    public static byte[] RunEncode(byte[] input, byte[] magic)
    {
        if (input == null || input.Length == 0) throw new NullReferenceException();

        byte[] result = new byte[input.Length];
        for (int i = 0; i < input.Length; i++)
        {
            result[i] = (byte)(input[i] ^ magic[i % magic.Length]);
        }

        return result;
    }

    public static string DecodeToString(string inputFile, byte[] magic)
    {
        var a = RunDecode(inputFile, magic);
        var d  = UTF8.GetString(a);
        
        // trim last char
        return d.Length > 0 ? d.Substring(0, d.Length - 1) : d;
    }
}