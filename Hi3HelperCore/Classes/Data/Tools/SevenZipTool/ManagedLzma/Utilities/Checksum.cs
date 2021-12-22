using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ManagedLzma
{
    /// <summary>CRC checksum used by 7z archives.</summary>
    public struct Checksum : IEquatable<Checksum>
    {
        public static Checksum GetEmptyStreamChecksum()
        {
            return new Checksum((int)CRC.Finish(CRC.kInitCRC));
        }

        public static Checksum Parse(string text)
        {
            return new Checksum(Int32.Parse(text, NumberStyles.AllowHexSpecifier));
        }

        public static Checksum Parse(string text, IFormatProvider provider)
        {
            return new Checksum(Int32.Parse(text, NumberStyles.AllowHexSpecifier, provider));
        }

        public readonly int Value;

        public Checksum(int value)
        {
            this.Value = value;
        }

        public override string ToString()
        {
            return Value.ToString("x8");
        }

        public string ToString(IFormatProvider provider)
        {
            return Value.ToString("x8", provider);
        }

        public override int GetHashCode()
        {
            return Value;
        }

        public override bool Equals(object obj)
        {
            return obj is Checksum && ((Checksum)obj).Value == Value;
        }

        public bool Equals(Checksum other)
        {
            return Value == other.Value;
        }

        public static bool operator ==(Checksum left, Checksum right)
        {
            return left.Value == right.Value;
        }

        public static bool operator !=(Checksum left, Checksum right)
        {
            return left.Value != right.Value;
        }
    }
}
