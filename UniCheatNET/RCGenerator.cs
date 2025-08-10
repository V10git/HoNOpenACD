using System.Data;
using System.Collections.ObjectModel;
using Iced.Intel;
using static Iced.Intel.AssemblerRegisters;
using V10Sharp.Iced;

namespace UniCheat;

public class RCGenerator
{
    protected readonly Assembler asm = new Assembler(64);
    protected readonly Dictionary<RCVar, Label> vars = new Dictionary<RCVar, Label>();
    protected readonly ulong funcPtr;
    public Dictionary<string, object>? Regs = null;

    public Stack<object>? Stack = null;
    public ushort PopStackLength;
    public int ReserveStackLen = 0xF0;
    public byte StackAutoAlignment = 16;

    public bool IsStackReversed { get; set; } = false;

    protected Label lbResult;
    public ulong DefaultResult = 0;
    public AssemblerRegister64 ResultRegister = rax;

    public Action<Assembler>? CustomPrologue = null;
    public Action<Assembler>? CustomEpilogue = null;

    public ReadOnlyDictionary<RCVar, Label> Vars => vars.AsReadOnly();

    public RCGenerator(IntPtr func, Dictionary<string, object>? regs = null, Stack<object>? stack = null, ushort popStackLength = 0)
    {
        lbResult = asm.CreateLabel("result");
        funcPtr = (ulong)func;
        Regs = regs;
        Stack = stack;
        PopStackLength = popStackLength;
    }

    protected virtual (object, AssemblerMemoryOperandFactory?) ParseReg(string regname)
    {
        AssemblerMemoryOperandFactory? deref = null;
        if (regname[0] == '[' && regname[^1] == ']')
        {
            deref = __qword_ptr;
            regname = regname[1..^1];
        }
        return (typeof(AssemblerRegisters).GetField(regname)?.GetValue(null)!, deref);
    }

    protected virtual object GetReg(string regname) =>
        typeof(AssemblerRegisters).GetField(regname)?.GetValue(null)!;

    protected virtual object DerefVal(object value, AssemblerMemoryOperandFactory deref) =>
        typeof(AssemblerMemoryOperandFactory).GetMethod("get_Item", [value.GetType()])?.Invoke(deref, [value])!;

    protected virtual void AddOp(string op, params object[] args) =>
        typeof(Assembler).GetMethod(op, args.Select(a => a.GetType()).ToArray())?.Invoke(asm, args);

    protected virtual void AddMov((object, AssemblerMemoryOperandFactory?) reg, object value) =>
        AddOp(value is float ? "movss":"mov", reg.Item1, reg.Item2 == null ? value : DerefVal(value, reg.Item2.Value));

    protected virtual void AddLea((object, AssemblerMemoryOperandFactory?) reg, object value) =>
        AddOp("lea", reg.Item1, reg.Item2 == null ? value : DerefVal(value, reg.Item2.Value));

    protected virtual void AddCall(ulong funcPtr)
    {
        asm.call(funcPtr);
        asm.mov(__qword_ptr[lbResult], ResultRegister);
    }

    protected virtual void AddPrologue()
    {
        if (Stack != null)
            foreach (var svalue in Stack)
                if (svalue is RCVar)
                    (svalue as RCVar)!.OnPrologue(asm);

        if (Regs != null)
            foreach (var rvalue in Regs.Values)
                if (rvalue is RCVar)
                    (rvalue as RCVar)!.OnPrologue(asm);

        if (CustomPrologue != null)
            CustomPrologue(asm);

        if (StackAutoAlignment > 0)
        {
            if (StackAutoAlignment % 4 != 0)
                throw new InvalidOperationException($"{nameof(StackAutoAlignment)} should be aligned to 4");

            asm.push(rbp);
            asm.mov(rbp, rsp);
            asm.and(rsp, -StackAutoAlignment);
        }

        if (ReserveStackLen > 0)
            asm.sub(rsp, ReserveStackLen);
    }

    protected virtual void AddEpilogue()
    {
        if (StackAutoAlignment > 0)
        {
            asm.mov(rsp, rbp);
            asm.pop(rbp);
        }
        else if (ReserveStackLen > 0)
            asm.add(rsp, ReserveStackLen);

        if (CustomEpilogue != null)
            CustomEpilogue(asm);

        foreach (var cv in vars.Keys)
            cv.OnEpilogue(asm);

    }

    protected virtual void AddRegisterSetup(KeyValuePair<string, object> regdata)
    {
        (var reg, var deref) = ParseReg(regdata.Key);

        if (regdata.Value is RCVar)
        {
            var cv = (RCVar)regdata.Value;
            vars[cv] = cv.CreateLabel(asm);

            if (deref != null)
                AddMov((reg, __qword_ptr), vars[cv]);
            else
                AddLea((reg, __qword_ptr), vars[cv]);
        }
        else
            AddMov((reg, deref), regdata.Value);
    }

    protected virtual void PushStackSetup(object sval)
    {
        if (sval is RCVar)
        {
            var cv = (RCVar)sval;
            vars[cv] = cv.CreateLabel(asm);
            asm.push(__qword_ptr[vars[cv]]);
        }
        else if (sval is IntPtr || sval is ulong || sval is long)
        {
            AddMov((rax, null), sval);
            asm.push(rax);
        }
        else
            AddOp("push", sval);
    }

    protected virtual void AddVar(RCVar cv)
    {
        var lb = vars[cv];
        asm.Label(ref lb);
        vars[cv] = lb;
        cv.Label = lb;
        asm.db(cv.Value);
    }

    public virtual unsafe void GenerateCode()
    {
        AddPrologue();

        if (Stack != null)
        {
            var stack = IsStackReversed ? Stack : Stack.Reverse();
            foreach (var svalue in stack)
                PushStackSetup(svalue);
        }

        if (Regs != null)
            foreach (var regdata in Regs)
                AddRegisterSetup(regdata);

        AddCall(funcPtr);

        for (int i = 0; i < PopStackLength; i++)
            asm.pop(rax);

        AddEpilogue();

        asm.ret();

        asm.int3();
        asm.int3();
        asm.int3();
        asm.int3();

        foreach (var cv in vars.Keys)
            AddVar(cv);

        asm.Label(ref lbResult);
        asm.dq(DefaultResult);        
    }

    public virtual CompiledResult Compile()
    {
        var labs = new List<Label>([lbResult]);
        labs.AddRange(vars.Values);
        return asm.Compile(labels: labs.ToArray());
    }
}
