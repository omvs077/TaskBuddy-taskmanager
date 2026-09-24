namespace TaskBuddyWPF.Models
{
    public class MemoryDetailInfo
    {
        public ulong InUseBytes { get; set; }
        public ulong AvailableBytes { get; set; }
        public ulong CommittedBytes { get; set; }
        public ulong CachedBytes { get; set; }
        public ulong PagedPoolBytes { get; set; }
        public ulong NonPagedPoolBytes { get; set; }
        public ulong CompressedBytes { get; set; }
        public ulong TotalBytes { get; set; }
        public uint SpeedMhz { get; set; }
        public int SlotsUsed { get; set; }
        public string FormFactor { get; set; } = "";
        public ulong HardwareReservedBytes { get; set; }
    }
}
