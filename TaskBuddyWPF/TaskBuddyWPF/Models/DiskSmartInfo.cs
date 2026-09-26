namespace TaskBuddyWPF.Models
{
    public class DiskSmartInfo
    {
        public bool IsAvailable { get; set; }
        public int TemperatureCelsius { get; set; }
        public int PercentageUsed { get; set; }
        public int AvailableSparePercent { get; set; }
        public ulong PowerOnHours { get; set; }
        public ulong DataUnitsReadBytes { get; set; }
        public ulong DataUnitsWrittenBytes { get; set; }
    }
}
