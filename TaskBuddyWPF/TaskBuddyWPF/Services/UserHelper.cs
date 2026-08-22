using System;
using System.Runtime.InteropServices;
using System.Text;
using TaskBuddyWPF.Native;

namespace TaskBuddyWPF.Services
{
    // Resolves the owning username for a PID via OpenProcessToken + LookupAccountSid.
    // Isolated from ProcessEnumerator deliberately: adding this to the Processes tab's
    // hot path would add per-tick token/SID overhead to a tab that doesn't need it.
    // Access-denied (protected system processes) is expected and non-fatal — returns null.
    public static class UserHelper
    {
        public static string? ResolveOwner(uint pid)
        {
            IntPtr hProcess = NativeMethods.OpenProcess(NativeMethods.PROCESS_QUERY_LIMITED_INFORMATION, false, pid);
            if (hProcess == IntPtr.Zero) return null;

            try
            {
                if (!NativeMethods.OpenProcessToken(hProcess, NativeMethods.TOKEN_QUERY, out IntPtr hToken))
                    return null;

                try
                {
                    NativeMethods.GetTokenInformation(hToken, NativeMethods.TokenUser, IntPtr.Zero, 0, out int size);
                    if (size == 0) return null;

                    IntPtr buffer = Marshal.AllocHGlobal(size);
                    try
                    {
                        if (!NativeMethods.GetTokenInformation(hToken, NativeMethods.TokenUser, buffer, size, out _))
                            return null;

                        IntPtr sid = Marshal.ReadIntPtr(buffer); // SID_AND_ATTRIBUTES.Sid is first field

                        var name = new StringBuilder(256);
                        var domain = new StringBuilder(256);
                        uint nameLen = (uint)name.Capacity;
                        uint domainLen = (uint)domain.Capacity;

                        if (!NativeMethods.LookupAccountSid(null, sid, name, ref nameLen, domain, ref domainLen, out _))
                            return null;

                        return domain.Length > 0 ? $"{domain}\\{name}" : name.ToString();
                    }
                    finally
                    {
                        Marshal.FreeHGlobal(buffer);
                    }
                }
                finally
                {
                    NativeMethods.CloseHandle(hToken);
                }
            }
            finally
            {
                NativeMethods.CloseHandle(hProcess);
            }
        }
    }
}
