using System;
using System.Runtime.InteropServices;
using TaskBuddyWPF.Native;

namespace TaskBuddyWPF.Services
{
    public static class SystemInfo
    {
        private static ulong? _totalPhysicalMemoryBytes;

        public static ulong TotalPhysicalMemoryBytes
        {
            get
            {
                if (_totalPhysicalMemoryBytes == null)
                {
                    var status = new MEMORYSTATUSEX { dwLength = (uint)Marshal.SizeOf(typeof(MEMORYSTATUSEX)) };
                    _totalPhysicalMemoryBytes = NativeMethods.GlobalMemoryStatusEx(ref status) ? status.ullTotalPhys : 1UL;
                }
                return _totalPhysicalMemoryBytes.Value;
            }
        }

        private static string? _processorName;
        public static string ProcessorName
        {
            get
            {
                if (_processorName == null)
                {
                    try
                    {
                        using var key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(@"HARDWARE\DESCRIPTION\System\CentralProcessor\0");
                        _processorName = (key?.GetValue("ProcessorNameString") as string)?.Trim() ?? "Unknown Processor";
                    }
                    catch
                    {
                        _processorName = "Unknown Processor";
                    }
                }
                return _processorName;
            }
        }

        public static int LogicalCoreCount => Environment.ProcessorCount;

        private static int? _physicalCoreCount;
        public static int PhysicalCoreCount
        {
            get
            {
                if (_physicalCoreCount == null)
                {
                    _physicalCoreCount = CountPhysicalCores();
                }
                return _physicalCoreCount.Value;
            }
        }

        private static int CountPhysicalCores()
        {
            uint len = 0;
            NativeMethods.GetLogicalProcessorInformationEx(NativeMethods.RelationProcessorCore, IntPtr.Zero, ref len);
            if (len == 0) return Environment.ProcessorCount;

            IntPtr buffer = Marshal.AllocHGlobal((int)len);
            try
            {
                if (!NativeMethods.GetLogicalProcessorInformationEx(NativeMethods.RelationProcessorCore, buffer, ref len))
                    return Environment.ProcessorCount;

                int count = 0;
                IntPtr current = buffer;
                uint bytesRead = 0;
                while (bytesRead < len)
                {
                    var header = Marshal.PtrToStructure<SYSTEM_LOGICAL_PROCESSOR_INFORMATION_EX_HEADER>(current);
                    if (header.Relationship == NativeMethods.RelationProcessorCore) count++;
                    current = IntPtr.Add(current, header.Size);
                    bytesRead += (uint)header.Size;
                }
                return count > 0 ? count : Environment.ProcessorCount;
            }
            finally
            {
                Marshal.FreeHGlobal(buffer);
            }
        }

        public static string UptimeString
        {
            get
            {
                var ts = TimeSpan.FromMilliseconds(NativeMethods.GetTickCount64());
                return $"{(int)ts.TotalDays}d:{ts.Hours:D2}h:{ts.Minutes:D2}m:{ts.Seconds:D2}s";
            }
        }
    }
}

