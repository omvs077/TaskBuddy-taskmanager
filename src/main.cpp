#include <windows.h>
#include <winternl.h>
#include <cstdio>
#include <vector>

typedef NTSTATUS(WINAPI* NtQuerySystemInformation_t)(ULONG, PVOID, ULONG, PULONG);

int main() {
    auto NtQuerySystemInformation = (NtQuerySystemInformation_t)GetProcAddress(
        GetModuleHandleW(L"ntdll.dll"), "NtQuerySystemInformation");
    if (!NtQuerySystemInformation) { fprintf(stderr, "Failed to resolve NtQuerySystemInformation\n"); return 1; }

    ULONG bufSize = 1 << 16;
    std::vector<BYTE> buffer;
    NTSTATUS status;
    do {
        buffer.resize(bufSize);
        status = NtQuerySystemInformation(5 /* SystemProcessInformation */, buffer.data(), bufSize, &bufSize);
        if (status == 0xC0000004 /* STATUS_INFO_LENGTH_MISMATCH */) bufSize *= 2;
    } while (status == 0xC0000004);

    if (status < 0) { fprintf(stderr, "NtQuerySystemInformation failed: 0x%08X\n", status); return 1; }

    auto* entry = reinterpret_cast<SYSTEM_PROCESS_INFORMATION*>(buffer.data());
    while (true) {
        if (entry->ImageName.Buffer)
            wprintf(L"PID: %-6zu  %s\n", (size_t)entry->UniqueProcessId, entry->ImageName.Buffer);
        else
            wprintf(L"PID: %-6zu  [System Idle/Unknown]\n", (size_t)entry->UniqueProcessId);

        if (entry->NextEntryOffset == 0) break;
        entry = reinterpret_cast<SYSTEM_PROCESS_INFORMATION*>(
            reinterpret_cast<BYTE*>(entry) + entry->NextEntryOffset);
    }
    return 0;
}
