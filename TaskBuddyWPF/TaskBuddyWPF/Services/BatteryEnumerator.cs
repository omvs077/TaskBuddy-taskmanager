using System;
using System.Management;
using TaskBuddyWPF.Models;

namespace TaskBuddyWPF.Services
{
    // BatteryStatus values (Win32_Battery): 1=Discharging, 2=OnAC(not charging),
    // 3=Fully Charged, 4=Low, 5=Critical, 6-9=Charging variants, 10=Undefined,
    // 11=Partially Charged. Collapsed here into three user-facing states.
    public class BatteryEnumerator
    {
        public BatteryInfo GetSnapshot()
        {
            var info = new BatteryInfo();
            try
            {
                using var searcher = new ManagementObjectSearcher(
                    "SELECT EstimatedChargeRemaining, BatteryStatus FROM Win32_Battery");
                foreach (ManagementObject mo in searcher.Get())
                {
                    info.IsPresent = true;
                    info.ChargePercent = Convert.ToInt32(mo["EstimatedChargeRemaining"]);
                    uint status = Convert.ToUInt32(mo["BatteryStatus"]);
                    info.StatusText = status switch
                    {
                        1 => "Discharging",
                        2 => info.ChargePercent >= 100 ? "Fully charged" : "Plugged in, not charging",
                        3 => "Fully charged",
                        4 => "Low",
                        5 => "Critical",
                        6 => "Charging",
                        7 => "Charging (high)",
                        8 => "Charging (low)",
                        9 => "Charging (critical)",
                        11 => "Partially charged",
                        _ => "Unknown"
                    };
                    break;
                }
            }
            catch { /* non-fatal — no battery or WMI unavailable */ }
            return info;
        }
    }
}

