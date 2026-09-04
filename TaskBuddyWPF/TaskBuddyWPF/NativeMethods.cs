using System;
using System.Runtime.InteropServices;

namespace TaskBuddyWPF.Native
{
    [StructLayout(LayoutKind.Sequential)]
    internal struct UNICODE_STRING
    {
        public ushort Length;
        public ushort MaximumLength;
        public IntPtr Buffer;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct SYSTEM_PROCESS_INFORMATION
    {
        public uint NextEntryOffset;
        public uint NumberOfThreads;
        public long WorkingSetPrivateSize;
        public uint HardFaultCount;
        public uint NumberOfThreadsHighWatermark;
        public ulong CycleTime;
        public long CreateTime;
        public long UserTime;
        public long KernelTime;
        public UNICODE_STRING ImageName;
        public int BasePriority;
        public IntPtr UniqueProcessId;
        public IntPtr InheritedFromUniqueProcessId;
        public uint HandleCount;
        public uint SessionId;
        public UIntPtr PageDirectoryBase;
        public UIntPtr PeakVirtualSize;
        public UIntPtr VirtualSize;
        public uint PageFaultCount;
        public UIntPtr PeakWorkingSetSize;
        public UIntPtr WorkingSetSize;
        public UIntPtr QuotaPeakPagedPoolUsage;
        public UIntPtr QuotaPagedPoolUsage;
        public UIntPtr QuotaPeakNonPagedPoolUsage;
        public UIntPtr QuotaNonPagedPoolUsage;
        public UIntPtr PagefileUsage;
        public UIntPtr PeakPagefileUsage;
        public UIntPtr PrivatePageCount;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct IO_COUNTERS
    {
        public ulong ReadOperationCount;
        public ulong WriteOperationCount;
        public ulong OtherOperationCount;
        public ulong ReadTransferCount;
        public ulong WriteTransferCount;
        public ulong OtherTransferCount;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    internal struct SHFILEINFO
    {
        public IntPtr hIcon;
        public int iIcon;
        public uint dwAttributes;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
        public string szDisplayName;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 80)]
        public string szTypeName;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct MEMORYSTATUSEX
    {
        public uint dwLength;
        public uint dwMemoryLoad;
        public ulong ullTotalPhys;
        public ulong ullAvailPhys;
        public ulong ullTotalPageFile;
        public ulong ullAvailPageFile;
        public ulong ullTotalVirtual;
        public ulong ullAvailVirtual;
        public ulong ullAvailExtendedVirtual;
    }

    [StructLayout(LayoutKind.Explicit)]
    internal struct PDH_FMT_COUNTERVALUE
    {
        [FieldOffset(0)] public int CStatus;
        [FieldOffset(8)] public double doubleValue;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    internal struct ENUM_SERVICE_STATUS_PROCESS
    {
        [MarshalAs(UnmanagedType.LPWStr)] public string lpServiceName;
        [MarshalAs(UnmanagedType.LPWStr)] public string lpDisplayName;
        public SERVICE_STATUS_PROCESS ServiceStatusProcess;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct SERVICE_STATUS_PROCESS
    {
        public uint dwServiceType;
        public uint dwCurrentState;
        public uint dwControlsAccepted;
        public uint dwWin32ExitCode;
        public uint dwServiceSpecificExitCode;
        public uint dwCheckPoint;
        public uint dwWaitHint;
        public uint dwProcessId;
        public uint dwServiceFlags;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct SERVICE_STATUS
    {
        public uint dwServiceType;
        public uint dwCurrentState;
        public uint dwControlsAccepted;
        public uint dwWin32ExitCode;
        public uint dwServiceSpecificExitCode;
        public uint dwCheckPoint;
        public uint dwWaitHint;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    internal struct SERVICE_DESCRIPTION
    {
        [MarshalAs(UnmanagedType.LPWStr)] public string lpDescription;
    }


    [StructLayout(LayoutKind.Sequential)]
    internal struct SYSTEM_LOGICAL_PROCESSOR_INFORMATION_EX_HEADER
    {
        public int Relationship;
        public int Size;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct PROCESS_POWER_THROTTLING_STATE
    {
        public uint Version;
        public uint ControlMask;
        public uint StateMask;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    internal struct QUERY_SERVICE_CONFIGW
    {
        public uint dwServiceType;
        public uint dwStartType;
        public uint dwErrorControl;
        [MarshalAs(UnmanagedType.LPWStr)] public string lpBinaryPathName;
        [MarshalAs(UnmanagedType.LPWStr)] public string lpLoadOrderGroup;
        public uint dwTagId;
        [MarshalAs(UnmanagedType.LPWStr)] public string lpDependencies;
        [MarshalAs(UnmanagedType.LPWStr)] public string lpServiceStartName;
        [MarshalAs(UnmanagedType.LPWStr)] public string lpDisplayName;
    }
    internal static class NativeMethods
    {
        internal const int SystemProcessInformation = 5;
        internal const uint STATUS_INFO_LENGTH_MISMATCH = 0xC0000004;
        internal const uint PROCESS_QUERY_LIMITED_INFORMATION = 0x1000;
        internal const uint PROCESS_TERMINATE = 0x0001;
        internal const uint PROCESS_SUSPEND_RESUME = 0x0800;
        internal const uint SHGFI_ICON = 0x000000100;
        internal const uint SHGFI_SMALLICON = 0x000000001;

        [DllImport("ntdll.dll")]
        internal static extern uint NtQuerySystemInformation(
            int SystemInformationClass,
            IntPtr SystemInformation,
            int SystemInformationLength,
            out int ReturnLength);

        [DllImport("kernel32.dll", SetLastError = true)]
        internal static extern IntPtr OpenProcess(uint dwDesiredAccess, bool bInheritHandle, uint dwProcessId);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool CloseHandle(IntPtr hObject);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool TerminateProcess(IntPtr hProcess, uint uExitCode);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool SetPriorityClass(IntPtr hProcess, uint dwPriorityClass);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool SetProcessInformation(IntPtr hProcess, int ProcessInformationClass, ref PROCESS_POWER_THROTTLING_STATE ProcessInformation, uint ProcessInformationSize);

        internal const uint PROCESS_SET_INFORMATION = 0x0200;
        internal const uint IDLE_PRIORITY_CLASS = 0x00000040;
        internal const uint NORMAL_PRIORITY_CLASS = 0x00000020;
        internal const int ProcessPowerThrottling = 4;
        internal const uint PROCESS_POWER_THROTTLING_CURRENT_VERSION = 1;
        internal const uint PROCESS_POWER_THROTTLING_EXECUTION_SPEED = 0x1;

        [DllImport("ntdll.dll")]
        internal static extern uint NtSuspendProcess(IntPtr processHandle);

        [DllImport("ntdll.dll")]
        internal static extern uint NtResumeProcess(IntPtr processHandle);

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool QueryFullProcessImageNameW(IntPtr hProcess, int dwFlags, System.Text.StringBuilder lpExeName, ref int lpdwSize);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool GetProcessIoCounters(IntPtr hProcess, out IO_COUNTERS lpIoCounters);

        [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
        internal static extern IntPtr SHGetFileInfo(string pszPath, uint dwFileAttributes, ref SHFILEINFO psfi, uint cbFileInfo, uint uFlags);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool DestroyIcon(IntPtr hIcon);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool GlobalMemoryStatusEx(ref MEMORYSTATUSEX lpBuffer);

        internal const uint SC_MANAGER_ENUMERATE_SERVICE = 0x0004;
        internal const uint SC_ENUM_PROCESS_INFO = 0;
        internal const uint SERVICE_WIN32 = 0x00000030;
        internal const uint SERVICE_STATE_ALL = 0x00000003;
        internal const uint SERVICE_QUERY_STATUS = 0x0004;
        internal const uint SERVICE_QUERY_CONFIG = 0x0001;
        internal const uint SERVICE_START = 0x0010;
        internal const uint SERVICE_STOP = 0x0020;
        internal const uint SERVICE_CONFIG_DESCRIPTION = 1;
        internal const uint SERVICE_CONTROL_STOP = 0x00000001;
        internal const uint ERROR_INSUFFICIENT_BUFFER = 122;
        internal const uint SERVICE_PAUSE_CONTINUE = 0x0040;
        internal const uint SERVICE_CONTROL_PAUSE = 0x00000002;
        internal const uint SERVICE_CONTROL_CONTINUE = 0x00000003;
        internal const uint SERVICE_STOPPED = 1;
        internal const int SC_STATUS_PROCESS_INFO = 0;

        [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        internal static extern IntPtr OpenSCManager(string? machineName, string? databaseName, uint dwDesiredAccess);

        [DllImport("advapi32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool CloseServiceHandle(IntPtr hSCObject);

        [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool EnumServicesStatusEx(IntPtr hSCManager, uint infoLevel, uint dwServiceType, uint dwServiceState, IntPtr lpServices, uint cbBufSize, out uint pcbBytesNeeded, out uint lpServicesReturned, ref uint lpResumeHandle, string? pszGroupName);

        [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        internal static extern IntPtr OpenService(IntPtr hSCManager, string lpServiceName, uint dwDesiredAccess);

        [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool QueryServiceConfig2(IntPtr hService, uint dwInfoLevel, IntPtr lpBuffer, uint cbBufSize, out uint pcbBytesNeeded);

        [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool QueryServiceConfig(IntPtr hService, IntPtr lpServiceConfig, uint cbBufSize, out uint pcbBytesNeeded);

        [DllImport("advapi32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool QueryServiceStatusEx(IntPtr hService, int infoLevel, IntPtr lpBuffer, uint cbBufSize, out uint pcbBytesNeeded);

        [DllImport("advapi32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool ControlService(IntPtr hService, uint dwControl, ref SERVICE_STATUS lpServiceStatus);

        [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool StartService(IntPtr hService, uint dwNumServiceArgs, string[]? lpServiceArgVectors);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool GetSystemTimes(out System.Runtime.InteropServices.ComTypes.FILETIME lpIdleTime, out System.Runtime.InteropServices.ComTypes.FILETIME lpKernelTime, out System.Runtime.InteropServices.ComTypes.FILETIME lpUserTime);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool GetLogicalProcessorInformationEx(int RelationshipType, IntPtr Buffer, ref uint ReturnedLength);

        [DllImport("kernel32.dll")]
        internal static extern ulong GetTickCount64();

        internal const int RelationProcessorCore = 0;

        internal const uint PDH_FMT_DOUBLE = 0x00000200;

        [DllImport("pdh.dll", CharSet = CharSet.Unicode)]
        internal static extern uint PdhOpenQuery(string? szDataSource, IntPtr dwUserData, out IntPtr phQuery);

        [DllImport("pdh.dll", CharSet = CharSet.Unicode)]
        internal static extern uint PdhAddEnglishCounter(IntPtr hQuery, string szFullCounterPath, IntPtr dwUserData, out IntPtr phCounter);

        [DllImport("pdh.dll")]
        internal static extern uint PdhCollectQueryData(IntPtr hQuery);

        [DllImport("pdh.dll")]
        internal static extern uint PdhGetFormattedCounterValue(IntPtr hCounter, uint dwFormat, IntPtr lpdwType, out PDH_FMT_COUNTERVALUE pValue);

        [DllImport("pdh.dll")]
        internal static extern uint PdhCloseQuery(IntPtr hQuery);

        internal const uint TOKEN_QUERY = 0x0008;
        internal const int TokenUser = 1;

        [DllImport("advapi32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool OpenProcessToken(IntPtr ProcessHandle, uint DesiredAccess, out IntPtr TokenHandle);

        [DllImport("advapi32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool GetTokenInformation(IntPtr TokenHandle, int TokenInformationClass, IntPtr TokenInformation, int TokenInformationLength, out int ReturnLength);

        [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool LookupAccountSid(string? lpSystemName, IntPtr Sid, System.Text.StringBuilder lpName, ref uint cchName, System.Text.StringBuilder lpReferencedDomainName, ref uint cchReferencedDomainName, out int peUse);

        internal const int TokenVirtualizationEnabled = 24;

        internal const ushort IMAGE_FILE_MACHINE_UNKNOWN = 0x0000;
        internal const ushort IMAGE_FILE_MACHINE_I386 = 0x014c;
        internal const ushort IMAGE_FILE_MACHINE_ARM = 0x01c0;
        internal const ushort IMAGE_FILE_MACHINE_AMD64 = 0x8664;
        internal const ushort IMAGE_FILE_MACHINE_ARM64 = 0xAA64;

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool IsWow64Process2(IntPtr hProcess, out ushort processMachine, out ushort nativeMachine);
    }
}


