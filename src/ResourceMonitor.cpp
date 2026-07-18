#include <windows.h>
#include <psapi.h>
#include "ResourceMonitor.h"

static uint64_t FileTimeToU64(const FILETIME& ft) {
    return ((uint64_t)ft.dwHighDateTime << 32) | ft.dwLowDateTime;
}

static float SampleCpuPercent() {
    static uint64_t prevIdle = 0, prevKernel = 0, prevUser = 0;
    FILETIME idleFt, kernelFt, userFt;
    if (!GetSystemTimes(&idleFt, &kernelFt, &userFt)) return 0.0f;

    uint64_t idle = FileTimeToU64(idleFt);
    uint64_t kernel = FileTimeToU64(kernelFt);
    uint64_t user = FileTimeToU64(userFt);

    uint64_t idleDelta = idle - prevIdle;
    uint64_t totalDelta = (kernel - prevKernel) + (user - prevUser);

    prevIdle = idle; prevKernel = kernel; prevUser = user;

    if (totalDelta == 0) return 0.0f;
    float busy = 1.0f - ((float)idleDelta / (float)totalDelta);
    return busy < 0.0f ? 0.0f : (busy > 1.0f ? 1.0f : busy) * 100.0f;
}

static float SampleRamPercent() {
    MEMORYSTATUSEX mem{};
    mem.dwLength = sizeof(mem);
    if (!GlobalMemoryStatusEx(&mem)) return 0.0f;
    return (float)mem.dwMemoryLoad;
}

static float SampleDiskBytesPerSec(const std::vector<ProcessInfo>& processes) {
    static uint64_t prevTotalBytes = 0;
    static bool firstRun = true;

    uint64_t totalBytes = 0;
    for (const auto& p : processes) {
        HANDLE h = OpenProcess(PROCESS_QUERY_LIMITED_INFORMATION, FALSE, p.Pid);
        if (!h) continue;
        IO_COUNTERS io{};
        if (GetProcessIoCounters(h, &io)) {
            totalBytes += io.ReadTransferCount + io.WriteTransferCount;
        }
        CloseHandle(h);
    }

    uint64_t delta = firstRun ? 0 : (totalBytes >= prevTotalBytes ? totalBytes - prevTotalBytes : 0);
    prevTotalBytes = totalBytes;
    firstRun = false;
    return (float)delta;
}

ResourceSample SampleResources(const std::vector<ProcessInfo>& processes) {
    ResourceSample s{};
    s.CpuPercent = SampleCpuPercent();
    s.RamPercent = SampleRamPercent();
    s.DiskBytesPerSec = SampleDiskBytesPerSec(processes);
    return s;
}
