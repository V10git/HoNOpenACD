using System.Diagnostics;
using System.Text;
using V10Sharp.ExtProcess.Windows;
using static V10Sharp.ExtProcess.Patterns.PatternScanner;

namespace V10Sharp.ExtProcess.Patterns;

public static class ProcessExtensions
{
    public static readonly Dictionary<int, MemCache> PatternsMemCache = new();

    public static MemCache? GetMemCache(this Process process, bool create = true)
    {
        if (!PatternsMemCache.ContainsKey(process.Id))
        {
            if (create)
                return PatternsMemCache[process.Id] = new();
            return null;
        }
        return PatternsMemCache[process.Id];
    }

    public static PatternScanner CreatePatternScanner(this Process process, bool cache = true, bool moduleAutoCache = false, MemCache? memCache = null) =>
        new PatternScanner(process, cache, memCache ?? process.GetMemCache(), moduleAutoCache);

    private static PatternScanner GetScanner(this Process process, bool moduleAutoCache = true) => 
        new PatternScanner(process, true, process.GetMemCache(), moduleAutoCache);

    public static Dictionary<string, IntPtr> FindJsonPatterns(this Process process, string json, MemCache? memCache = null) =>
        PatternScanner.FindJsonPatterns(process, json, memCache ?? process.GetMemCache());

    public static Dictionary<string, IntPtr> FindPatternsFromFile(this Process process, string filename, MemCache? memCache = null) =>
        PatternScanner.FindJsonPatterns(process, File.ReadAllText(filename), memCache ?? process.GetMemCache());


    public static bool TryFindPattern(this Process process, out IntPtr ptr, IntPtr startAddress, long size, string pattern)
    {
        var compiled = CompilePattern(pattern);
        return (ptr = process.GetScanner().FindPattern(startAddress, size, compiled.Item1, compiled.Item2)) != IntPtr.Zero;
    }

    public static bool TryFindPattern(this Process process, out IntPtr ptr, IntPtr startAddress, long size, string pattern, string mask) =>
        (ptr = process.GetScanner().FindPattern(startAddress, size, Encoding.Latin1.GetBytes(pattern), mask)) != IntPtr.Zero;

    public static bool TryFindPattern(this Process process, out IntPtr ptr, IntPtr startAddress, long size, (byte[] pattern, string mask) pat) =>
        (ptr = process.GetScanner().FindPattern(startAddress, size, pat.pattern, pat.mask)) != IntPtr.Zero;

    public static bool TryFindPattern(this Process process, out IntPtr ptr, IntPtr startAddress, long size, byte[] cbPattern, string szMask) =>
        (ptr = process.GetScanner().FindPattern(startAddress, size, cbPattern, szMask)) != IntPtr.Zero;


    public static unsafe bool TryFindPattern(this Process process, out IntPtr ptr, void* startAddress, long size, string pattern)
    {
        var compiled = CompilePattern(pattern);
        return (ptr = process.GetScanner().FindPattern((IntPtr)startAddress, size, compiled.Item1, compiled.Item2)) != IntPtr.Zero;
    }

    public static unsafe bool TryFindPattern(this Process process, out IntPtr ptr, void* startAddress, long size, string pattern, string mask) =>
        (ptr = process.GetScanner().FindPattern((IntPtr)startAddress, size, Encoding.Latin1.GetBytes(pattern), mask)) != IntPtr.Zero;

    public static unsafe bool TryFindPattern(this Process process, out IntPtr ptr, void* startAddress, long size, (byte[] pattern, string mask) pat) =>
        (ptr = process.GetScanner().FindPattern((IntPtr)startAddress, size, pat.pattern, pat.mask)) != IntPtr.Zero;

    public static unsafe bool TryFindPattern(this Process process, out IntPtr ptr, void* startAddress, long size, byte[] cbPattern, string szMask) =>
        (ptr = process.GetScanner().FindPattern((IntPtr)startAddress, size, cbPattern, szMask)) != IntPtr.Zero;


    public static bool TryFindPattern(this Process process, out IntPtr ptr, ProcessModule module, string pattern)
    {
        var compiled = CompilePattern(pattern);
        return (ptr = process.GetScanner(false).FindPattern(module.BaseAddress, module.ModuleMemorySize, compiled.Item1, compiled.Item2)) != IntPtr.Zero;
    }

    public static bool TryFindPattern(this Process process, out IntPtr ptr, ProcessModule module, string pattern, string mask) =>
        (ptr = process.GetScanner(false).FindPattern(module.BaseAddress, module.ModuleMemorySize, Encoding.Latin1.GetBytes(pattern), mask)) != IntPtr.Zero;

    public static bool TryFindPattern(this Process process, out IntPtr ptr, ProcessModule module, (byte[] pattern, string mask) pat) =>
        (ptr = process.GetScanner(false).FindPattern(module.BaseAddress, module.ModuleMemorySize, pat.pattern, pat.mask)) != IntPtr.Zero;

    public static bool TryFindPattern(this Process process, out IntPtr ptr, ProcessModule module, byte[] cbPattern, string szMask) =>
        (ptr = process.GetScanner(false).FindPattern(module.BaseAddress, module.ModuleMemorySize, cbPattern, szMask)) != IntPtr.Zero;


    public static bool AddFindPatternModuleCache(this Process process, ProcessModule module)
    {
        if (PatternsMemCache.ContainsKey(process.Id) && PatternsMemCache[process.Id].ContainsKey(module.BaseAddress))
            return true;
        return process.UpdateFindPatternModuleCache(module);
    }

    public static bool UpdateFindPatternModuleCache(this Process process, ProcessModule module)
    {
        var mem = new byte[module.ModuleMemorySize];
        if (!process.ReadMemory(module.BaseAddress, mem))
            return false;

        if (!PatternsMemCache.ContainsKey(process.Id))
            PatternsMemCache[process.Id] = new();
        PatternsMemCache[process.Id][module.BaseAddress] = mem;
        return true;
    }

    public static void ClearFindPatternMemCaches(this Process process)
    {
        PatternsMemCache.Remove(process.Id);
    }
}


