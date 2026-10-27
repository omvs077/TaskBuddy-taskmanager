namespace TaskBuddyWPF.Models
{
    public class BatteryInfo
    {
        public bool IsPresent { get; set; }
        public int ChargePercent { get; set; }
        public string StatusText { get; set; } = "Unknown";
    }
}
