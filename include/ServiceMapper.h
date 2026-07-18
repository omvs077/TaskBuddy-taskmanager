#pragma once
#include <cstdint>
#include <string>
#include <vector>
#include <unordered_map>

std::unordered_map<uint32_t, std::vector<std::wstring>> GetServicesByPid();
