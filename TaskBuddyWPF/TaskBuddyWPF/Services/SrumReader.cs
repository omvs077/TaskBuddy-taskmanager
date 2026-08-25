using System;
using System.Collections.Generic;
using System.IO;
using System.Management;
using Microsoft.Isam.Esent.Interop;

namespace TaskBuddyWPF.Services
{
    // Reads Windows' own SRUM database — the real data source behind Task Manager's
    // App History tab (confirmed via independent forensics-community documentation;
    // Microsoft does not publish this schema officially). Table GUIDs and the
    // SruDbIdMapTable indirection are consistent across multiple independent parsers
    // (SrumECmd, srum-dump, Velociraptor, Plaso).
    //
    // SRUDB.dat is held with an EXCLUSIVE lock by the Diagnostic Policy Service —
    // confirmed via direct testing (Win32 error 32, ERROR_SHARING_VIOLATION, even
    // when requesting full FILE_SHARE_READ|WRITE|DELETE via CreateFile). The only way
    // to read it live is a Volume Shadow Copy snapshot. Note: "vssadmin create shadow"
    // was deliberately removed from client Windows (Home/Pro) editions by Microsoft to
    // prevent ransomware abuse — confirmed via direct testing (the command doesn't even
    // appear in vssadmin's supported-commands list). The WMI Win32_ShadowCopy.Create
    // method is a separate API surface that remains functional on client editions.
    public static class SrumReader
    {
        private const string SrumRelativePath = @"Windows\System32\sru\SRUDB.dat";
        private const string AppResourceTable = "{D10CA2FE-6FCF-4F6D-848E-B2E99266FA89}";
        private const string NetworkUsageTable = "{973F5D5C-1D90-4944-BE8E-24B94231A174}";
        private const string IdMapTable = "SruDbIdMapTable";

        public static List<Models.AppHistoryInfo> ReadHistory()
        {
            var (shadowId, devicePath) = CreateShadowCopy();
            try
            {
                string shadowSrumPath = Path.Combine(devicePath, SrumRelativePath);
                string tempCopy = Path.Combine(Path.GetTempPath(), $"TaskBuddy_SRUDB_{Guid.NewGuid():N}.dat");

                // The shadow-copy snapshot is a frozen, read-only point-in-time image —
                // not held open by the live service, so a normal File.Copy works here.
                File.Copy(shadowSrumPath, tempCopy, overwrite: true);

                try
                {
                    return ParseDatabase(tempCopy);
                }
                finally
                {
                    try { File.Delete(tempCopy); } catch { /* best-effort cleanup */ }
                }
            }
            finally
            {
                DeleteShadowCopy(shadowId);
            }
        }

        // Requires elevation (WMI Win32_ShadowCopy.Create requires Administrator).
        private static (string shadowId, string devicePath) CreateShadowCopy()
        {
            using var shadowClass = new ManagementClass(@"root\cimv2", "Win32_ShadowCopy", null);
            using var inParams = shadowClass.GetMethodParameters("Create");
            inParams["Volume"] = @"C:\";
            inParams["Context"] = "ClientAccessible";

            using var outParams = shadowClass.InvokeMethod("Create", inParams, null);
            uint returnValue = (uint)outParams["ReturnValue"];
            if (returnValue != 0)
            {
                throw new InvalidOperationException(
                    $"WMI Win32_ShadowCopy.Create failed with return code {returnValue}. " +
                    "TaskBuddy must run as Administrator to read App History.");
            }

            string shadowId = (string)outParams["ShadowID"];

            using var searcher = new ManagementObjectSearcher(
                $"SELECT * FROM Win32_ShadowCopy WHERE ID='{shadowId}'");
            foreach (ManagementObject obj in searcher.Get())
            {
                string devicePath = (string)obj["DeviceObject"];
                return (shadowId, devicePath);
            }

            throw new InvalidOperationException("Shadow copy was created but could not be located afterward.");
        }

        private static void DeleteShadowCopy(string shadowId)
        {
            try
            {
                using var searcher = new ManagementObjectSearcher(
                    $"SELECT * FROM Win32_ShadowCopy WHERE ID='{shadowId}'");
                foreach (ManagementObject obj in searcher.Get())
                {
                    obj.Delete();
                }
            }
            catch
            {
                // Best-effort cleanup — an orphaned shadow copy is a minor disk-space
                // annoyance, not worth crashing over. Windows also auto-expires these.
            }
        }

        private static List<Models.AppHistoryInfo> ParseDatabase(string dbPath)
        {
            var instance = new Instance("TaskBuddySrumReader");
            instance.Parameters.Recovery = false;
            instance.Parameters.NoInformationEvent = true;
            instance.Parameters.MaxTemporaryTables = 0;
            instance.Init();

            using (instance)
            using (var session = new Session(instance))
            {
                JET_DBID dbid;
                Api.JetAttachDatabase(session, dbPath, AttachDatabaseGrbit.ReadOnly);
                Api.JetOpenDatabase(session, dbPath, null, out dbid, OpenDatabaseGrbit.ReadOnly);

                var idMap = ReadIdMap(session, dbid);
                var byApp = new Dictionary<int, Models.AppHistoryInfo>();

                ReadAppResourceUsage(session, dbid, idMap, byApp);
                ReadNetworkUsage(session, dbid, idMap, byApp);

                var result = new List<Models.AppHistoryInfo>(byApp.Values);
                result.Sort((a, b) => b.CpuCycles.CompareTo(a.CpuCycles));
                return result;
            }
        }

        private static Dictionary<int, string> ReadIdMap(Session session, JET_DBID dbid)
        {
            var map = new Dictionary<int, string>();
            using var table = new Table(session, dbid, IdMapTable, OpenTableGrbit.ReadOnly);

            var idIndexCol = Api.GetTableColumnid(session, table, "IdIndex");
            var idBlobCol = Api.GetTableColumnid(session, table, "IdBlob");
            var idTypeCol = Api.GetTableColumnid(session, table, "IdType");

            Api.MoveBeforeFirst(session, table);
            while (Api.TryMoveNext(session, table))
            {
                var idIndex = Api.RetrieveColumnAsInt32(session, table, idIndexCol);
                var idType = Api.RetrieveColumnAsByte(session, table, idTypeCol);
                if (idIndex == null) continue;

                if (idType == 3)
                {
                    continue;
                }

                var blob = Api.RetrieveColumn(session, table, idBlobCol);
                if (blob != null)
                {
                    string text = System.Text.Encoding.Unicode.GetString(blob).TrimEnd('\0');
                    map[idIndex.Value] = text;
                }
            }
            return map;
        }

        private static void ReadAppResourceUsage(Session session, JET_DBID dbid,
            Dictionary<int, string> idMap, Dictionary<int, Models.AppHistoryInfo> byApp)
        {
            if (!TryOpenTable(session, dbid, AppResourceTable, out var table)) return;
            using (table)
            {
                var appIdCol = Api.GetTableColumnid(session, table, "AppId");
                var fgCycleCol = TryGetColumn(session, table, "ForegroundCycleTime");
                var bgCycleCol = TryGetColumn(session, table, "BackgroundCycleTime");
                var timeStampCol = TryGetColumn(session, table, "TimeStamp");

                Api.MoveBeforeFirst(session, table);
                while (Api.TryMoveNext(session, table))
                {
                    var appId = Api.RetrieveColumnAsInt32(session, table, appIdCol);
                    if (appId == null || !idMap.TryGetValue(appId.Value, out var path)) continue;

                    long fgCycles = fgCycleCol.HasValue ? (Api.RetrieveColumnAsInt64(session, table, fgCycleCol.Value) ?? 0) : 0;
                    long bgCycles = bgCycleCol.HasValue ? (Api.RetrieveColumnAsInt64(session, table, bgCycleCol.Value) ?? 0) : 0;
                    DateTime? timestamp = timeStampCol.HasValue ? Api.RetrieveColumnAsDateTime(session, table, timeStampCol.Value) : null;

                    if (!byApp.TryGetValue(appId.Value, out var info))
                    {
                        info = new Models.AppHistoryInfo
                        {
                            AppName = Path.GetFileName(path),
                            AppPath = path
                        };
                        byApp[appId.Value] = info;
                    }
                    info.CpuCycles += fgCycles + bgCycles;
                    if (timestamp.HasValue && timestamp.Value > info.LastActive)
                        info.LastActive = timestamp.Value;
                }
            }
        }

        private static void ReadNetworkUsage(Session session, JET_DBID dbid,
            Dictionary<int, string> idMap, Dictionary<int, Models.AppHistoryInfo> byApp)
        {
            if (!TryOpenTable(session, dbid, NetworkUsageTable, out var table)) return;
            using (table)
            {
                var appIdCol = Api.GetTableColumnid(session, table, "AppId");
                var sentCol = TryGetColumn(session, table, "BytesSent");
                var recvCol = TryGetColumn(session, table, "BytesRecvd");

                Api.MoveBeforeFirst(session, table);
                while (Api.TryMoveNext(session, table))
                {
                    var appId = Api.RetrieveColumnAsInt32(session, table, appIdCol);
                    if (appId == null || !idMap.TryGetValue(appId.Value, out var path)) continue;

                    ulong sent = sentCol.HasValue ? (ulong)(Api.RetrieveColumnAsInt64(session, table, sentCol.Value) ?? 0) : 0;
                    ulong recv = recvCol.HasValue ? (ulong)(Api.RetrieveColumnAsInt64(session, table, recvCol.Value) ?? 0) : 0;

                    if (!byApp.TryGetValue(appId.Value, out var info))
                    {
                        info = new Models.AppHistoryInfo
                        {
                            AppName = Path.GetFileName(path),
                            AppPath = path
                        };
                        byApp[appId.Value] = info;
                    }
                    info.NetworkBytes += sent + recv;
                }
            }
        }

        private static bool TryOpenTable(Session session, JET_DBID dbid, string tableName, out Table table)
        {
            try
            {
                table = new Table(session, dbid, tableName, OpenTableGrbit.ReadOnly);
                return true;
            }
            catch (EsentObjectNotFoundException)
            {
                table = null!;
                return false;
            }
        }

        private static JET_COLUMNID? TryGetColumn(Session session, Table table, string columnName)
        {
            try { return Api.GetTableColumnid(session, table, columnName); }
            catch (EsentErrorException) { return null; }
        }
    }
}
