using System;

namespace TaskBuddyWPF.Models
{
    public class ProcessInfo
    {
        public uint Pid { get; set; }
        public uint ParentPid { get; set; }
        public ulong WorkingSetBytes { get; set; }
        public long CpuTime100ns { get; set; }
        public double CpuPercent { get; set; }
        public string ImageName { get; set; } = string.Empty;
        public string ImagePath { get; set; } = string.Empty;
    }
}
