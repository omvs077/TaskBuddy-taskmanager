using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Principal;
using System.Xml.Linq;

namespace TaskBuddyWPF.Services
{
    public static class StartupImpactReader
    {
        private const string LogDir = @"C:\Windows\System32\WDI\LogFiles\StartupInfo";

        public static Dictionary<string, (long cpuMicroseconds, long diskBytes)> ReadLatestBootTrace()
        {
            var result = new Dictionary<string, (long, long)>(StringComparer.OrdinalIgnoreCase);

            try
            {
                string? sid = WindowsIdentity.GetCurrent().User?.Value;
                if (sid == null) return result;
                // Directory.Exists() deliberately not used here — same masking issue
                // as File.Exists() on SRUDB.dat: it silently returns false on
                // UnauthorizedAccessException, hiding a permissions problem as
                // "folder missing." Let Directory.GetFiles surface the real exception.

                var candidates = Directory.GetFiles(LogDir, $"{sid}_StartupInfo*.xml");
                if (candidates.Length == 0) return result;

                string latest = candidates.OrderByDescending(f => File.GetLastWriteTimeUtc(f)).First();

                var doc = XDocument.Load(latest);
                if (doc.Root == null) return result;

                foreach (var proc in doc.Root.Elements("Process"))
                {
                    string? rawName = (string?)proc.Attribute("Name");
                    if (string.IsNullOrWhiteSpace(rawName)) continue;
                    string name = Path.GetFileName(rawName);

                    long cpu = (long?)proc.Element("CpuUsage") ?? 0;
                    long disk = (long?)proc.Element("DiskUsage") ?? 0;

                    if (result.TryGetValue(name, out var existing))
                        result[name] = (existing.Item1 + cpu, existing.Item2 + disk);
                    else
                        result[name] = (cpu, disk);
                }
            }
            catch
            {
            }

            return result;
        }

        public static Models.StartupImpact ClassifyImpact(long cpuMicroseconds, long diskBytes)
        {
            double cpuMs = cpuMicroseconds / 1000.0;
            double diskKb = diskBytes / 1024.0;

            if (cpuMs > 1000 || diskKb > 3072) return Models.StartupImpact.High;
            if (cpuMs >= 300 || diskKb >= 300) return Models.StartupImpact.Medium;
            return Models.StartupImpact.Low;
        }
    }
}
