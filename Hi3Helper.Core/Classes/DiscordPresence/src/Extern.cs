#if !DISABLEDISCORD
using static Discord.Discord;
using System.Runtime.InteropServices;
using System;
using System.Text;

namespace Discord
{
    internal static class Extern
    {
        public const string DllName = "Lib\\discord_game_sdk";

        [DllImport(DllName, CallingConvention = CallingConvention.Winapi, ExactSpelling = true)]
        internal static extern Result DiscordCreate(UInt32 version, ref FFICreateParams createParams, out IntPtr manager);

        internal static string ReadUtf8Byte(this byte[] input) => input == null || input.Length == 0 ? string.Empty : Encoding.UTF8.GetString(input);

        internal static byte[] StrToByteUtf8(this string s, int len = 128)
        {
            // Use fixed width (provided by len or 128 bytes) as defined in field's SizeConst
            byte[] bufferOut = new byte[len];
            // Get the UTF-8 bytes (converting 16-bit (UTF-16) to 8-bit (UTF-8) char (as byte))
            Encoding.UTF8.GetBytes(s, bufferOut);
            // return the buffer
            return bufferOut;
        }
    }
}
#endif