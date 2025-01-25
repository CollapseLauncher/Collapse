using Hi3Helper.EncTool.Parser.InnoUninstallerLog;
using LibISULR;
using LibISULR.Flags;
using LibISULR.Records;
using System;
using System.IO;
using System.Linq;

namespace InnoSetupHelper
{
    public enum InnoSetupLogType
    {
        Default, Warning, Error
    }

    public struct InnoSetupLogStruct
    {
        public InnoSetupLogType LogType;
        public bool IsWriteToLog;
        public string Message;
    }

    public class InnoSetupLogUpdate
    {
        public static event EventHandler<InnoSetupLogStruct>? LoggerEvent;

        private static readonly string[] ExcludeDeleteFile = [
            // Generic Files
#if DEBUG
            "ApplyUpdate.",
#else
            "ApplyUpdate.exe",
#endif
            "config.ini",
            "_Temp",
            "unins00",
            "unins000",

            // Game Files (we added this in-case the user uses the same directory as the executable)
            "Hi3SEA", "Hi3Global", "Hi3TW", "Hi3KR", "Hi3CN", "Hi3JP", "BH3",
            "GIGlb", "GICN", "GenshinImpact", "YuanShen", "GIBilibili",
            "SRGlb", "SRCN", "StarRail", "HSRBilibili",
            "ZZZGlb", "ZZZCN", "ZZZBilibili" // Adding ZZZ for Bilibili (if that even exist lol)
#if DEBUG
            // Hi3Helper.Http DLLs
            , "Hi3Helper.Http"
#endif
        ];

        public static void UpdateInnoSetupLog(string path)
        {
            string directoryPath = Path.GetDirectoryName(path)!;
            string searchValue = GetPathWithoutDriveLetter(directoryPath);

            LogWriteLine($"[InnoSetupLogUpdate::UpdateInnoSetupLog()] Updating Inno Setup file located at: {path}", InnoSetupLogType.Default, true);
            try
            {
                using InnoUninstallLog innoLog = InnoUninstallLog.Load(path, true);
                // Always set the log to x64 mode
                innoLog.Header.IsLog64Bit = true;

                // Clean up the existing file and directory records
                CleanUpInnoDirOrFilesRecord(innoLog, searchValue);

                // Try register the parent path
                RegisterDirOrFilesRecord(innoLog, directoryPath);

                // Save the Inno Setup log
                innoLog.Save(path);
                LogWriteLine($"[InnoSetupLogUpdate::UpdateInnoSetupLog()] Inno Setup file: {path} has been successfully updated!", InnoSetupLogType.Default, true);
            }
            catch (Exception ex)
            {
                LogWriteLine($"[InnoSetupLogUpdate::UpdateInnoSetupLog()] Inno Setup file: {path} was failed due to an error: {ex}", InnoSetupLogType.Warning, true);
            }
        }

        private static string GetPathWithoutDriveLetter(string path)
        {
            int firstIndexOf = path.IndexOf('\\');
            return firstIndexOf > -1 ? path[(firstIndexOf + 1)..] : path;
        }

        private static void RegisterDirOrFilesRecord(InnoUninstallLog innoLog, string pathToRegister)
        {
            DirectoryInfo currentDirectory = new DirectoryInfo(pathToRegister);
            if (innoLog.Records == null)
            {
                LogWriteLine("[InnoSetupLogUpdate::RegisterDirOrFilesRecord()] Records is uninitialized!", InnoSetupLogType.Error, true);
            }
            else
            {
                foreach (FileInfo fileInfo in currentDirectory.EnumerateFiles("*", SearchOption.TopDirectoryOnly))
                {
                    if (ExcludeDeleteFile.Any(x => x.IndexOf(fileInfo.FullName, StringComparison.OrdinalIgnoreCase) > -1)) continue;
                    fileInfo.IsReadOnly = false;
                    LogWriteLine($"[InnoSetupLogUpdate::RegisterDirOrFilesRecord()] " +
                                 $"Registering Inno Setup record: (DeleteFileRecord){fileInfo.FullName}", InnoSetupLogType.Default, true);
                    innoLog.Records.Add(DeleteFileRecord.Create(fileInfo.FullName));
                }
                LogWriteLine($"[InnoSetupLogUpdate::RegisterDirOrFilesRecord()] " +
                             $"Registering Inno Setup record: (DeleteDirOrFilesRecord){pathToRegister}", InnoSetupLogType.Default, true);
                innoLog.Records.Add(DeleteDirOrFilesRecord.Create(pathToRegister));
            }

            foreach (DirectoryInfo subdirectories in currentDirectory.EnumerateDirectories("*", SearchOption.TopDirectoryOnly))
            {
                RegisterDirOrFilesRecord(innoLog, subdirectories.FullName);
            }
        }

        private static void CleanUpInnoDirOrFilesRecord(InnoUninstallLog innoLog, string? searchValue)
        {
            int index = 0;
            do
            {
                if (innoLog.Records == null)
                {
                    LogWriteLine("[InnoSetupLogUpdate::RegisterDirOrFilesRecord()] Records is uninitialized!",
                                 InnoSetupLogType.Error, true);
                    return;
                }
                BaseRecord baseRecord = innoLog.Records[index];
                bool isRecordValid = false;
                switch (baseRecord.Type)
                {
                    case RecordType.DeleteDirOrFiles:
                        isRecordValid = IsInnoRecordContainsPath<DeleteDirOrFilesFlags>(baseRecord, searchValue)
                                     && IsDeleteDirOrFilesFlagsValid((DeleteDirOrFilesRecord)baseRecord);
                        LogWriteLine($"[InnoSetupLogUpdate::CleanUpInnoDirOrFilesRecord()] Removing outdated Inno Setup record: (DeleteDirOrFilesRecord){((DeleteDirOrFilesRecord)baseRecord).Paths[0]}", InnoSetupLogType.Default, true);
                        break;
                    case RecordType.DeleteFile:
                        isRecordValid = IsInnoRecordContainsPath<DeleteFileFlags>(baseRecord, searchValue)
                                     && IsDeleteFileFlagsValid((DeleteFileRecord)baseRecord);
                        LogWriteLine($"[InnoSetupLogUpdate::CleanUpInnoDirOrFilesRecord()] Removing outdated Inno Setup record: (DeleteFileRecord){((DeleteFileRecord)baseRecord).Paths[0]}", InnoSetupLogType.Default, true);
                        break;
                }
                if (isRecordValid)
                {
                    innoLog.Records.RemoveAt(index);
                    continue;
                }
                ++index;
            } while (index < innoLog.Records.Count);
        }

        private static bool IsDeleteDirOrFilesFlagsValid(DeleteDirOrFilesRecord record) => (record.Flags ^ (DeleteDirOrFilesFlags.IsDir | DeleteDirOrFilesFlags.DisableFsRedir)) == 0;
        private static bool IsDeleteFileFlagsValid(DeleteFileRecord record) => (record.Flags & DeleteFileFlags.DisableFsRedir) != 0;
        private static bool IsInnoRecordContainsPath<TFlags>(BaseRecord record, string? searchValue)
            where TFlags : Enum => ((BasePathListRecord<TFlags>)record)
            .Paths[0]!
            .IndexOf(searchValue!, StringComparison.OrdinalIgnoreCase) > -1;

        private static void LogWriteLine(string message, InnoSetupLogType logType, bool isWriteLog)
            => LoggerEvent?.Invoke(null, new InnoSetupLogStruct { IsWriteToLog = isWriteLog, LogType = logType, Message = message });
    }
}
