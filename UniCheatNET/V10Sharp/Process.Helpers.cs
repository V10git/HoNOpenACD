using System.Diagnostics;
using TerraFX.Interop.Windows;
using static TerraFX.Interop.Windows.Windows;
using System.Runtime.Versioning;
using V10Sharp.TerraFX;

namespace V10Sharp.ExtProcess.Windows;

public static class ProcessHelpers
{
    public static Process? GetProcess(string name)
    {
        return Process.GetProcesses().Where(p => p.ProcessName == name).FirstOrDefault();
    }

    public static bool TryGetProcess(string name, out Process? process) =>
        (process = GetProcess(name)) != null;

    public static bool WaitProcess(string name, out Process? process, Func<bool>? waiter)
    {
        while (!TryGetProcess(name, out process))
            if (waiter != null && !waiter())
                return false;
        return true;
    }

    [SupportedOSPlatform("windows")]
    public unsafe static bool EnableDebugPrivileges()
    {
        BOOL success = false;
        HANDLE tokenHandle;
        LUID luid;
        TOKEN_PRIVILEGES newPrivileges = new TOKEN_PRIVILEGES();

        if (OpenProcessToken((HANDLE)Process.GetCurrentProcess().Handle, TOKEN.TOKEN_ALL_ACCESS, &tokenHandle))
        {
            if (LookupPrivilegeValueW(null, SE.SE_DEBUG_NAME.ToPWChar(), &luid))
            {
                newPrivileges.PrivilegeCount = 1;
                newPrivileges.Privileges[0].Luid = luid;
                newPrivileges.Privileges[0].Attributes = SE.SE_PRIVILEGE_ENABLED;

                if (AdjustTokenPrivileges(tokenHandle, false, &newPrivileges, NULL, null, null))
                    success = true;
            }
            CloseHandle(tokenHandle);
        }

        return success;
    }
}
