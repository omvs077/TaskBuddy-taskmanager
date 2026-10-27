using System;
using System.Management;

namespace TaskBuddyWPF.Services
{
    // DesignedCapacity and FullChargedCapacity live in the root\WMI namespace
    // (not root\cimv2). Health% is the standard OEM-tool definition: current
    // full-charge capacity vs. the capacity the battery shipped with.
    public static class BatteryStaticInfo
    {
        private static uint? _designedCapacity;
        public static uint DesignedCapacity
        {
            get
            {
                if (_designedCapacity == null)
                {
                    _designedCapacity = 0;
                    try
                    {
                        using var searcher = new ManagementObjectSearcher(
                            new ManagementScope(@"root\WMI"),
                            new ObjectQuery("SELECT DesignedCapacity FROM BatteryStaticData"));
                        foreach (ManagementObject mo in searcher.Get())
                        {
                            _designedCapacity = Convert.ToUInt32(mo["DesignedCapacity"]);
                            break;
                        }
                    }
                    catch { }
                }
                return _designedCapacity.Value;
            }
        }

        private static uint? _fullChargedCapacity;
        public static uint FullChargedCapacity
        {
            get
            {
                if (_fullChargedCapacity == null)
                {
                    _fullChargedCapacity = 0;
                    try
                    {
                        using var searcher = new ManagementObjectSearcher(
                            new ManagementScope(@"root\WMI"),
                            new ObjectQuery("SELECT FullChargedCapacity FROM BatteryFullChargedCapacity"));
                        foreach (ManagementObject mo in searcher.Get())
                        {
                            _fullChargedCapacity = Convert.ToUInt32(mo["FullChargedCapacity"]);
                            break;
                        }
                    }
                    catch { }
                }
                return _fullChargedCapacity.Value;
            }
        }

        public static int HealthPercent =>
            DesignedCapacity > 0 ? (int)Math.Round(FullChargedCapacity * 100.0 / DesignedCapacity) : 0;
    }
}
