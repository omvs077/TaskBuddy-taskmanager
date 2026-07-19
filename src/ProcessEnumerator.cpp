#include <windows.h>
#include <vector>
#include <unordered_map>
#include "ProcessInfo.h"
#include "ProcessEnumerator.h"

typedef NTSTATUS(WINAPI* NtQuerySystemInformation_t)(ULONG, PVOID, ULONG, PULONG);

static std::vector<ProcessInfo> ParseSnapshot(BYTE* buffer) {
    std::vector<ProcessInfo> result;
    auto* entry = reinterpret_cast<TB_SYSTEM_PROCESS_INFORMATION*>(buffer);
    while (true) {
        ProcessInfo info{};
        info.Pid = (uint32_t)(uintptr_t)entry->UniqueProcessId;
        info.ParentPid = (uint32_t)(uintptr_t)entry->InheritedFromUniqueProcessId;
        info.WorkingSetBytes = entry->WorkingSetSize;
        info.CpuTime100ns = (uint64_t)entry->KernelTime.QuadPart + (uint64_t)entry->UserTime.QuadPart;
        info.ImageName = entry->ImageName.Buffer ? entry->ImageName.Buffer : L"[System Idle/Unknown]";
        result.push_back(std::move(info));

        if (entry->NextEntryOffset == 0) break;
        entry = reinterpret_cast<TB_SYSTEM_PROCESS_INFORMATION*>(
            reinterpret_cast<BYTE*>(entry) + entry->NextEntryOffset);
    }
    return result;
}

std::vector<ProcessInfo> GetProcessSnapshot() {
    static auto NtQuerySystemInformation = (NtQuerySystemInformation_t)GetProcAddress(
        GetModuleHandleW(L"ntdll.dll"), "NtQuerySystemInformation");
    if (!NtQuerySystemInformation) return {};

    ULONG bufSize = 1 << 16;
    std::vector<BYTE> buffer;
    NTSTATUS status;
    do {
        buffer.resize(bufSize);
        status = NtQuerySystemInformation(5, buffer.data(), bufSize, &bufSize);
        if (status == 0xC0000004) bufSize *= 2;
    } while (status == 0xC0000004);

    if (status < 0) return {};
    auto result = ParseSnapshot(buffer.data());

    static std::unordered_map<uint32_t, uint64_t> prevCpuTime;
    static uint64_t prevTick = 0;
    static int numCores = [] { SYSTEM_INFO si; GetSystemInfo(&si); return (int)si.dwNumberOfProcessors; }();

    uint64_t nowTick = GetTickCount64();
    uint64_t elapsedMs = prevTick ? (nowTick - prevTick) : 0;
    uint64_t elapsed100ns = elapsedMs * 10000ULL;

    for (auto& p : result) {
        auto it = prevCpuTime.find(p.Pid);
        if (it != prevCpuTime.end() && elapsed100ns > 0) {
            uint64_t delta = (p.CpuTime100ns >= it->second) ? (p.CpuTime100ns - it->second) : 0;
            p.CpuPercent = (float)((double)delta / (double)(elapsed100ns * numCores) * 100.0);
        }
        prevCpuTime[p.Pid] = p.CpuTime100ns;
    }
    prevTick = nowTick;

    return result;
}


