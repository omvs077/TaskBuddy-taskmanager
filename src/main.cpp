#include <windows.h>
#include <cstdio>
#include <vector>
#include "ProcessInfo.h"

typedef NTSTATUS(WINAPI* NtQuerySystemInformation_t)(ULONG, PVOID, ULONG, PULONG);

std::vector<ProcessInfo> SnapshotToProcessInfo(BYTE* buffer) {
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

int main() {
    auto NtQuerySystemInformation = (NtQuerySystemInformation_t)GetProcAddress(
        GetModuleHandleW(L"ntdll.dll"), "NtQuerySystemInformation");
    if (!NtQuerySystemInformation) { fprintf(stderr, "Failed to resolve NtQuerySystemInformation\n"); return 1; }

    ULONG bufSize = 1 << 16;
    std::vector<BYTE> buffer;
    NTSTATUS status;
    do {
        buffer.resize(bufSize);
        status = NtQuerySystemInformation(5, buffer.data(), bufSize, &bufSize);
        if (status == 0xC0000004) bufSize *= 2;
    } while (status == 0xC0000004);

    if (status < 0) { fprintf(stderr, "NtQuerySystemInformation failed: 0x%08X\n", status); return 1; }

    auto processes = SnapshotToProcessInfo(buffer.data());
    for (const auto& p : processes)
        wprintf(L"PID: %-6u  PPID: %-6u  WS: %8llu KB  %s\n",
                p.Pid, p.ParentPid, p.WorkingSetBytes / 1024, p.ImageName.c_str());

    wprintf(L"\nTotal processes: %zu\n", processes.size());
    return 0;
}
