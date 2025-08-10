using System.Diagnostics;
using System.Text;
using V10Sharp.ExtProcess.Windows;
using V10Sharp.Externals;
using System.Text.Json;
using System.Diagnostics.CodeAnalysis;

namespace V10Sharp.ExtProcess.Patterns;

public partial class PatternScanner
{

    public class MemCache : Dictionary<IntPtr, byte[]>;

    private readonly Process _process;
    private readonly bool _moduleAutoCache;
    private readonly MemCache? _memCache;
    private static bool _initialized = false;

    public PatternScanner(Process process, bool cache = true, MemCache? memCache = null, bool moduleAutoCache = false)
    {
        _process = process;
        _moduleAutoCache = moduleAutoCache;
        if (cache)
        {
            if (memCache == null)
                memCache = new();
            _memCache = memCache;
        }
        if (!_initialized)
        {
            PatternScanLazySIMD.Init();
            _initialized = true;
        }
    }

    public bool UpdateModuleCache(ProcessModule module)
    {
        if (_memCache == null)
            return false;
        var mem = new byte[module.ModuleMemorySize];
        if (!_process.ReadMemory(module.BaseAddress, mem))
            return false;

        _memCache[module.BaseAddress] = mem;
        return true;
    }

    public IntPtr FindPattern(IntPtr startAddress, long size, string pattern)
    {
        var compiled = CompilePattern(pattern);
        return FindPattern(startAddress, size, compiled.Item1, compiled.Item2);
    }

    public IntPtr FindPattern(IntPtr startAddress, long size, string pattern, string mask) =>
        FindPattern(startAddress, size, Encoding.Latin1.GetBytes(pattern), mask);

    public IntPtr FindPattern(IntPtr startAddress, long size, (byte[] pattern, string mask) pat) =>
         FindPattern(startAddress, size, pat.pattern, pat.mask);

    public IntPtr FindPattern(IntPtr startAddress, long size, byte[] cbPattern, string szMask)
    {
        IntPtr result = IntPtr.Zero;
        if (_memCache != null)
        {
            foreach ((var modBase, var memory) in _memCache)
            {
                if (startAddress >= modBase && modBase + memory.Length > startAddress)
                {
                    var offset = startAddress - modBase;
                    if (offset == 0)
                        result = FindPattern(_memCache[modBase], cbPattern, szMask);
                    else
                        result = FindPattern(_memCache[modBase][(Index)offset..], cbPattern, szMask);
                    return result == IntPtr.Zero ? result : modBase + offset + result;
                }
            }

            if (_moduleAutoCache)
            {
                foreach (ProcessModule module in _process.Modules)
                {
                    if (startAddress >= module.BaseAddress && module.BaseAddress + module.ModuleMemorySize > startAddress)
                    {
                        var newmem = new byte[size];
                        if (!_process.ReadMemory(startAddress, newmem))
                            break;  // maybe cant read all module mem, so skip to read below
                        _memCache[startAddress] = newmem;

                        var offset = startAddress - module.BaseAddress;
                        if (offset == 0)
                            result = FindPattern(_memCache[module.BaseAddress], cbPattern, szMask);
                        else
                            result = FindPattern(_memCache[module.BaseAddress][(Index)offset..], cbPattern, szMask);
                        return result == IntPtr.Zero ? result : module.BaseAddress + offset + result;
                    }
                }
            }
        }

        var mem = new byte[size];
        if (!_process.ReadMemory(startAddress, mem))
            return IntPtr.Zero;
        if (_memCache != null)
            _memCache[startAddress] = mem;
        result = FindPattern(mem, cbPattern, szMask);
        return result == IntPtr.Zero ? result : startAddress + result;
    }

    public unsafe IntPtr FindPattern(void* startAddress, long size, string pattern)
    {
        var compiled = CompilePattern(pattern);
        return FindPattern((IntPtr)startAddress, size, compiled.Item1, compiled.Item2);
    }

    public unsafe IntPtr FindPattern(void* startAddress, long size, string pattern, string mask) =>
        FindPattern((IntPtr)startAddress, size, Encoding.Latin1.GetBytes(pattern), mask);

    public unsafe IntPtr FindPattern(void* startAddress, long size, (byte[] pattern, string mask) pat) =>
         FindPattern((IntPtr)startAddress, size, pat.pattern, pat.mask);

    public unsafe IntPtr FindPattern(void* startAddress, long size, byte[] cbPattern, string szMask)
    {
        return FindPattern((IntPtr)startAddress, size, cbPattern, szMask);
    }

    public IntPtr FindPattern(ProcessModule module, string pattern)
    {
        var compiled = CompilePattern(pattern);
        return FindPattern(module.BaseAddress, module.ModuleMemorySize, compiled.Item1, compiled.Item2);
    }

    public IntPtr FindPattern(ProcessModule module, (byte[] pattern, string mask) pat) =>
        FindPattern(module.BaseAddress, module.ModuleMemorySize, pat.pattern, pat.mask);

    public IntPtr FindPattern(ProcessModule module, string pattern, string mask) =>
        FindPattern(module.BaseAddress, module.ModuleMemorySize, Encoding.Latin1.GetBytes(pattern), mask);

    public IntPtr FindPattern(ProcessModule module, byte[] cbPattern, string szMask) =>
        FindPattern(module.BaseAddress, module.ModuleMemorySize, cbPattern, szMask);

    private IntPtr FindPattern(byte[] cbMemory, byte[] cbPattern, string szMask)
    {
        return (IntPtr)PatternScanLazySIMD.FindPattern(cbMemory, cbPattern, szMask);
    }

    public MemCache? GetCache() => _memCache;

    public void ClearCache()
    {
        if (_memCache != null)
            _memCache.Clear();
    }

    public static (byte[], string) CompilePattern(string pattern)
    {
        string mask = "";
        string[] parts = pattern.Split(' ');
        byte[] bytesPattern = new byte[parts.Length];

        for (int i = 0; i < parts.Length; i++)
            if (parts[i] == "?")
            {
                mask += "?";
                parts[i] = "0";
            }
            else
                mask += "x";

        for (int i = 0; i < parts.Length; i++)
            bytesPattern[i] = Convert.ToByte(parts[i], 16);

        return (bytesPattern, mask);
    }

    public static string DumpPattern((byte[] pattern, string mask) pattern)
    {
        return $"([0x{BitConverter.ToString(pattern.pattern).Replace("-", ", 0x")}], \"{pattern.mask}\")";
    }

    public static string GenerateCode(string name, string pattern, bool @var=false)
    {
        var compiled = CompilePattern(pattern);
        if (@var)
            return $"var {name} = {DumpPattern(compiled)};";
        return $"ValueTuple<byte[], string> {name} = {DumpPattern(compiled)};";
    }

    [UnconditionalSuppressMessage("AssemblyLoadTrimming", "IL2026")]
    public static Dictionary<string, IntPtr> FindJsonPatterns(Process process, string json, MemCache? memCache = null)
    {
        var ps = new PatternScanner(process, true, memCache);
        var pats = JsonSerializer.Deserialize<Dictionary<string, JsonPattern>>(json)!;
        var results = new Dictionary<string, IntPtr>();
        foreach (var patName in pats.Keys)
        {
            results[patName] = IntPtr.Zero;
            var pattern = pats[patName];
            if (string.IsNullOrEmpty(pattern.Module) || string.IsNullOrEmpty(pattern.Pattern))
                continue;

            var module = process.GetModule(pattern.Module);
            if (module == null)
                continue;
            process.AddFindPatternModuleCache(module);

            IntPtr address;
            if (string.IsNullOrEmpty(pattern.Symbol) || pattern.SymbolLen <= 0)
                address = ps.FindPattern(module, pattern.Pattern);
            else
            {
                var export = process.GetModuleExport(module.BaseAddress, pattern.Symbol);
                if (export == IntPtr.Zero)
                    continue;
                address = ps.FindPattern(export, pattern.SymbolLen, pattern.Pattern);
            }

            if (address == IntPtr.Zero)
                continue;

            address += pattern.Offset;

            if (pattern.DerefType == JsonPattern.DereferenceType.Direct)
            {
                if (!process.ReadMemory(address, out address))
                    continue;
                address += pattern.DerefOffset;
            }
            else if (pattern.DerefType == JsonPattern.DereferenceType.Relative)
            {
                if (!process.ReadMemory<int>(address, out var relAddress))
                    continue;
                address += relAddress + (int)pattern.DerefRelativeSize;
                address += pattern.DerefOffset;
            }
            else if (pattern.DerefType != JsonPattern.DereferenceType.None)
                throw new NotImplementedException();

            if (pattern.Deref2Type == JsonPattern.DereferenceType.Direct)
            {
                if (!process.ReadMemory(address, out address))
                    continue;
            }
            else if (pattern.Deref2Type == JsonPattern.DereferenceType.Relative)
            {
                if (!process.ReadMemory<int>(address, out var relAddress))
                    continue;
                address += relAddress + (int)pattern.Deref2RelativeSize;
            }
            else if (pattern.Deref2Type != JsonPattern.DereferenceType.None)
                throw new NotImplementedException();
            results[patName] = address;
        }
        return results;
    }
}


