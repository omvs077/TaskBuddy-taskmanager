using System;
using System.Linq;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;
using TaskBuddyWPF.Models;
using TaskBuddyWPF.Native;

namespace TaskBuddyWPF.Services
{
    // Adapter name, connection type, IP addresses, and throughput come from
    // System.Net.NetworkInformation (.NET stdlib) — SSID and signal quality
    // aren't exposed there, so those two fields specifically use the native
    // WLAN API. The WLAN client handle is opened once and reused (per
    // Microsoft's own guidance: WlanOpenHandle is a real RPC round-trip and
    // reopening every tick is wasteful) and closed via Dispose.
    public class WifiEnumerator : IDisposable
    {
        private IntPtr _wlanHandle = IntPtr.Zero;
        private bool _wlanAvailable;
        private (ulong sent, ulong received, DateTime timestamp)? _lastCounters;

        public WifiEnumerator()
        {
            try
            {
                uint result = (uint)NativeMethods.WlanOpenHandle(2, IntPtr.Zero, out _, out _wlanHandle);
                _wlanAvailable = result == 0;
            }
            catch
            {
                _wlanAvailable = false; // wlanapi.dll unavailable (e.g. Wi-Fi service disabled) — non-fatal
            }
        }

        public WifiInfo GetSnapshot()
        {
            var info = new WifiInfo();

            var adapter = NetworkInterface.GetAllNetworkInterfaces()
                .FirstOrDefault(ni => ni.NetworkInterfaceType == NetworkInterfaceType.Wireless80211
                    && ni.OperationalStatus == OperationalStatus.Up);

            if (adapter == null)
            {
                info.IsConnected = false;
                return info;
            }

            info.IsConnected = true;
            info.AdapterName = adapter.Name;
            info.ConnectionType = "Wi-Fi";

            var ipProps = adapter.GetIPProperties();
            info.IPv4Address = ipProps.UnicastAddresses
                .FirstOrDefault(a => a.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)?
                .Address.ToString() ?? "";
            info.IPv6Address = ipProps.UnicastAddresses
                .FirstOrDefault(a => a.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetworkV6)?
                .Address.ToString() ?? "";

            var stats = adapter.GetIPStatistics();
            var now = DateTime.UtcNow;
            ulong sent = (ulong)stats.BytesSent;
            ulong received = (ulong)stats.BytesReceived;

            if (_lastCounters.HasValue)
            {
                double elapsed = (now - _lastCounters.Value.timestamp).TotalSeconds;
                if (elapsed > 0)
                {
                    info.SendKbps = (sent - _lastCounters.Value.sent) * 8.0 / 1024.0 / elapsed;
                    info.ReceiveKbps = (received - _lastCounters.Value.received) * 8.0 / 1024.0 / elapsed;
                }
            }
            _lastCounters = (sent, received, now);

            if (_wlanAvailable)
            {
                var (ssid, signal) = ResolveSsidAndSignal();
                info.SSID = ssid;
                info.SignalQuality = signal;
            }

            return info;
        }

        private (string ssid, int signal) ResolveSsidAndSignal()
        {
            IntPtr ifListPtr = IntPtr.Zero;
            try
            {
                if (NativeMethods.WlanEnumInterfaces(_wlanHandle, IntPtr.Zero, out ifListPtr) != 0)
                    return ("", 0);

                int numberOfItems = Marshal.ReadInt32(ifListPtr);
                // WLAN_INTERFACE_INFO_LIST layout: dwNumberOfItems (4) + dwIndex (4) + InterfaceInfo[]
                IntPtr arrayStart = IntPtr.Add(ifListPtr, 8);
                int entrySize = Marshal.SizeOf<NativeMethods.WLAN_INTERFACE_INFO>();

                for (int i = 0; i < numberOfItems; i++)
                {
                    IntPtr entryPtr = IntPtr.Add(arrayStart, i * entrySize);
                    var ifInfo = Marshal.PtrToStructure<NativeMethods.WLAN_INTERFACE_INFO>(entryPtr);
                    if (ifInfo.isState != NativeMethods.WLAN_INTERFACE_STATE_CONNECTED) continue;

                    Guid guid = ifInfo.InterfaceGuid;
                    IntPtr dataPtr = IntPtr.Zero;
                    try
                    {
                        int queryResult = NativeMethods.WlanQueryInterface(
                            _wlanHandle, ref guid, NativeMethods.WLAN_INTF_OPCODE_CURRENT_CONNECTION,
                            IntPtr.Zero, out _, out dataPtr, IntPtr.Zero);
                        if (queryResult != 0) continue;

                        var connAttrs = Marshal.PtrToStructure<NativeMethods.WLAN_CONNECTION_ATTRIBUTES>(dataPtr);
                        var ssidStruct = connAttrs.wlanAssociationAttributes.dot11Ssid;
                        string ssid = ssidStruct.uSSIDLength > 0
                            ? System.Text.Encoding.UTF8.GetString(ssidStruct.ucSSID, 0, (int)ssidStruct.uSSIDLength)
                            : "";
                        return (ssid, (int)connAttrs.wlanAssociationAttributes.wlanSignalQuality);
                    }
                    finally
                    {
                        if (dataPtr != IntPtr.Zero) NativeMethods.WlanFreeMemory(dataPtr);
                    }
                }
                return ("", 0);
            }
            catch
            {
                return ("", 0); // non-fatal — Wi-Fi service unavailable, permissions, or driver quirk
            }
            finally
            {
                if (ifListPtr != IntPtr.Zero) NativeMethods.WlanFreeMemory(ifListPtr);
            }
        }

        public void Dispose()
        {
            if (_wlanHandle != IntPtr.Zero)
            {
                NativeMethods.WlanCloseHandle(_wlanHandle, IntPtr.Zero);
                _wlanHandle = IntPtr.Zero;
            }
        }
    }
}
