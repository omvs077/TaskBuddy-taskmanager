#pragma once
#include <d3d11.h>
#include <string>
void* IconCache_Get(ID3D11Device* device, const std::wstring& path);
void IconCache_Shutdown();
