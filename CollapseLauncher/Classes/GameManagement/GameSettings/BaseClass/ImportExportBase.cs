using Hi3Helper;
using Hi3Helper.EncTool;
using Hi3Helper.UABT.Binary;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;
using static CollapseLauncher.GameSettings.Statics;
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

                if (path == null) throw new OperationCanceledException(Lang._GameSettingsPage.SettingsRegErr1);

                using (FileStream fs = new FileStream(path, FileMode.Open, FileAccess.Read))
                {
                    byte[] head = new byte[6];
                    fs.Read(head, 0, head.Length);

                    if (Encoding.UTF8.GetString(head) != "clReg\0") throw new FormatException(Lang._GameSettingsPage.SettingsRegErr2);

                    Logger.LogWriteLine($"Importing registry {RegistryPath}...");

                    using (XORStream xorS = new XORStream(fs, xorKey, true))
                    using (BrotliStream comp = new BrotliStream(fs, CompressionMode.Decompress, true))
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
            }
            catch (Exception ex)
            {
                return ex;
            }

            return null;
        }

        public Exception ExportSettings()
        {
            try
            {
                string path = FileDialogNative.GetFileSavePicker(new Dictionary<string, string> { { "Collapse Registry", "*.clreg" } }, Lang._GameSettingsPage.SettingsRegExportTitle).GetAwaiter().GetResult();

                EnsureFileSaveHasExtension(ref path, ".clreg");

                if (path == null) throw new OperationCanceledException(Lang._GameSettingsPage.SettingsRegErr1);

                Logger.LogWriteLine($"Exporting registry {RegistryPath}...");

                using (FileStream fs = new FileStream(path, FileMode.Create, FileAccess.Write))
                {
                    fs.Write(Encoding.UTF8.GetBytes("clReg\0"));

                    using (XORStream xorS = new XORStream(fs, xorKey, true))
                    using (BrotliStream comp = new BrotliStream(fs, CompressionMode.Compress, true))
                    using (EndianBinaryWriter writer = new EndianBinaryWriter(fs, Hi3Helper.UABT.EndianType.BigEndian, true))
                    {
                        string[] names = RegistryRoot.GetValueNames();
                        writer.Write((short)names.Length);
                        foreach (string valueName in names)
                        {
                            object val = RegistryRoot.GetValue(valueName);
                            RegistryValueKind valueType = RegistryRoot.GetValueKind(valueName);

                            Logger.LogWriteLine($"Writing {valueName}...");
                            WriteValueName(writer, valueName);
                            switch (valueType)
                            {
                                case RegistryValueKind.DWord:
                                    WriteDWord(writer, val); break;
                                case RegistryValueKind.QWord:
                                    WriteQWord(writer, val); break;
                                case RegistryValueKind.String:
                                    WriteString(writer, val); break;
                                case RegistryValueKind.Binary:
                                    WriteBinary(writer, val); break;
                            }
                        }
                    }
                }

                Logger.LogWriteLine($"Registry {RegistryPath} has been exported!");
            }
            catch (Exception ex)
            {
                return ex;
            }

            return null;
        }

        private void EnsureFileSaveHasExtension(ref string path, string exte)
        {
            string ext = Path.GetExtension(path);
            if (ext == null) return;
            if (string.IsNullOrEmpty(ext))
            {
                path += exte;
            }
        }

        private void WriteValueName(EndianBinaryWriter writer, string name) => writer.Write(name);
        private string ReadValueName(EndianBinaryReader reader) => reader.ReadString8BitLength();

        private void WriteDWord(EndianBinaryWriter writer, object obj)
        {
            writer.Write((byte)0x00);
            writer.Write((int)obj);
        }

        private void ReadDWord(EndianBinaryReader reader, string valueName)
        {
            int val = reader.ReadInt32();
            RegistryRoot.SetValue(valueName, val, RegistryValueKind.DWord);
        }

        private void WriteQWord(EndianBinaryWriter writer, object obj)
        {
            writer.Write((byte)0x01);
            writer.Write((long)obj);
        }

        private void ReadQWord(EndianBinaryReader reader, string valueName)
        {
            long val = reader.ReadInt64();
            RegistryRoot.SetValue(valueName, val, RegistryValueKind.QWord);
        }

        private void WriteString(EndianBinaryWriter writer, object obj)
        {
            writer.Write((byte)0x02);
            writer.Write((string)obj);
        }

        private void ReadString(EndianBinaryReader reader, string valueName)
        {
            string val = reader.ReadString8BitLength();
            RegistryRoot.SetValue(valueName, val, RegistryValueKind.String);
        }

        private void WriteBinary(EndianBinaryWriter writer, object obj)
        {
            byte[] val = (byte[])obj;
            writer.Write((byte)0x03);
            writer.Write(val.Length);
            writer.Write(val);
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
