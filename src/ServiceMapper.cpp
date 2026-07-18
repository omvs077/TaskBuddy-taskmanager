#include <windows.h>
#include <vector>
#include "ServiceMapper.h"

std::unordered_map<uint32_t, std::vector<std::wstring>> GetServicesByPid() {
    std::unordered_map<uint32_t, std::vector<std::wstring>> result;

    SC_HANDLE scm = OpenSCManagerW(nullptr, nullptr, SC_MANAGER_ENUMERATE_SERVICE);
    if (!scm) return result;

    DWORD bytesNeeded = 0, servicesReturned = 0, resumeHandle = 0;
    EnumServicesStatusExW(scm, SC_ENUM_PROCESS_INFO, SERVICE_WIN32, SERVICE_STATE_ALL,
        nullptr, 0, &bytesNeeded, &servicesReturned, &resumeHandle, nullptr);

    if (bytesNeeded == 0) { CloseServiceHandle(scm); return result; }

    std::vector<BYTE> buffer(bytesNeeded);
    if (!EnumServicesStatusExW(scm, SC_ENUM_PROCESS_INFO, SERVICE_WIN32, SERVICE_STATE_ALL,
        buffer.data(), bytesNeeded, &bytesNeeded, &servicesReturned, &resumeHandle, nullptr)) {
        CloseServiceHandle(scm);
        return result;
    }

    auto* services = reinterpret_cast<ENUM_SERVICE_STATUS_PROCESSW*>(buffer.data());
    for (DWORD i = 0; i < servicesReturned; ++i) {
        uint32_t pid = services[i].ServiceStatusProcess.dwProcessId;
        if (pid != 0) result[pid].push_back(services[i].lpDisplayName);
    }

    CloseServiceHandle(scm);
    return result;
}
