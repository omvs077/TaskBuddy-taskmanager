#include <windows.h>
#include <psapi.h>
#include <pdh.h>
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

static float SampleDiskBytesPerSec(const std::vector<ProcessInfo>&) {
    static HQUERY query = nullptr;
    static HCOUNTER counter = nullptr;
    static bool initFailed = false;

    if (!query && !initFailed) {
        if (PdhOpenQueryW(nullptr, 0, &query) != ERROR_SUCCESS) { initFailed = true; return 0.0f; }
        if (PdhAddEnglishCounterW(query, L"\\PhysicalDisk(_Total)\\Disk Bytes/sec", 0, &counter) != ERROR_SUCCESS) {
            initFailed = true;
            return 0.0f;
        }
        PdhCollectQueryData(query);
        return 0.0f;
    }
    if (initFailed) return 0.0f;

    if (PdhCollectQueryData(query) != ERROR_SUCCESS) return 0.0f;

    PDH_FMT_COUNTERVALUE value{};
    if (PdhGetFormattedCounterValue(counter, PDH_FMT_DOUBLE, nullptr, &value) != ERROR_SUCCESS) return 0.0f;
    return (float)value.doubleValue;
}

ResourceSample SampleResources(const std::vector<ProcessInfo>& processes) {
    ResourceSample s{};
    s.CpuPercent = SampleCpuPercent();
    s.RamPercent = SampleRamPercent();
    s.DiskBytesPerSec = SampleDiskBytesPerSec(processes);
    return s;
}


