using Iced.Intel;
using System.Text;
using System.Diagnostics.CodeAnalysis;
using V10Sharp.Iced;

namespace UniCheat;

public class RCVar
{
    public Label? Label = null;
    public byte[] Value;
    protected string? _name;

    protected RCVar()
    {
        Value = Array.Empty<byte>();
    }

    public RCVar(byte[] value, string? name = null) =>
        Init(value, name);

    public RCVar(string value, string? name = null, Encoding? encoding = null) =>
        Init((encoding ?? Encoding.UTF8).GetBytes(value + "\x00"), name);

    public RCVar(long value, string? name = null) =>
        Init(BitConverter.GetBytes(value), name);

    public RCVar(float value, string? name = null) =>
        Init(BitConverter.GetBytes(value), name);

    public RCVar(IntPtr value, string? name = null) =>
        Init(BitConverter.GetBytes(value), name);

    [MemberNotNull(nameof(Value))]
    protected virtual RCVar Init(byte[] value, string? name = null)
    {
        Value = value;
        _name = name;
        return this;
    }

    public virtual Label CreateLabel(Assembler asm)
    {
        Label = asm.CreateLabel(_name);
        return Label.Value;
    }

    public static implicit operator byte[](RCVar cv) => cv.Value;
    public static implicit operator Label(RCVar cv) => cv.Label!.Value;

    public virtual void OnPrologue(Assembler asm) { }
    public virtual void OnEpilogue(Assembler asm) { }

    public virtual void OnAllocated(IntPtr baseptr, CompiledResult compiled) { }
    public virtual void OnBeforeDeallocated(IntPtr baseptr, CompiledResult compiled) { }
}