#pragma once
#include <vector>
#include "ProcessInfo.h"

struct ResourceSample {
    float CpuPercent;
    float RamPercent;
    float DiskBytesPerSec;
};

ResourceSample SampleResources(const std::vector<ProcessInfo>& processes);
