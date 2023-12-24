using CollapseLauncher.FileDialogCOM;
using Hi3Helper;
using Hi3Helper.EncTool;
using Hi3Helper.UABT.Binary;
using Microsoft.Win32;
using System;
using System.Buffers;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using static CollapseLauncher.GameSettings.Base.SettingsBase;
using static Hi3Helper.Locale;

namespace CollapseLauncher.GameSettings.Base
{
    internal class ImportExportBase
    {
        private const byte xorKey = 69;
        public Exception ImportSettings()
        {
            try
            {
                string path = FileDialogNative.GetFilePicker(new Dictionary<string, string> { { "Collapse Registry", "*.clreg" } }, Lang._GameSettingsPage.SettingsRegImportTitle).GetAwaiter().GetResult();

                if (string.IsNullOrEmpty(path)) throw new OperationCanceledException(Lang._GameSettingsPage.SettingsRegErr1);

                using (FileStream fs = new FileStream(path, FileMode.Open, FileAccess.Read))
                {
                    byte[] head = new byte[6];
                    fs.Read(head, 0, head.Length);

                    Logger.LogWriteLine($"Importing registry {RegistryPath}...");

                    string header = Encoding.UTF8.GetString(head);
                    switch (header)
                    {
                        case "clReg\0":
                            ReadLegacyValues(fs);
                            break;
                        case "ColReg":
                            ReadNewerValues(fs);
                            break;
                        default:
                            throw new FormatException(Lang._GameSettingsPage.SettingsRegErr2);
                    }
                }
            }
            catch (Exception ex)
            {
                return ex;
            }

            return null;
        }

        private void ReadNewerValues(Stream fs)
        {
            byte version = (byte)fs.ReadByte();

            switch (version)
            {
                case 1:
                    using (XORStream xorS = new XORStream(fs, xorKey, true))
                    using (BrotliStream comp = new BrotliStream(xorS, CompressionMode.Decompress, true))
                    {
                        ReadLegacyValues(comp);
                    }
                    break;
                case 2:
                    ReadV2Values(fs);
                    break;
                default:
                    throw new FormatException($"Registry version is not supported! Read: {version}");
            }
        }

        private void ReadLegacyValues(Stream fs)
        {
            using (EndianBinaryReader reader = new EndianBinaryReader(fs, Hi3Helper.UABT.EndianType.BigEndian, true))
            {
                short count = reader.ReadInt16();
                Logger.LogWriteLine($"File has {count} values.");
                while (count-- > 0)
                {
                    string valueName = ReadValueName(reader);
                    byte type = reader.ReadByte();
                    switch (type)
                    {
                        case 0:
                            ReadDWord(reader, valueName); break;
                        case 1:
                            ReadQWord(reader, valueName); break;
                        case 2:
                            ReadString(reader, valueName); break;
                        case 3:
                            ReadBinary(reader, valueName); break;
                    }
                    Logger.LogWriteLine($"Value {valueName} imported!");
                }
            }
        }

        private void ReadV2Values(Stream fs)
        {
            bool isCompressed = fs.ReadByte() > 0;

            Logger.LogWriteLine($"Exporting registry V2 {RegistryPath}...");
            int count = ReadInt32(fs);
            using Stream stream = isCompressed ? new BrotliStream(fs, CompressionMode.Decompress) : fs;
            do
            {
                --count;
                string valueName = ReadValueName(stream);
                EnsureReadImpostorData(stream, valueName);
                Logger.LogWriteLine($"Value V2 {valueName} imported!");
            } while (count > 0);
        }

        public Exception ExportSettings(bool isCompressed = true)
        {
            try
            {
                string path = FileDialogNative.GetFileSavePicker(new Dictionary<string, string> { { "Collapse Registry", "*.clreg" } }, Lang._GameSettingsPage.SettingsRegExportTitle).GetAwaiter().GetResult();
                EnsureFileSaveHasExtension(ref path, ".clreg");

                if (string.IsNullOrEmpty(path)) throw new OperationCanceledException(Lang._GameSettingsPage.SettingsRegErr1);
                Logger.LogWriteLine($"Exporting registry V2 {RegistryPath}...");

                using FileStream fileStream = new FileStream(path, FileMode.Create, FileAccess.Write);
                using Stream writeStream = isCompressed ? new BrotliStream(fileStream, CompressionLevel.Optimal, true) : fileStream;

                fileStream.Write(Encoding.UTF8.GetBytes("ColReg"));
                fileStream.WriteByte(2);
                fileStream.WriteByte((byte)(isCompressed ? 1 : 0));

                string[] names = RegistryRoot.GetValueNames();
                Span<byte> lenByte = stackalloc byte[4];
                MemoryMarshal.Write(lenByte, names.Length);
                fileStream.Write(lenByte);

                foreach (string valueName in names)
                {
                    object val = RegistryRoot.GetValue(valueName);
                    RegistryValueKind valueType = RegistryRoot.GetValueKind(valueName);

                    Logger.LogWriteLine($"Writing value V2 {valueName}...");

                    WriteValueName(writeStream, valueName);
                    EnsureWriteImpostorData(writeStream, valueType, val);
                }

                Logger.LogWriteLine($"Registry V2 {RegistryPath} has been exported!");
            }
            catch (Exception ex)
            {
                return ex;
            }

            return null;
        }

        private unsafe void EnsureReadImpostorData(Stream stream, string valueName)
        {
            int sizeOf = ReadValueKindAndSize(stream, out RegistryValueKind realValueType);
            object result = realValueType switch
            {
                RegistryValueKind.QWord => ReadTypeNumberUnsafe(stream, sizeOf),
                RegistryValueKind.DWord => ReadTypeNumberUnsafe(stream, sizeOf),
                RegistryValueKind.String => ReadTypeString(stream, sizeOf),
                RegistryValueKind.Binary => WriteTypeBinary(stream, sizeOf),
                _ => throw new InvalidCastException($"Cast of the object cannot be determined!")
            };

            if (realValueType == RegistryValueKind.QWord && sizeOf == sizeof(int)
             || realValueType == RegistryValueKind.DWord && sizeOf == sizeof(long))
            {
                // Fix wrong casting of the number. Store the value as a binary type.
                byte[] numberAsBuffer = new byte[sizeOf];
                if (sizeOf == sizeof(int))
                    MemoryMarshal.Write(numberAsBuffer, (int)result);
                else
                    MemoryMarshal.Write(numberAsBuffer, (long)result);

                realValueType = RegistryValueKind.Binary;
                result = numberAsBuffer;
            }

            try
            {
                RegistryRoot.SetValue(valueName, result, realValueType);
            }
            catch
            {
                throw;
            }
        }

        private unsafe void EnsureWriteImpostorData(Stream stream, RegistryValueKind realValueType, object value)
        {
            Type valueType = value.GetType();
            if (valueType == typeof(int) || valueType == typeof(float))
            {
                int castValue = (int)value;
                WriteTypeNumberUnsafe(stream, ref castValue, 4, realValueType);
                return;
            }
            if (valueType == typeof(long) || valueType == typeof(double))
            {
                long castValue = (long)value;
                WriteTypeNumberUnsafe(stream, ref castValue, 8, realValueType);
                return;
            }
            if (valueType == typeof(string))
            {
                WriteTypeString(stream, (string)value, realValueType);
                return;
            }
            if (valueType == typeof(byte[]))
            {
                WriteTypeBinary(stream, (byte[])value, realValueType);
                return;
            }

            throw new InvalidCastException($"Cast of the object cannot be determined!");
        }

        private unsafe object ReadTypeNumberUnsafe(Stream stream, int sizeOf)
        {
            Span<byte> buffer = stackalloc byte[sizeOf];
            stream.ReadExactly(buffer);

            if (sizeOf == sizeof(int)) return MemoryMarshal.Read<int>(buffer);
            if (sizeOf == sizeof(long)) return MemoryMarshal.Read<long>(buffer);

            throw new InvalidCastException($"The type of the number type is unknown! Expecting size: {sizeOf}");
        }

        private unsafe void WriteTypeNumberUnsafe<T>(Stream stream, ref T value, int sizeOf, RegistryValueKind realValueType)
            where T : struct
        {
            ReadOnlySpan<byte> byteNumberAsSpan = new ReadOnlySpan<byte>((byte*)Unsafe.AsPointer<T>(ref value), sizeOf);
            WriteValueKindAndSize(stream, sizeOf, realValueType);
            WriteToStream(stream, byteNumberAsSpan);
        }

        private unsafe string ReadTypeString(Stream stream, int length)
        {
            bool isUsePool = length <= 64 << 10;
            byte[] buffer = isUsePool ? ArrayPool<byte>.Shared.Rent(length) : new byte[length];
            try
            {
                stream.ReadExactly(buffer, 0, length);
                string result = Encoding.UTF8.GetString(buffer, 0, length);
                return result;
            }
            finally
            {
                if (isUsePool) ArrayPool<byte>.Shared.Return(buffer);
            }
        }

        private unsafe void WriteTypeString(Stream stream, ReadOnlySpan<char> stringSpan, RegistryValueKind realValueType)
        {
            bool isUsePool = stringSpan.Length <= 64 << 10;
            byte[] buffer = isUsePool ? ArrayPool<byte>.Shared.Rent(stringSpan.Length) : new byte[stringSpan.Length];
            try
            {
                int writtenLen = Encoding.UTF8.GetBytes(stringSpan, buffer);
                WriteValueKindAndSize(stream, writtenLen, realValueType);
                WriteToStream(stream, buffer.AsSpan(0, writtenLen));
            }
            finally
            {
                if (isUsePool) ArrayPool<byte>.Shared.Return(buffer);
            }
        }

        private byte[] WriteTypeBinary(Stream stream, int length)
        {
            byte[] buffer = new byte[length];
            stream.ReadExactly(buffer);
            return buffer;
        }

        private void WriteTypeBinary(Stream stream, ReadOnlySpan<byte> buffer, RegistryValueKind realValueType)
        {
            WriteValueKindAndSize(stream, buffer.Length, realValueType);
            WriteToStream(stream, buffer);
        }

        private void WriteValueKindAndSize(Stream stream, int sizeOf, RegistryValueKind kind)
        {
            Span<byte> lenBuffer = stackalloc byte[5];
            lenBuffer[0] = (byte)kind;
            BinaryPrimitives.WriteInt32LittleEndian(lenBuffer.Slice(1), sizeOf);
            stream.Write(lenBuffer);
        }

        private int ReadValueKindAndSize(Stream stream, out RegistryValueKind kind)
        {
            Span<byte> lenBuffer = stackalloc byte[5];
            stream.ReadExactly(lenBuffer);
            kind = (RegistryValueKind)lenBuffer[0];
            int sizeOf = MemoryMarshal.Read<int>(lenBuffer.Slice(1));
            return sizeOf;
        }

        private void WriteToStream(Stream stream, ReadOnlySpan<byte> buffer) => stream.Write(buffer);

        private void EnsureFileSaveHasExtension(ref string path, string exte)
        {
            if (string.IsNullOrEmpty(path)) return;
            string ext = Path.GetExtension(path);
            if (ext == null) return;
            if (string.IsNullOrEmpty(ext))
            {
                path += exte;
            }
        }

        private void WriteValueName(Stream stream, string name)
        {
            byte[] buffer = ArrayPool<byte>.Shared.Rent(4 << 10);
            try
            {
                int writtenData = Encoding.UTF8.GetBytes(name, buffer.AsSpan(2));
                BinaryPrimitives.WriteUInt16LittleEndian(buffer, (ushort)writtenData);
                stream.Write(buffer, 0, writtenData + 2);
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(buffer);
            }
        }

        private string ReadValueName(Stream stream)
        {
            short length = ReadInt16(stream);
            byte[] buffer = ArrayPool<byte>.Shared.Rent(length);
            try
            {
                stream.Read(buffer, 0, length);
                return Encoding.UTF8.GetString(buffer, 0, length);
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(buffer);
            }
        }

        private long ReadInt64(Stream stream)
        {
            Span<byte> buffer = stackalloc byte[4];
            stream.Read(buffer);
            return MemoryMarshal.Read<long>(buffer);
        }

        private int ReadInt32(Stream stream)
        {
            Span<byte> buffer = stackalloc byte[4];
            stream.Read(buffer);
            return MemoryMarshal.Read<int>(buffer);
        }

        private short ReadInt16(Stream stream)
        {
            Span<byte> buffer = stackalloc byte[2];
            stream.Read(buffer);
            return MemoryMarshal.Read<short>(buffer);
        }

        private string ReadValueName(EndianBinaryReader reader) => reader.ReadString8BitLength();

        private void ReadDWord(EndianBinaryReader reader, string valueName)
        {
            int val = reader.ReadInt32();
            RegistryRoot.SetValue(valueName, val, RegistryValueKind.DWord);
        }

        private void ReadQWord(EndianBinaryReader reader, string valueName)
        {
            long val = reader.ReadInt64();
            RegistryRoot.SetValue(valueName, val, RegistryValueKind.QWord);
        }

        private void ReadString(EndianBinaryReader reader, string valueName)
        {
            string val = reader.ReadString8BitLength();
            RegistryRoot.SetValue(valueName, val, RegistryValueKind.String);
        }

        private void ReadBinary(EndianBinaryReader reader, string valueName)
        {
            int leng = reader.ReadInt32();
            byte[] val = new byte[leng];
            reader.Read(val, 0, leng);
            RegistryRoot.SetValue(valueName, val, RegistryValueKind.Binary);
        }
    }
}
