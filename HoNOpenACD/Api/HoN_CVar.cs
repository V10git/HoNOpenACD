using System.Diagnostics;
using UniCheat;
using V10Sharp.ExtProcess.Windows;
using V10Sharp.ExtProcess.Patterns;
using static V10Sharp.ExtConsole.Ansi;


namespace HoNOpenACD
{
    public class CVarPattern
    {
        public readonly string Module;
        public readonly string Pattern;
        public readonly int NameOffset;
        public readonly int Offset;

        public CVarPattern(string module, string pattern, int nameOffset, int cvarOffset)
        {
            Module = module;
            Pattern = pattern;
            NameOffset = nameOffset;
            Offset = cvarOffset;
        }
    }

    public class HoN_CVar<T> where T : unmanaged
    {
        public static readonly HoN_CVar<T> INVALID_CVAR = new HoN_CVar<T>(null!, string.Empty, IntPtr.Zero);

#if BUILD_REBORN
        const ushort VALUES_OFFSET = 0xE8;
#else
        const ushort VALUES_OFFSET = 0x1D0;
#endif

        public readonly Process Process;
        public readonly string Name;
        public IntPtr Ptr { get; private set; } = IntPtr.Zero;
        public IntPtr DefaultValuePtr { get; private set; } = IntPtr.Zero;

        public bool IsValid { get => Ptr != IntPtr.Zero; }

        public IntPtr ValuePtr
        {
            get
            {
                if (!IsValid)
                    return IntPtr.Zero;
                return DefaultValuePtr + 0x4;
            }
        }

        public T Value { 
            get
            {
                if (!IsValid || !Process.ReadMemory<T>(ValuePtr, out var value))
                    return default;
                return value;
            }
            set
            {
                if (!IsValid) return;

                Process.WriteMemory(DefaultValuePtr, value);
                Process.WriteMemory(ValuePtr, value);
                // unknown value copy
                Process.WriteMemory(ValuePtr + 0x4, value);
            }
        }

        public HoN_CVar(Process process, string name, IntPtr ptr)
        {
            Process = process;
            Ptr = ptr;
            Name = name;

            if (IsValid)
                DefaultValuePtr = Ptr + VALUES_OFFSET;
        }

        private static string GetTypeId()
        {
            if (typeof(T) == typeof(float))
                return "MM";
            else if (typeof(T) == typeof(uint))
                return "II";
            else if (typeof(T) == typeof(int))
                return "HH";
            else if (typeof(T) == typeof(bool))
                return "_N_N";
            throw new NotImplementedException($"CVar type \"{typeof(T)}\" not supported for now.");
        }

        public static HoN_CVar<T> CreateFromExport(Process process, string module, string name)
        {
            var ename = "?" + name + "@@3V?$CCvar@" + GetTypeId() + "@@A";
            return new HoN_CVar<T>(process, name, process.GetModuleExport(module, ename));
        }

        public static IntPtr CreateFromPattern(Process process, string name, CVarPattern pattern, 
            out HoN_CVar<T> cvar, IntPtr lastOffset = default, Func<string, bool>? waiter = null)
        {
            cvar = INVALID_CVAR;

            Dll? dll = Engine.GetModule(process, pattern.Module);
            if (dll == null)
                return IntPtr.Zero;

            var compiledPattern = PatternScanner.CompilePattern(pattern.Pattern);
            var scanStart = dll.Handle;
            IntPtr scanSize = (IntPtr)dll.Size;

            IntPtr TryOffset(IntPtr ptr)
            {
                // reading cvar name string and check
                var strPtr = process.CalcPtrFromRelative(ptr + pattern.NameOffset); 
                if (strPtr == IntPtr.Zero || !process.ReadMemoryWChars(strPtr, out var readedStr))
                    return IntPtr.Zero;
                if (readedStr != name)
                    return IntPtr.Zero;

                return process.CalcPtrFromRelative(ptr + pattern.Offset);
            }

            Console.Write($"Searching cvar {@Name(name)}...");

            // check cache value
            if (lastOffset != IntPtr.Zero)
            {
                var pCvar = TryOffset(dll.Handle + lastOffset); // calc with modulebase
                if (pCvar != IntPtr.Zero)
                {
                    AnsiPrint(@Good($"found in cache at {@Id(pCvar)}"));
                    cvar = new HoN_CVar<T>(process, name, pCvar);
                    return lastOffset;
                }
            }

            var scanner = dll.CreatePatternScanner(true, process.GetMemCache());
            while (true)
            {
                var candidate = scanner.FindPattern(scanStart, scanSize, compiledPattern);
                if (candidate == IntPtr.Zero)
                {
                    AnsiPrint(@Bad("not found."));
                    return IntPtr.Zero;
                }

                var pCvar = TryOffset(candidate);
                if (pCvar != IntPtr.Zero)
                {
                    AnsiPrint(@Good($"found at {@Id(pCvar)}"));
                    cvar = new HoN_CVar<T>(process, name, pCvar);
                    return candidate - dll.Handle; // return offset from module base
                }

                // try next
                candidate += 0x100; // 0x100 min func size for cvar init
                scanSize -= candidate - scanStart; // decreasing scan size
                scanStart = candidate;

                if (scanSize <= 0 || (waiter != null && !waiter(scanStart.ToString("X"))) || process.HasExited)
                {
                    AnsiPrint(@Bad("not found."));
                    return IntPtr.Zero;
                }
            }
        }
    }
}
