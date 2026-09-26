using System;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

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

    // First 8 bytes of the native CACHE_RELATIONSHIP struct (Level, Associativity,
    // LineSize, CacheSize) — read at offset 8 within a RelationCache entry, right
    // after the 8-byte header above (same union-overlay technique already used
    // for RelationProcessorCore). The full native struct has more fields
    // (Type/Reserved/GroupCount/GroupMask) that aren't needed here.
    [StructLayout(LayoutKind.Sequential)]
    internal struct CACHE_RELATIONSHIP_MINIMAL
    {
        public byte Level;
        public byte Associativity;
        public ushort LineSize;
        public uint CacheSize;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct PROCESS_POWER_THROTTLING_STATE
    {
        public uint Version;
        public uint ControlMask;
        public uint StateMask;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    internal struct SHELLEXECUTEINFO
    {
        public int cbSize;
        public uint fMask;
        public IntPtr hwnd;
        [MarshalAs(UnmanagedType.LPWStr)] public string lpVerb;
        [MarshalAs(UnmanagedType.LPWStr)] public string lpFile;
        [MarshalAs(UnmanagedType.LPWStr)] public string lpParameters;
        [MarshalAs(UnmanagedType.LPWStr)] public string lpDirectory;
        public int nShow;
        public IntPtr hInstApp;
        public IntPtr lpIDList;
        [MarshalAs(UnmanagedType.LPWStr)] public string lpClass;
        public IntPtr hkeyClass;
        public uint dwHotKey;
        public IntPtr hIcon;
        public IntPtr hProcess;
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
        internal const uint PROCESS_QUERY_INFORMATION = 0x0400;
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

        internal struct PROCESS_BASIC_INFORMATION
        {
            public IntPtr ExitStatus;
            public IntPtr PebBaseAddress;
            public IntPtr AffinityMask;
            public IntPtr BasePriority;
            public IntPtr UniqueProcessId;
            public IntPtr InheritedFromUniqueProcessId;
        }

        [DllImport("ntdll.dll")]
        internal static extern uint NtQueryInformationProcess(
            IntPtr ProcessHandle,
            int ProcessInformationClass,
            ref PROCESS_BASIC_INFORMATION ProcessInformation,
            int ProcessInformationLength,
            out int ReturnLength);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool ReadProcessMemory(
            IntPtr hProcess,
            IntPtr lpBaseAddress,
            byte[] lpBuffer,
            int dwSize,
            out int lpNumberOfBytesRead);

        [DllImport("kernel32.dll", SetLastError = true)]
        internal static extern uint GetPriorityClass(IntPtr hProcess);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool SetProcessAffinityMask(IntPtr hProcess, UIntPtr dwProcessAffinityMask);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool GetProcessAffinityMask(IntPtr hProcess, out UIntPtr lpProcessAffinityMask, out UIntPtr lpSystemAffinityMask);

        [DllImport("dbghelp.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool MiniDumpWriteDump(IntPtr hProcess, uint processId, Microsoft.Win32.SafeHandles.SafeFileHandle hFile, uint dumpType, IntPtr exceptionParam, IntPtr userStreamParam, IntPtr callbackParam);

        internal const uint PROCESS_VM_READ = 0x0010;

        // Exact flag combination confirmed from a real Task Manager-generated dump
        // header (via .dumpdebug), not guessed -- Flags 0x1826.
        internal const uint MiniDumpWithFullMemory = 0x00000002;
        internal const uint MiniDumpWithHandleData = 0x00000004;
        internal const uint MiniDumpWithUnloadedModules = 0x00000020;
        internal const uint MiniDumpWithFullMemoryInfo = 0x00000800;
        internal const uint MiniDumpWithThreadInfo = 0x00001000;

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool SetProcessInformation(IntPtr hProcess, int ProcessInformationClass, ref PROCESS_POWER_THROTTLING_STATE ProcessInformation, uint ProcessInformationSize);

        [DllImport("shell32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool ShellExecuteEx(ref SHELLEXECUTEINFO lpExecInfo);

        internal const uint SEE_MASK_INVOKEIDLIST = 0x0000000C;

        internal const uint PROCESS_SET_INFORMATION = 0x0200;
        internal const uint IDLE_PRIORITY_CLASS = 0x00000040;
        internal const uint NORMAL_PRIORITY_CLASS = 0x00000020;
        internal const uint BELOW_NORMAL_PRIORITY_CLASS = 0x00004000;
        internal const uint ABOVE_NORMAL_PRIORITY_CLASS = 0x00008000;
        internal const uint HIGH_PRIORITY_CLASS = 0x00000080;
        internal const uint REALTIME_PRIORITY_CLASS = 0x00000100;
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
        internal const int RelationCache = 2;
        internal const int RelationProcessorPackage = 3;

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

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool IsProcessCritical(IntPtr hProcess, out bool Critical);

        internal delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool IsWindowVisible(IntPtr hWnd);

        [DllImport("user32.dll")]
        internal static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

        [DllImport("user32.dll")]
        internal static extern IntPtr GetWindow(IntPtr hWnd, uint uCmd);

        [DllImport("user32.dll")]
        internal static extern int GetWindowLong(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll")]
        internal static extern int GetWindowTextLength(IntPtr hWnd);

        internal const uint GW_OWNER = 4;
        internal const int GWL_EXSTYLE = -20;
        internal const int WS_EX_TOOLWINDOW = 0x00000080;

        [DllImport("user32.dll")]
        internal static extern IntPtr GetAncestor(IntPtr hwnd, uint gaFlags);

        [DllImport("user32.dll")]
        internal static extern IntPtr GetLastActivePopup(IntPtr hWnd);

        [DllImport("dwmapi.dll")]
        internal static extern int DwmGetWindowAttribute(IntPtr hwnd, int dwAttribute, out int pvAttribute, int cbAttribute);

        internal const uint GA_ROOTOWNER = 3;
        internal const int DWMWA_CLOAKED = 14;
        internal const int WS_EX_APPWINDOW = 0x00040000;

        // --- WLAN API (Wi-Fi tab): SSID and signal quality aren't exposed by
        // System.Net.NetworkInformation, so these specific fields use the native
        // Native Wi-Fi API. Adapter name, connection type, IP addresses, and
        // throughput are read via NetworkInterface (.NET stdlib) instead — see
        // WifiEnumerator.cs. Struct layouts confirmed via Microsoft documentation
        // before implementation.
        internal const int WLAN_INTF_OPCODE_CURRENT_CONNECTION = 7;
        internal const uint WLAN_INTERFACE_STATE_CONNECTED = 1;

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        internal struct WLAN_INTERFACE_INFO
        {
            public Guid InterfaceGuid;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
            public string strInterfaceDescription;
            public uint isState;
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct DOT11_SSID
        {
            public uint uSSIDLength;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 32)]
            public byte[] ucSSID;
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct WLAN_ASSOCIATION_ATTRIBUTES
        {
            public DOT11_SSID dot11Ssid;
            public uint dot11BssType;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 6)]
            public byte[] dot11Bssid;
            public uint dot11PhyType;
            public uint uDot11PhyIndex;
            public uint wlanSignalQuality;
            public uint ulRxRate;
            public uint ulTxRate;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        internal struct WLAN_CONNECTION_ATTRIBUTES
        {
            public uint isState;
            public uint wlanConnectionMode;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
            public string strProfileName;
            public WLAN_ASSOCIATION_ATTRIBUTES wlanAssociationAttributes;
        }

        [DllImport("wlanapi.dll")]
        internal static extern int WlanOpenHandle(uint dwClientVersion, IntPtr pReserved, out uint pdwNegotiatedVersion, out IntPtr phClientHandle);

        [DllImport("wlanapi.dll")]
        internal static extern int WlanCloseHandle(IntPtr hClientHandle, IntPtr pReserved);

        [DllImport("wlanapi.dll")]
        internal static extern int WlanEnumInterfaces(IntPtr hClientHandle, IntPtr pReserved, out IntPtr ppInterfaceList);

        [DllImport("wlanapi.dll")]
        internal static extern int WlanQueryInterface(IntPtr hClientHandle, ref Guid pInterfaceGuid, int OpCode, IntPtr pReserved, out int pdwDataSize, out IntPtr ppData, IntPtr pWlanOpcodeValueType);

        [DllImport("wlanapi.dll")]
        internal static extern void WlanFreeMemory(IntPtr pMemory);

        // --- PDH additions for GPU tab: Task Manager itself reads GPU
        // utilization/memory via these same raw PDH counters (confirmed via
        // research). The alternative WMI class,
        // Win32_PerfFormattedData_GPUPerformanceCounters_GPUEngine, is
        // independently reported to read as always-0 on many systems and was
        // deliberately avoided. Reuses the existing PdhOpenQuery,
        // PdhAddEnglishCounter, PdhCollectQueryData, PdhCloseQuery, and
        // PDH_FMT_COUNTERVALUE declared above for the CPU/Disk sampling.
        internal const uint PDH_MORE_DATA = 0x800007D2;

        [StructLayout(LayoutKind.Sequential)]
        internal struct PDH_FMT_COUNTERVALUE_ITEM_W
        {
            public IntPtr szName;
            public PDH_FMT_COUNTERVALUE FmtValue;
        }

        [DllImport("pdh.dll")]
        internal static extern uint PdhGetFormattedCounterArrayW(IntPtr hCounter, uint dwFormat, ref int lpdwBufferSize, out int lpdwItemCount, IntPtr itemBuffer);

        // GetPerformanceInfo: documented psapi.dll API giving paged/non-paged pool,
        // system cache, and commit stats — used for the Memory detail panel instead
        // of NtQuerySystemInformation(SystemMemoryListInformation) which has an
        // undocumented struct layout that varies between Windows versions.
        [StructLayout(LayoutKind.Sequential)]
        internal struct PERFORMANCE_INFORMATION
        {
            public uint cb;
            public UIntPtr CommitTotal;
            public UIntPtr CommitLimit;
            public UIntPtr CommitPeak;
            public UIntPtr PhysicalTotal;
            public UIntPtr PhysicalAvailable;
            public UIntPtr SystemCache;
            public UIntPtr KernelTotal;
            public UIntPtr KernelPaged;
            public UIntPtr KernelNonPaged;
            public UIntPtr PageSize;
            public uint HandleCount;
            public uint ProcessCount;
            public uint ThreadCount;
        }

        [DllImport("psapi.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool GetPerformanceInfo(out PERFORMANCE_INFORMATION pPerformanceInformation, uint cb);

        internal const uint GENERIC_READ = 0x80000000;
        internal const uint FILE_SHARE_READ = 0x1;
        internal const uint FILE_SHARE_WRITE = 0x2;
        internal const uint OPEN_EXISTING = 3;
        internal const uint StorageDeviceSeekPenaltyProperty = 7;
        internal const uint PropertyStandardQuery = 0;
        internal const uint IOCTL_STORAGE_QUERY_PROPERTY = 0x2D1400;

        [StructLayout(LayoutKind.Sequential)]
        internal struct STORAGE_PROPERTY_QUERY
        {
            public uint PropertyId;
            public uint QueryType;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 1)]
            public byte[] AdditionalParameters;
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct DEVICE_SEEK_PENALTY_DESCRIPTOR
        {
            public uint Version;
            public uint Size;
            [MarshalAs(UnmanagedType.U1)]
            public bool IncursSeekPenalty;
        }

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        internal static extern SafeFileHandle CreateFile(string lpFileName, uint dwDesiredAccess, uint dwShareMode, IntPtr lpSecurityAttributes, uint dwCreationDisposition, uint dwFlagsAndAttributes, IntPtr hTemplateFile);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool DeviceIoControl(SafeFileHandle hDevice, uint dwIoControlCode, ref STORAGE_PROPERTY_QUERY lpInBuffer, uint nInBufferSize, ref DEVICE_SEEK_PENALTY_DESCRIPTOR lpOutBuffer, uint nOutBufferSize, out uint lpBytesReturned, IntPtr lpOverlapped);

        [DllImport("powrprof.dll")]
        internal static extern uint PowerGetActiveScheme(IntPtr UserRootPowerKey, out IntPtr ActivePolicyGuid);

        [DllImport("kernel32.dll")]
        internal static extern IntPtr LocalFree(IntPtr hMem);

        [StructLayout(LayoutKind.Sequential)]
        internal struct SYSTEM_POWER_STATUS
        {
            public byte ACLineStatus;
            public byte BatteryFlag;
            public byte BatteryLifePercent;
            public byte SystemStatusFlag;
            public int BatteryLifeTime;
            public int BatteryFullLifeTime;
        }

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool GetSystemPowerStatus(out SYSTEM_POWER_STATUS lpSystemPowerStatus);
    }
}









