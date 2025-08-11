using Iced.Intel;
using System.Runtime.InteropServices;
using System.Text;


namespace V10Sharp.Iced;

public class CompiledResult
{
    public required AssemblerResult Result;
    public required Dictionary<string, Label> Labels;
    public required byte[] Bytes;
    public uint Length { get => (uint)Bytes.Length; }

    public static implicit operator byte[](CompiledResult r) => r.Bytes;

    public unsafe static implicit operator byte*(CompiledResult r)
    {
        fixed (byte* buffer = r.Bytes)
            return buffer;
    }

    public IntPtr Offset(string label) => (IntPtr)Result.GetLabelRIP(Labels[label]);
    public IntPtr Offset(Label label) => (IntPtr)Result.GetLabelRIP(label);

    public unsafe ulong this[string label]
    {
        get
        {
            fixed (byte* buffer = Bytes)
                return *(ulong*)&buffer[Result.GetLabelRIP(Labels[label])];
        }
        set
        {
            fixed (byte* buffer = Bytes)
                *(ulong*)&buffer[Result.GetLabelRIP(Labels[label])] = value;
        }
    }

    public unsafe int this[string label, int @default]
    {
        get
        {
            fixed (byte* buffer = Bytes)
                return *(int*)&buffer[Result.GetLabelRIP(Labels[label])];
        }
        set
        {
            fixed (byte* buffer = Bytes)
                *(int*)&buffer[Result.GetLabelRIP(Labels[label])] = value;
        }
    }

    public unsafe uint this[string label, uint @default]
    {
        get
        {
            fixed (byte* buffer = Bytes)
                return *(uint*)&buffer[Result.GetLabelRIP(Labels[label])];
        }
        set
        {
            fixed (byte* buffer = Bytes)
                *(uint*)&buffer[Result.GetLabelRIP(Labels[label])] = value;
        }
    }

    public unsafe float this[string label, float @default]
    {
        get
        {
            fixed (byte* buffer = Bytes)
                return *(float*)&buffer[Result.GetLabelRIP(Labels[label])];
        }
        set
        {
            fixed (byte* buffer = Bytes)
                *(float*)&buffer[Result.GetLabelRIP(Labels[label])] = value;
        }
    }

    public unsafe string this[string label, string @default]
    {
        get
        {
            return string.Empty;
        }
        set
        {
            Update(label, value);
        }
    }

    public ulong this[Label label]
    {
        get
        {
            return Result.GetLabelRIP(label);
        }
    }

    public unsafe void Update<T>(string label, T value) where T : unmanaged
    {
        fixed (byte* buffer = Bytes)
            *(T*)&buffer[Offset(label)] = value;        
    }

    public unsafe void Update(string label, byte[] value)
    {
        var offset = (uint)Offset(label);
        fixed (byte* buffer = Bytes)
            for (ulong i = 0; i < (ulong)value.Length; i++)
                buffer[offset + i] = value[i];
    }

    public unsafe void Update(string label, string value)
    {
        var offset = (uint)Offset(label);
        var bytes = Encoding.ASCII.GetBytes(value);
        fixed (byte* buffer = Bytes)
        {
            for (ulong i = 0; i < (ulong)value.Length; i++)
                buffer[offset + i] = bytes[i];
            buffer[offset + (ulong)value.Length] = 0;
        }
    }

    public unsafe T Get<T>(Label label) where T : unmanaged
    {
        fixed (byte* buffer = Bytes)
            return *(T*)&buffer[Offset(label)];
    }

    public T Get<T>(string label) where T : unmanaged => Get<T>(Labels[label]);

    public unsafe byte[] Get(Label label, int len)
    {
        var result = new byte[len];
        var offset = (uint)Offset(label);
        fixed (byte* buffer = Bytes)
            for (ulong i = 0; i < (ulong)len; i++)
                result[i] = buffer[offset + i];
        return result;
    }

    public byte[] Get(string label, int len) => Get(Labels[label], len);


    public unsafe string Get(Label label)
    {
        var offset = (uint)Offset(label);
        fixed (byte* buffer = Bytes)
            return Marshal.PtrToStringAnsi((IntPtr)(&buffer[offset]))!;
    }

    public string Get(string label) => Get(Labels[label]);
}


public static class Extensions
{
    public static CompiledResult Compile(this Assembler asm, IntPtr rip = 0, Label[]? labels = default)
    {
        var ls = new Dictionary<string, Label>();
        if (labels != null && labels.Length > 0)
            foreach (var label in labels)
                ls[label.Name] = label;

        using MemoryStream ms = new MemoryStream();
        return new CompiledResult {
            Result = asm.Assemble(new StreamCodeWriter(ms), (ulong)rip, BlockEncoderOptions.ReturnNewInstructionOffsets | BlockEncoderOptions.ReturnRelocInfos),
            Bytes = ms.ToArray(),
            Labels = ls
        };
    }


    public static Label Func(this Assembler asm)
    {
        var result = asm.CreateLabel();
        asm.Label(ref result);
        return result;
    }

    public static void LabelHere(this Assembler asm, out Label label, string? name = null)
    {
        label = asm.CreateLabel(name);
        asm.Label(ref label);
    }

    public static Label Variable(this Assembler asm, uint value)
    {
        var result = asm.CreateLabel();
        asm.Label(ref result);
        asm.dd(value);
        return result;
    }

    public static Label Variable(this Assembler asm, int value)
    {
        var result = asm.CreateLabel();
        asm.Label(ref result);
        asm.dd(value);
        return result;
    }

    public static Label Variable(this Assembler asm, long value)
    {
        var result = asm.CreateLabel();
        asm.Label(ref result);
        asm.dd(value);
        return result;
    }

    public static Label Variable(this Assembler asm, float value)
    {
        var result = asm.CreateLabel();
        asm.Label(ref result);
        asm.dd(value);
        return result;
    }

    public static Label Variable(this Assembler asm, bool value)
    {
        var result = asm.CreateLabel();
        asm.Label(ref result);
        asm.dd(value ? 1 : 0);
        return result;
    }
}
