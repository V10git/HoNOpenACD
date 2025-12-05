using System.Diagnostics;
using static HoNOpenACD.Consts;
using V10Sharp.ExtProcess.Windows;

namespace HoNOpenACD
{
    public class HoN_CVar<T> where T : unmanaged
    {
        const ushort VALUES_OFFSET = 0x1D0;

        private Process Process;
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
                if (!IsValid || !Process.ReadMemory<T>(Ptr + 0x8, out var value))
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

        public HoN_CVar(Process Process, string module, string name)
        {
            this.Process = Process;

            var ename = "?" + name + "@@3V?$CCvar@" + GetTypeId() + "@@A";
            this.Ptr = Process.GetModuleExport(module, ename);
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
    }
}
