using System.Diagnostics;
using System.Text;
using V10Sharp.ExtProcess.Patterns;
using V10Sharp.ExtProcess.Windows;
using static V10Sharp.ExtProcess.Patterns.PatternScanner;
using static V10Sharp.ExtConsole.Ansi;


namespace UniCheat;

public class Dll: IFormattable
{
    public readonly string Filename;
    public nint Handle => Module.BaseAddress;
    public long Size => Module.ModuleMemorySize;

    public MemCache? PatternScanMemCache { get; set; } = null;
    public byte[]? MemCache { 
        get
        {
            if (PatternScanMemCache != null && PatternScanMemCache.ContainsKey(Handle))
                return PatternScanMemCache[Handle];
            return null;
        }
    }

    public readonly ProcessModule Module;
    private readonly Process _process;

    public Dll(Process process, string filename, ProcessModule module)
    {
        Filename = filename;
        Module = module;
        _process = process;
    }

    public bool TryGetExport(string name, out nint ptr, bool showError = true)
    {
        ptr = _process.GetModuleExport(Handle, name);
        var result = ptr != nint.Zero;
        if (!result && showError)
        {
            Engine.ShowError($"Export {@Name(name)} not found in {@Name(Filename)} ( {@Id(Handle)} )");
        }
        return result;
    }

    public IntPtr GetExport(string name) => _process.GetModuleExport(Handle, name);

    public void PatternScanSetGlobalCache() => PatternScanMemCache = _process.GetMemCache()!;

    public PatternScanner CreatePatternScanner(bool moduleAutoCache = false, MemCache? memCache = null)
    {
        if (memCache == null)
        {
            if (PatternScanMemCache == null)
                PatternScanMemCache = new MemCache();
            memCache = PatternScanMemCache;
        }
        return new PatternScanner(_process, true, memCache, moduleAutoCache);
    }

    public IntPtr FindPattern(string pattern) =>
        CreatePatternScanner().FindPattern(Handle, Size, CompilePattern(pattern));

    public IntPtr FindPattern(string pattern, string mask) =>
        CreatePatternScanner().FindPattern(Handle, Size, Encoding.Latin1.GetBytes(pattern), mask);

    public IntPtr FindPattern((byte[] pattern, string mask) pat) =>
        CreatePatternScanner().FindPattern(Handle, Size, pat.pattern, pat.mask);

    public IntPtr FindPattern(byte[] cbPattern, string szMask) =>
        CreatePatternScanner().FindPattern(Handle, Size, cbPattern, szMask);

    public bool TryFindPattern(out IntPtr ptr, string pattern) =>
        (ptr = CreatePatternScanner().FindPattern(Handle, Size, CompilePattern(pattern))) != IntPtr.Zero;

    public bool TryFindPattern(out IntPtr ptr, string pattern, string mask) =>
        (ptr = CreatePatternScanner().FindPattern(Handle, Size, Encoding.Latin1.GetBytes(pattern), mask)) != IntPtr.Zero;

    public bool TryFindPattern(out IntPtr ptr, (byte[] pattern, string mask) pat) =>
        (ptr = CreatePatternScanner().FindPattern(Handle, Size, pat.pattern, pat.mask)) != IntPtr.Zero;

    public bool TryFindPattern(out IntPtr ptr, byte[] cbPattern, string szMask) =>
        (ptr = CreatePatternScanner().FindPattern(Handle, Size, cbPattern, szMask)) != IntPtr.Zero;

    public void ClearPatternScanCache(bool cleanGlobal = false)
    {
        if (cleanGlobal && PatternScanMemCache != null)
        {
            PatternScanMemCache.Clear();
        }
        PatternScanMemCache = null!;
    }

    public IntPtr RCall(string function, params object?[]? args) =>
        RCFunction.Call(_process, Handle, function, args);

    public static implicit operator IntPtr(Dll dll)
    {
        return dll.Handle;
    }

    public static implicit operator long(Dll dll)
    {
        return dll.Handle;
    }

    public static implicit operator MemCache?(Dll dll)
    {
        return dll.PatternScanMemCache!;
    }

    public static implicit operator byte[]?(Dll dll)
    {
        return dll.MemCache;
    }

    public override string ToString()
    {
        return $"{Filename} at 0x{Handle:X}";
    }

    public string ToString(string? format, IFormatProvider? formatProvider)
    {
        if (string.IsNullOrEmpty(format))
            return ToString();
        return Handle.ToString(format, formatProvider);
    }
}
