using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using TerraFX.Interop.Windows;
using static TerraFX.Interop.Windows.MEM;
using static TerraFX.Interop.Windows.PAGE;
using static TerraFX.Interop.Windows.PROCESS;
using static TerraFX.Interop.Windows.Windows;
using Iced.Intel;
using static Iced.Intel.AssemblerRegisters;
using V10Sharp.TerraFX;
using V10Sharp.Iced;


namespace V10Sharp.ExtProcess.Windows;

[SupportedOSPlatform("windows")]
public static class Extensions
{
    public static unsafe IntPtr Alloc<T>(this Process process, nuint count, uint protect = PAGE_EXECUTE_READWRITE) where T : unmanaged
    {
        HANDLE hProcess = OpenProcess(PROCESS_VM_OPERATION | PROCESS_VM_READ | PROCESS_VM_WRITE, false, (uint)process.Id);
        if (hProcess == IntPtr.Zero)
            ThrowForLastError();
        try
        {
            var size = (uint)sizeof(T) * count;
            return (IntPtr)VirtualAllocEx(hProcess, null, size, MEM_RESERVE | MEM_COMMIT, protect);
        }
        finally
        {
            CloseHandle(hProcess);
        }
    }

    public static unsafe bool Free(this Process process, IntPtr address) =>  Free(process, (void*)address);
    public static unsafe bool Free(this Process process, void* address)
    {
        HANDLE hProcess = OpenProcess(PROCESS_VM_OPERATION | PROCESS_VM_READ | PROCESS_VM_WRITE, false, (uint)process.Id);
        if (hProcess == IntPtr.Zero)
            ThrowForLastError();
        try
        {
            return VirtualFreeEx(hProcess, address, 0, MEM_RELEASE);
        }
        finally
        {
            CloseHandle(hProcess);
        }
    }

    public static unsafe bool ReadMemory<T>(this Process process, IntPtr address, out T value, T @default = default) where T : unmanaged
    {
        T v = @default;
        var result = ReadMemory(process, (void*)address, (byte*)&v, sizeof(T));
        value = v;
        return result;
    }

    public static unsafe bool ReadMemory(this Process process, IntPtr address, byte[] bytes)
    {
        fixed (byte* buffer = bytes)
            return ReadMemory(process, (void*)address, buffer, bytes.Length);
    }

    public static unsafe bool ReadMemory(this Process process, void* address, byte[] bytes)
    {
        fixed (byte* buffer = bytes)
            return ReadMemory(process, address, buffer, bytes.Length);
    }

    public static unsafe bool ReadMemory(this Process process, IntPtr address, byte* buffer, int bufferLength) =>
        ReadMemory(process, (void*)address, buffer, bufferLength);

    public static unsafe bool ReadMemory(this Process process, void* address, byte* buffer, int bufferLength)
    {
        var hProcess = OpenProcess(PROCESS_VM_OPERATION | PROCESS_VM_READ, false, (uint)process.Id);
        if (hProcess == HANDLE.NULL)
            ThrowForLastError();
        try { return ReadProcessMemory(hProcess, address, buffer, (nuint)bufferLength, null); }
        finally { CloseHandle(hProcess); }
    }

    public static unsafe void WriteMemory<T>(this Process process, IntPtr address, T value, bool restoreProtect = true) where T : unmanaged
    {
        WriteMemory(process, (void*)address, (byte*)&value, sizeof(T), restoreProtect);
    }

    public static unsafe void WriteMemory(this Process process, IntPtr address, byte[] bytes, bool restoreProtect = false)
    {
        fixed (byte* buffer = bytes)
        {
            WriteMemory(process, (void*)address, buffer, bytes.Length, restoreProtect);
        }
    }

    public static unsafe void WriteMemory(this Process process, void* address, byte[] bytes, bool restoreProtect = false)
    {
        fixed (byte* buffer = bytes)
        {
            WriteMemory(process, address, buffer, bytes.Length, restoreProtect);
        }
    }

    public static unsafe void WriteMemory(this Process process, IntPtr address, byte* buffer, int bufferLength, bool restoreProtect = false) =>
        WriteMemory(process, (void*)address, buffer, bufferLength, restoreProtect);

    public static unsafe void WriteMemory(this Process process, void* address, byte* buffer, int bufferLength, bool restoreProtect = false)
    {
        var hProcess = OpenProcess(PROCESS_VM_OPERATION | PROCESS_VM_READ | PROCESS_VM_WRITE, true, (uint)process.Id);
        if (hProcess == HANDLE.NULL)
            ThrowForLastError();

        try
        {
            uint old;
            var protectLen = bufferLength > 256 ? bufferLength : 256;
            if (!VirtualProtectEx(hProcess, address, (nuint)protectLen, PAGE_EXECUTE_READWRITE, &old))
                ThrowForLastError();

            if (!WriteProcessMemory(hProcess, address, buffer, (nuint)bufferLength, null))
                ThrowForLastError();

            if (restoreProtect)
            {
                uint protect;
                if (!VirtualProtectEx(hProcess, address, (nuint)protectLen, old, &protect))
                    ThrowForLastError();
            }

            if (!FlushInstructionCache(hProcess, address, (nuint)bufferLength))
                ThrowForLastError();
        }
        finally
        {
            CloseHandle(hProcess);
        }
    }

    public static unsafe int RunThread(this Process process, IntPtr address, void* parameter = null)
    {
        int exitCode;
        var hProcess = OpenProcess(PROCESS_CREATE_THREAD, false, (uint)process.Id);
        if (hProcess == HANDLE.NULL)
            ThrowForLastError();
        try
        {
            HANDLE hThread = CreateRemoteThread(hProcess, null, 1000000, (delegate* unmanaged<void*, uint>)address, parameter, 0, null);
            if (hThread == HANDLE.NULL)
                ThrowForLastError();

            try
            {
                if (WaitForSingleObject(hThread, INFINITE) == WAIT.WAIT_FAILED)
                    ThrowForLastError();

                if (!GetExitCodeThread(hThread, (uint*)&exitCode))
                    ThrowForLastError();
            }
            finally
            {
                CloseHandle(hThread);
            }
        }
        finally
        {
            CloseHandle(hProcess);
        }

        return exitCode;
    }

    public static unsafe void ApplyPatch(this Process process, void* address, Action<Assembler> generator)
    {
        var asm = new Assembler(64);
        generator(asm);
        WriteMemory(process, address, asm.Compile(), true);
    }

    public static unsafe CompiledResult Execute(this Process process, Action<Assembler> generator)
    {
        var asm = new Assembler(64);
        generator(asm);
        var result = asm.Compile();
        var codeBase = Alloc<byte>(process, result.Length);
        WriteMemory(process, codeBase, result);
        RunThread(process, codeBase);
        ReadMemory(process, codeBase, result);
        Free(process, codeBase);
        return result;
    }

    public static bool IsStillRunning(int processId)
    {
        try
        {
            Process.GetProcessById(processId);
        }
        catch (ArgumentException)
        {
            return false;
        }
        return true;
    }

    private static CompiledResult? RemoteGPA = null;

    public static ProcessModule GetModule(this Process process, string module)
    {
        module = module.ToLower();
        for (int i = 0; i < process.Modules.Count; i++)
            if (process.Modules[i].ModuleName.ToLower() == module)
                return process.Modules[i];
        return null!;
    }

    public static IntPtr GetModuleBase(this Process process, string module)
    {
        var mo = GetModule(process, module);
        if (mo == null)
            return IntPtr.Zero;
        return mo.BaseAddress;
    }

    public static unsafe IntPtr GetModuleExport(this Process process, string module, string export, bool remoteCall = false) => 
        GetModuleExport(process, GetModuleBase(process, module), export, remoteCall);

    public static unsafe IntPtr GetModuleExport(this Process process, IntPtr hModule, string export, bool remoteCall = false)
    {
        if (hModule == IntPtr.Zero)
            return IntPtr.Zero;

        ArgumentException.ThrowIfNullOrEmpty(nameof(export));

        if (remoteCall)
        {
            if (RemoteGPA == null)
                CompileRemoteGPA();

            RemoteGPA["hModule"] = (ulong)hModule;
            RemoteGPA["sFuncName", export] = export;

            var codeBase = Alloc<byte>(process, RemoteGPA.Length);
            WriteMemory(process, codeBase, RemoteGPA);
            RunThread(process, codeBase);

            ReadMemory(process, codeBase + RemoteGPA.Offset("pResult"), out ulong result);
            Free(process, codeBase);
            return (IntPtr)result;
        }
        var hProcess = OpenProcess(PROCESS_VM_READ, false, (uint)process.Id);
        if (hProcess == IntPtr.Zero)
            ThrowForLastError();

        try
        {
            var pFuncName = Marshal.StringToHGlobalAnsi(export);

            try
            {
                
                int dosHeader_e_lfanew = 0;
                //e_lfanew = 0x3c
                if (!ReadProcessMemory(hProcess, (byte*)hModule + 0x3c, &dosHeader_e_lfanew, 4, null))
                    return IntPtr.Zero;

                IMAGE_NT_HEADERS64 nt;
                if (!ReadProcessMemory(hProcess, (byte*)hModule + dosHeader_e_lfanew, &nt, (uint)sizeof(IMAGE_NT_HEADERS64), null))
                    return IntPtr.Zero;

                if (nt.OptionalHeader.NumberOfRvaAndSizes <= IMAGE.IMAGE_DIRECTORY_ENTRY_EXPORT)
                    return IntPtr.Zero;

                IMAGE_DATA_DIRECTORY* idd = &nt.OptionalHeader.DataDirectory[IMAGE.IMAGE_DIRECTORY_ENTRY_EXPORT];
                if (idd->VirtualAddress == 0)
                    return IntPtr.Zero;

                IMAGE_EXPORT_DIRECTORY ied;
                if (!ReadProcessMemory(hProcess, (byte*)hModule + idd->VirtualAddress, &ied, (uint)sizeof(IMAGE_EXPORT_DIRECTORY), null))
                    return IntPtr.Zero;

                uint? dwOrdinal = null;
                // Ordinal exports
                if (HIWORD((nuint)pFuncName) == 0)
                    dwOrdinal = LOWORD((nuint)pFuncName) - ied.Base;
                // Named functions
                else
                    dwOrdinal = GetExportOrdinal(hProcess, hModule, &ied, idd, (sbyte*)pFuncName);

                if (dwOrdinal == null || dwOrdinal >= ied.NumberOfFunctions)
                    return IntPtr.Zero;

                uint func;
                if (!ReadProcessMemory(hProcess, (byte*)hModule + ied.AddressOfFunctions + (sizeof(uint) * dwOrdinal.Value), &func, sizeof(uint), null))
                    return IntPtr.Zero;

                return hModule + (IntPtr)func;
            }
            finally
            {
                Marshal.FreeHGlobal(pFuncName);
            }
        }
        finally
        {
            CloseHandle(hProcess);
        }
    }

    [MemberNotNull(nameof(RemoteGPA))]
    private unsafe static void CompileRemoteGPA()
    {
        IntPtr pGPA = (IntPtr)GetProcAddress(GetModuleHandleW("kernel32.dll".ToPWChar()), "GetProcAddress"u8.ToPChar());
        if (pGPA == IntPtr.Zero)
            ThrowForLastError();

        Assembler asm = new Assembler(64);
        var lbModule = asm.CreateLabel("hModule");
        var lbFuncName = asm.CreateLabel("sFuncName");
        var lbResult = asm.CreateLabel("pResult");
        asm.sub(rsp, 0x28);
        asm.mov(rcx, __qword_ptr[lbModule]);
        asm.lea(rdx, __qword_ptr[lbFuncName]);
        asm.mov(rax, pGPA);
        asm.call(rax);
        asm.add(rsp, 0x28);
        asm.mov(__qword_ptr[lbResult], rax);
        asm.ret();
        asm.int3();
        asm.Label(ref lbModule);
        asm.dq(0);
        asm.Label(ref lbResult);
        asm.dq(0);
        asm.Label(ref lbFuncName);
        asm.db(new byte[100]);

        RemoteGPA = asm.Compile(0, [lbModule, lbFuncName, lbResult]);
    }

    private static unsafe ushort? GetExportOrdinal(HANDLE hProcess, IntPtr hModule, IMAGE_EXPORT_DIRECTORY* pExport, IMAGE_DATA_DIRECTORY* pDirectory, sbyte* pName)
    {
        var names = (uint*)NativeMemory.Alloc(pExport->NumberOfNames * sizeof(uint));
        var ordinals = (ushort*)NativeMemory.Alloc(pExport->NumberOfNames * sizeof(ushort));

        // Temporary buffer to store the export name for comparison.
        var length = (uint)lstrlenA(pName);
        var buffer = (sbyte*)NativeMemory.Alloc(length + 1);
        try
        {
            if (!ReadProcessMemory(hProcess, (byte*)hModule + pExport->AddressOfNames, names, pExport->NumberOfNames * sizeof(uint), null))
                return null;

            if (!ReadProcessMemory(hProcess, (byte*)hModule + pExport->AddressOfNameOrdinals, ordinals, pExport->NumberOfNames * sizeof(ushort), null))
                return null;

            for (uint i = 0; i < pExport->NumberOfNames; i++)
            {
                if (!ReadProcessMemory(hProcess, (byte*)hModule + names[i], buffer, length + 1, null))
                    continue;

                if (StrCmpNIA(pName, buffer, (int)length) != 0)
                    continue;

                return ordinals[i];
            }
            return null;
        }
        finally
        {
            NativeMemory.Free(buffer);
            NativeMemory.Free(ordinals);
            NativeMemory.Free(names);
        }
    }

    public static void ThrowForLastError()
    {
        Marshal.ThrowExceptionForHR(HRESULT_FROM_WIN32(Marshal.GetLastSystemError()));
    }
}
