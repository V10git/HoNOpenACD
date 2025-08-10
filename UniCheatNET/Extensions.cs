using System.Diagnostics;


namespace UniCheat;

public static class Extensions
{
    public static bool InjectDll(this Process process, string dll) => 
        RCFunction.Call(process, "kernel32.dll", "LoadLibraryA", dll) != IntPtr.Zero;
}
