#pragma once
#include <cstdint>

bool TerminateTargetProcess(uint32_t pid);
bool SuspendTargetProcess(uint32_t pid);
bool ResumeTargetProcess(uint32_t pid);
bool SetTargetProcessPriority(uint32_t pid, uint32_t priorityClass);
