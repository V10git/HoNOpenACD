using System.Text;
using V10Sharp.Iced;
using UniCheat;

namespace HoNOpenACD
{
    public class HoN_wcstring : RCVar
    {
        const ushort WCSTRING_SIZE = 0x28;

        public HoN_wcstring(string val) : base(Encoding.GetEncoding("utf-16").GetBytes(val + "\x00"))
        {
            byte[] newValue;
            if (val.Length > 3)
            {
                newValue = new byte[WCSTRING_SIZE + val.Length * 2 + 2];
                Value.CopyTo(newValue, WCSTRING_SIZE);
            }
            else
            {
                newValue = new byte[WCSTRING_SIZE];
                Value.CopyTo(newValue, 0);
            }
            var bLen = BitConverter.GetBytes((ulong)val.Length);
            bLen.CopyTo(newValue, 0x10);
            bLen.CopyTo(newValue, 0x20);
            BitConverter.GetBytes((ulong)val.Length * 2).CopyTo(newValue, 0x18);
            Value = newValue;
        }

        public override void OnAllocated(IntPtr baseptr, CompiledResult compiled)
        {
            if (Value.Length == WCSTRING_SIZE)
                return;

            ulong offset = compiled[this];
            ulong ptr = (ulong)baseptr + offset + WCSTRING_SIZE;
            BitConverter.GetBytes(ptr).CopyTo(compiled, (int)offset);
        }
    }

}
