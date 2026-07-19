#include "IconCache.h"
#include <unordered_map>
#include <vector>
#include <shellapi.h>
#include <windows.h>

static std::unordered_map<std::wstring, ID3D11ShaderResourceView*> g_cache;

void* IconCache_Get(ID3D11Device* device, const std::wstring& path) {
    auto it = g_cache.find(path);
    if (it != g_cache.end()) return (void*)it->second;

    SHFILEINFOW sfi{};
    if (!SHGetFileInfoW(path.c_str(), 0, &sfi, sizeof(sfi), SHGFI_ICON | SHGFI_SMALLICON))
        return nullptr;

    ICONINFO ii{};
    if (!GetIconInfo(sfi.hIcon, &ii)) { DestroyIcon(sfi.hIcon); return nullptr; }

    BITMAP bmColor{};
    GetObject(ii.hbmColor, sizeof(bmColor), &bmColor);
    int w = bmColor.bmWidth, h = bmColor.bmHeight;

    BITMAPINFO bmi{};
    bmi.bmiHeader.biSize = sizeof(BITMAPINFOHEADER);
    bmi.bmiHeader.biWidth = w;
    bmi.bmiHeader.biHeight = -h;
    bmi.bmiHeader.biPlanes = 1;
    bmi.bmiHeader.biBitCount = 32;
    bmi.bmiHeader.biCompression = BI_RGB;

    std::vector<uint8_t> px(w * h * 4);
    HDC hdc = GetDC(nullptr);
    GetDIBits(hdc, ii.hbmColor, 0, h, px.data(), &bmi, DIB_RGB_COLORS);
    ReleaseDC(nullptr, hdc);
    DeleteObject(ii.hbmColor); DeleteObject(ii.hbmMask); DestroyIcon(sfi.hIcon);

    for (int i = 0; i < w * h; i++) std::swap(px[i*4], px[i*4+2]);

    D3D11_TEXTURE2D_DESC desc{};
    desc.Width = w; desc.Height = h; desc.MipLevels = 1; desc.ArraySize = 1;
    desc.Format = DXGI_FORMAT_R8G8B8A8_UNORM; desc.SampleDesc.Count = 1;
    desc.Usage = D3D11_USAGE_DEFAULT; desc.BindFlags = D3D11_BIND_SHADER_RESOURCE;
    D3D11_SUBRESOURCE_DATA sub{ px.data(), (UINT)(w * 4), 0 };

    ID3D11Texture2D* tex = nullptr;
    if (FAILED(device->CreateTexture2D(&desc, &sub, &tex))) return nullptr;

    ID3D11ShaderResourceView* srv = nullptr;
    D3D11_SHADER_RESOURCE_VIEW_DESC svd{};
    svd.Format = desc.Format; svd.ViewDimension = D3D11_SRV_DIMENSION_TEXTURE2D;
    svd.Texture2D.MipLevels = 1;
    device->CreateShaderResourceView(tex, &svd, &srv);
    tex->Release();

    g_cache[path] = srv;
    return (void*)srv;
}

void IconCache_Shutdown() {
    for (auto& kv : g_cache) if (kv.second) kv.second->Release();
    g_cache.clear();
}
