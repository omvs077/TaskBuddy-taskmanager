#include <algorithm>
#include <cfloat>
#include <unordered_map>
#include <unordered_set>
#include <shlwapi.h>
#include "imgui.h"
#include "imgui_impl_win32.h"
#include "imgui_impl_dx11.h"
#include "ProcessEnumerator.h"
#include "ProcessOps.h"
#include "ServiceMapper.h"
#include "ResourceMonitor.h"
#include "IconCache.h"
#include <d3d11.h>
#define NOMINMAX
#include <windows.h>
#include <tchar.h>

extern IMGUI_IMPL_API LRESULT ImGui_ImplWin32_WndProcHandler(HWND, UINT, WPARAM, LPARAM);

static ID3D11Device* g_pd3dDevice = nullptr;
static ID3D11DeviceContext* g_pd3dDeviceContext = nullptr;
static IDXGISwapChain* g_pSwapChain = nullptr;
static ID3D11RenderTargetView* g_mainRenderTargetView = nullptr;

static bool CreateDeviceD3D(HWND hWnd) {
    DXGI_SWAP_CHAIN_DESC sd{};
    sd.BufferCount = 2;
    sd.BufferDesc.Width = 0;
    sd.BufferDesc.Height = 0;
    sd.BufferDesc.Format = DXGI_FORMAT_R8G8B8A8_UNORM;
    sd.BufferDesc.RefreshRate.Numerator = 60;
    sd.BufferDesc.RefreshRate.Denominator = 1;
    sd.Flags = DXGI_SWAP_CHAIN_FLAG_ALLOW_MODE_SWITCH;
    sd.BufferUsage = DXGI_USAGE_RENDER_TARGET_OUTPUT;
    sd.OutputWindow = hWnd;
    sd.SampleDesc.Count = 1;
    sd.SampleDesc.Quality = 0;
    sd.Windowed = TRUE;
    sd.SwapEffect = DXGI_SWAP_EFFECT_DISCARD;

    UINT createDeviceFlags = 0;
    D3D_FEATURE_LEVEL featureLevel;
    const D3D_FEATURE_LEVEL featureLevelArray[2] = { D3D_FEATURE_LEVEL_11_0, D3D_FEATURE_LEVEL_10_0 };
    HRESULT res = D3D11CreateDeviceAndSwapChain(nullptr, D3D_DRIVER_TYPE_HARDWARE, nullptr, createDeviceFlags,
        featureLevelArray, 2, D3D11_SDK_VERSION, &sd, &g_pSwapChain, &g_pd3dDevice, &featureLevel, &g_pd3dDeviceContext);
    if (res != S_OK) return false;

    ID3D11Texture2D* pBackBuffer;
    g_pSwapChain->GetBuffer(0, IID_PPV_ARGS(&pBackBuffer));
    g_pd3dDevice->CreateRenderTargetView(pBackBuffer, nullptr, &g_mainRenderTargetView);
    pBackBuffer->Release();
    return true;
}

static void CleanupDeviceD3D() {
    if (g_mainRenderTargetView) { g_mainRenderTargetView->Release(); g_mainRenderTargetView = nullptr; }
    if (g_pSwapChain) { g_pSwapChain->Release(); g_pSwapChain = nullptr; }
    if (g_pd3dDeviceContext) { g_pd3dDeviceContext->Release(); g_pd3dDeviceContext = nullptr; }
    if (g_pd3dDevice) { g_pd3dDevice->Release(); g_pd3dDevice = nullptr; }
}

extern LRESULT CALLBACK WndProc(HWND hWnd, UINT msg, WPARAM wParam, LPARAM lParam) {
    if (ImGui_ImplWin32_WndProcHandler(hWnd, msg, wParam, lParam)) return true;
    switch (msg) {
    case WM_SIZE:
        if (g_pd3dDevice != nullptr && wParam != SIZE_MINIMIZED) {
            if (g_mainRenderTargetView) { g_mainRenderTargetView->Release(); g_mainRenderTargetView = nullptr; }
            g_pSwapChain->ResizeBuffers(0, (UINT)LOWORD(lParam), (UINT)HIWORD(lParam), DXGI_FORMAT_UNKNOWN, 0);
            ID3D11Texture2D* pBackBuffer;
            g_pSwapChain->GetBuffer(0, IID_PPV_ARGS(&pBackBuffer));
            g_pd3dDevice->CreateRenderTargetView(pBackBuffer, nullptr, &g_mainRenderTargetView);
            pBackBuffer->Release();
        }
        return 0;
    case WM_DESTROY:
        PostQuitMessage(0);
        return 0;
    }
    return DefWindowProc(hWnd, msg, wParam, lParam);
}

int RunAppWindow() {
    WNDCLASSEXW wc = { sizeof(wc), CS_CLASSDC, WndProc, 0L, 0L, GetModuleHandle(nullptr),
        nullptr, nullptr, nullptr, nullptr, L"TaskBuddyWndClass", nullptr };
    RegisterClassExW(&wc);
    HWND hwnd = CreateWindowW(wc.lpszClassName, L"TaskBuddy", WS_OVERLAPPEDWINDOW,
        100, 100, 1280, 800, nullptr, nullptr, wc.hInstance, nullptr);

    if (!CreateDeviceD3D(hwnd)) { CleanupDeviceD3D(); UnregisterClassW(wc.lpszClassName, wc.hInstance); return 1; }

    ShowWindow(hwnd, SW_SHOWDEFAULT);
    UpdateWindow(hwnd);

    IMGUI_CHECKVERSION();
    ImGui::CreateContext();
    ImGui::StyleColorsDark();
    { ImGuiStyle& style = ImGui::GetStyle();
      style.WindowRounding = 3.0f; style.FrameRounding = 3.0f; style.GrabRounding = 3.0f;
      style.WindowPadding = ImVec2(8,8); style.FramePadding = ImVec2(6,4); style.ItemSpacing = ImVec2(8,6);
      ImVec4* c = style.Colors;
      c[ImGuiCol_WindowBg] = ImVec4(0.11f,0.11f,0.12f,1.00f);
      c[ImGuiCol_ChildBg]  = ImVec4(0.13f,0.13f,0.14f,1.00f);
      c[ImGuiCol_FrameBg]  = ImVec4(0.16f,0.16f,0.17f,1.00f);
      c[ImGuiCol_Header]        = ImVec4(0.00f,0.47f,0.83f,0.35f);
      c[ImGuiCol_HeaderHovered] = ImVec4(0.00f,0.47f,0.83f,0.55f);
      c[ImGuiCol_HeaderActive]  = ImVec4(0.00f,0.47f,0.83f,0.75f);
      c[ImGuiCol_Button]        = ImVec4(0.16f,0.16f,0.17f,1.00f);
      c[ImGuiCol_ButtonHovered] = ImVec4(0.00f,0.47f,0.83f,0.55f);
      c[ImGuiCol_ButtonActive]  = ImVec4(0.00f,0.47f,0.83f,0.85f);
    }
    ImGui_ImplWin32_Init(hwnd);
    ImGui_ImplDX11_Init(g_pd3dDevice, g_pd3dDeviceContext);

    bool done = false;
    while (!done) {
        MSG msg;
        while (PeekMessage(&msg, nullptr, 0U, 0U, PM_REMOVE)) {
            TranslateMessage(&msg);
            DispatchMessage(&msg);
            if (msg.message == WM_QUIT) done = true;
        }
        if (done) break;

        ImGui_ImplDX11_NewFrame();
        ImGui_ImplWin32_NewFrame();
        ImGui::NewFrame();

        ImGui::Begin("TaskBuddy");
        static auto processes = GetProcessSnapshot();
        static float refreshTimer = 0.0f;
        refreshTimer += ImGui::GetIO().DeltaTime;
        static auto servicesByPid = GetServicesByPid();
        static std::unordered_map<uint32_t, double> spawnTimes;
        if (refreshTimer > 1.0f) {
            auto fresh = GetProcessSnapshot();
            std::unordered_set<uint32_t> currentPids;
            for (auto& p : fresh) {
                currentPids.insert(p.Pid);
                if (spawnTimes.find(p.Pid) == spawnTimes.end()) spawnTimes[p.Pid] = ImGui::GetTime();
            }
            for (auto it = spawnTimes.begin(); it != spawnTimes.end(); ) {
                if (currentPids.find(it->first) == currentPids.end()) it = spawnTimes.erase(it); else ++it;
            }
            processes = std::move(fresh);
            servicesByPid = GetServicesByPid();
            refreshTimer = 0.0f;
        }

        static const int kHistoryLen = 120;
        static float cpuHistory[kHistoryLen] = {};
        static float ramHistory[kHistoryLen] = {};
        static float diskHistory[kHistoryLen] = {};
        static int historyOffset = 0;
        static float sampleTimer = 0.0f;
        sampleTimer += ImGui::GetIO().DeltaTime;
        if (sampleTimer > 0.5f) {
            ResourceSample sample = SampleResources(processes);
            cpuHistory[historyOffset] = sample.CpuPercent;
            ramHistory[historyOffset] = sample.RamPercent;
            diskHistory[historyOffset] = sample.DiskBytesPerSec / (1024.0f * 1024.0f);
            historyOffset = (historyOffset + 1) % kHistoryLen;
            sampleTimer = 0.0f;
        }

        ImGui::PlotLines("CPU %", cpuHistory, kHistoryLen, historyOffset, nullptr, 0.0f, 100.0f, ImVec2(0, 60));
        ImGui::PlotLines("RAM %", ramHistory, kHistoryLen, historyOffset, nullptr, 0.0f, 100.0f, ImVec2(0, 60));
        ImGui::PlotLines("Disk MB/s", diskHistory, kHistoryLen, historyOffset, nullptr, 0.0f, FLT_MAX, ImVec2(0, 60));

        ImGui::Text("Processes: %zu", processes.size());
        static char filterBuf[128] = "";
        ImGui::InputTextWithHint("##filter", "Filter by name...", filterBuf, IM_ARRAYSIZE(filterBuf));
        ImGui::Text("Total CPU: %.1f%%   Total RAM: %.1f%%", cpuHistory[historyOffset], ramHistory[historyOffset]);
        if (ImGui::BeginTable("ProcessTable", 5, ImGuiTableFlags_Borders | ImGuiTableFlags_RowBg | ImGuiTableFlags_ScrollY | ImGuiTableFlags_Sortable, ImVec2(0, 600))) {
            ImGui::TableSetupColumn("CPU %");
            ImGui::TableSetupColumn("PID");
            ImGui::TableSetupColumn("PPID");
            ImGui::TableSetupColumn("Working Set (KB)");
            ImGui::TableSetupColumn("Name");
            ImGui::TableSetupScrollFreeze(0, 1);
            ImGui::TableHeadersRow();

            std::vector<const ProcessInfo*> view;
            for (const auto& p : processes) {
                if (filterBuf[0] != 0) {
                    char narrow[256];
                    WideCharToMultiByte(CP_UTF8, 0, p.ImageName.c_str(), -1, narrow, sizeof(narrow), nullptr, nullptr);
                    if (StrStrIA(narrow, filterBuf) == nullptr) continue;
                }
                view.push_back(&p);
            }

            if (ImGuiTableSortSpecs* sortSpecs = ImGui::TableGetSortSpecs()) {
                if (sortSpecs->SpecsCount > 0) {
                    const auto& spec = sortSpecs->Specs[0];
                    bool asc = spec.SortDirection == ImGuiSortDirection_Ascending;
                    std::sort(view.begin(), view.end(), [&](const ProcessInfo* a, const ProcessInfo* b) {
                        switch (spec.ColumnIndex) {
                            case 0: return asc ? a->CpuPercent < b->CpuPercent : a->CpuPercent > b->CpuPercent;
                            case 1: return asc ? a->Pid < b->Pid : a->Pid > b->Pid;
                            case 2: return asc ? a->ParentPid < b->ParentPid : a->ParentPid > b->ParentPid;
                            case 3: return asc ? a->WorkingSetBytes < b->WorkingSetBytes : a->WorkingSetBytes > b->WorkingSetBytes;
                            case 4: return asc ? a->ImageName < b->ImageName : a->ImageName > b->ImageName;
                            default: return false;
                        }
                    });
                    sortSpecs->SpecsDirty = false;
                }
            }

            for (const auto* pp : view) {
                const auto& p = *pp;
                double elapsed = ImGui::GetTime() - (spawnTimes.count(p.Pid) ? spawnTimes[p.Pid] : -1000.0);
                float rowAlpha = (float)(elapsed < 0.4 ? (std::max)(0.0, elapsed) / 0.4 : 1.0);
                ImGui::PushStyleVar(ImGuiStyleVar_Alpha, rowAlpha);
                ImGui::TableNextRow();
                ImGui::TableSetColumnIndex(0);
                {
                    ImVec4 cpuColor = p.CpuPercent > 50.0f ? ImVec4(0.90f,0.30f,0.25f,1.0f)
                                     : p.CpuPercent > 20.0f ? ImVec4(0.90f,0.65f,0.15f,1.0f)
                                     : ImGui::GetStyleColorVec4(ImGuiCol_Text);
                    ImGui::TextColored(cpuColor, "%.1f%%", p.CpuPercent);
                }
                ImGui::TableSetColumnIndex(1); ImGui::Text("%u", p.Pid);
                ImGui::TableSetColumnIndex(2); ImGui::Text("%u", p.ParentPid);
                ImGui::TableSetColumnIndex(3); ImGui::Text("%llu", p.WorkingSetBytes / 1024);
                ImGui::TableSetColumnIndex(4);
                auto svcIt = servicesByPid.find(p.Pid);
                bool hasServices = (svcIt != servicesByPid.end() && !svcIt->second.empty());
                if (hasServices) {
                    ImGui::SetNextItemOpen(false, ImGuiCond_FirstUseEver);
                    void* icon = IconCache_Get(g_pd3dDevice, p.ImagePath);
                    if (icon) { ImGui::Image(icon, ImVec2(16, 16)); ImGui::SameLine(); }
                    bool open = ImGui::TreeNodeEx((void*)(intptr_t)p.Pid, ImGuiTreeNodeFlags_SpanAvailWidth, "%ls", p.ImageName.c_str());
                    if (open) {
                        for (const auto& svcName : svcIt->second) {
                            ImGui::BulletText("%ls", svcName.c_str());
                        }
                        ImGui::TreePop();
                    }
                } else {
                    ImGui::SameLine(0, 0);
                    ImGui::Text("%ls", p.ImageName.c_str());
                }
                ImGui::PushID((int)p.Pid);
                if (ImGui::BeginPopupContextItem("RowCtx")) {
                    if (ImGui::MenuItem("Terminate")) TerminateTargetProcess(p.Pid);
                    if (ImGui::MenuItem("Suspend")) SuspendTargetProcess(p.Pid);
                    if (ImGui::MenuItem("Resume")) ResumeTargetProcess(p.Pid);
                    if (ImGui::BeginMenu("Priority")) {
                        if (ImGui::MenuItem("Realtime")) SetTargetProcessPriority(p.Pid, REALTIME_PRIORITY_CLASS);
                        if (ImGui::MenuItem("High")) SetTargetProcessPriority(p.Pid, HIGH_PRIORITY_CLASS);
                        if (ImGui::MenuItem("Above Normal")) SetTargetProcessPriority(p.Pid, ABOVE_NORMAL_PRIORITY_CLASS);
                        if (ImGui::MenuItem("Normal")) SetTargetProcessPriority(p.Pid, NORMAL_PRIORITY_CLASS);
                        if (ImGui::MenuItem("Below Normal")) SetTargetProcessPriority(p.Pid, BELOW_NORMAL_PRIORITY_CLASS);
                        if (ImGui::MenuItem("Idle")) SetTargetProcessPriority(p.Pid, IDLE_PRIORITY_CLASS);
                        ImGui::EndMenu();
                    }
                    ImGui::EndPopup();
                }
                ImGui::PopID();
                ImGui::PopStyleVar();
            }
            ImGui::EndTable();
        }
        ImGui::End();

        ImGui::Render();
        const float clearColor[4] = { 0.08f, 0.08f, 0.10f, 1.0f };
        g_pd3dDeviceContext->OMSetRenderTargets(1, &g_mainRenderTargetView, nullptr);
        g_pd3dDeviceContext->ClearRenderTargetView(g_mainRenderTargetView, clearColor);
        ImGui_ImplDX11_RenderDrawData(ImGui::GetDrawData());

        g_pSwapChain->Present(1, 0);
    }

    IconCache_Shutdown();
    ImGui_ImplDX11_Shutdown();
    ImGui_ImplWin32_Shutdown();
    ImGui::DestroyContext();
    CleanupDeviceD3D();
    DestroyWindow(hwnd);
    UnregisterClassW(wc.lpszClassName, wc.hInstance);
    return 0;
}





























