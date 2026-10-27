using System;
using System.Runtime.InteropServices;
using TaskBuddyWPF.Native;

namespace TaskBuddyWPF.Services
{
    // Power plan GUIDs are the four standard Windows schemes (confirmed via
    // a live "powercfg /l" listing). Any other GUID (a custom/duplicated
    // plan) falls back to "Custom".
    public static class PowerModeService
    {
        private static readonly Guid BalancedGuid = new("381b4222-f694-41f0-9685-ff5bb260df2e");
        private static readonly Guid HighPerformanceGuid = new("8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c");
        private static readonly Guid PowerSaverGuid = new("a1841308-3541-4fab-bc81-f71556f20b4a");
        private static readonly Guid UltimatePerformanceGuid = new("e9a42b02-d5df-448d-aa00-03f14749eb61");

        public static string GetActivePlanName()
        {
            IntPtr guidPtr = IntPtr.Zero;
            try
            {
                if (NativeMethods.PowerGetActiveScheme(IntPtr.Zero, out guidPtr) != 0 || guidPtr == IntPtr.Zero)
                    return "Unknown";

                Guid activeGuid = Marshal.PtrToStructure<Guid>(guidPtr);
                if (activeGuid == BalancedGuid) return "Balanced";
                if (activeGuid == HighPerformanceGuid) return "High performance";
                if (activeGuid == PowerSaverGuid) return "Power saver";
                if (activeGuid == UltimatePerformanceGuid) return "Ultimate performance";
                return "Custom";
            }
            catch { return "Unknown"; }
            finally
            {
                if (guidPtr != IntPtr.Zero) NativeMethods.LocalFree(guidPtr);
            }
        }

        public static bool IsBatterySaverOn()
        {
            try
            {
                if (NativeMethods.GetSystemPowerStatus(out var status))
                    return status.SystemStatusFlag == 1;
            }
            catch { }
            return false;
        }
    }
}

