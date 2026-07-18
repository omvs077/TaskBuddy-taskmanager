#include <windows.h>
#include "ProcessOps.h"

typedef NTSTATUS(WINAPI* NtSuspendProcess_t)(HANDLE);
typedef NTSTATUS(WINAPI* NtResumeProcess_t)(HANDLE);

static NtSuspendProcess_t GetNtSuspendProcess() {
    static auto fn = (NtSuspendProcess_t)GetProcAddress(GetModuleHandleW(L"ntdll.dll"), "NtSuspendProcess");
    return fn;
}
static NtResumeProcess_t GetNtResumeProcess() {
    static auto fn = (NtResumeProcess_t)GetProcAddress(GetModuleHandleW(L"ntdll.dll"), "NtResumeProcess");
    return fn;
}

bool TerminateTargetProcess(uint32_t pid) {
    HANDLE h = OpenProcess(PROCESS_TERMINATE, FALSE, pid);
    if (!h) return false;
    bool ok = TerminateProcess(h, 1) != 0;
    CloseHandle(h);
    return ok;
}

bool SuspendTargetProcess(uint32_t pid) {
    auto fn = GetNtSuspendProcess();
    if (!fn) return false;
    HANDLE h = OpenProcess(PROCESS_SUSPEND_RESUME, FALSE, pid);
    if (!h) return false;
    bool ok = fn(h) >= 0;
    CloseHandle(h);
    return ok;
}

bool ResumeTargetProcess(uint32_t pid) {
    auto fn = GetNtResumeProcess();
    if (!fn) return false;
    HANDLE h = OpenProcess(PROCESS_SUSPEND_RESUME, FALSE, pid);
    if (!h) return false;
    bool ok = fn(h) >= 0;
    CloseHandle(h);
    return ok;
}

bool SetTargetProcessPriority(uint32_t pid, uint32_t priorityClass) {
    HANDLE h = OpenProcess(PROCESS_SET_INFORMATION, FALSE, pid);
    if (!h) return false;
    bool ok = SetPriorityClass(h, priorityClass) != 0;
    CloseHandle(h);
    return ok;
}
